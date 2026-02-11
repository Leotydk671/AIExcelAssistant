import socket
import json
import threading
import time
from enum import Enum
from dataclasses import dataclass, asdict
from typing import Optional, Callable, Any

class MessageType(Enum):
    EXECUTE = "execute"
    READY = "ready"
    ACK = "execute_ack"
    HEARTBEAT = "heartbeat"
    HEARTBEAT_ACK = "heartbeat_ack"

@dataclass
class Message:
    type: str
    timestamp: float = None
    data: dict = None
    
    def __post_init__(self):
        if self.timestamp is None:
            self.timestamp = time.time()
    
    @classmethod
    def from_json(cls, json_str: str):
        """从JSON字符串创建Message对象"""
        data = json.loads(json_str)
        return cls(**data)
    
    def to_json(self) -> str:
        """转换为JSON字符串"""
        return json.dumps(asdict(self))

class HeartbeatMonitor:
    """心跳监控器"""
    def __init__(self, timeout: int = 60, interval: int = 45):
        self.timeout = timeout
        self.interval = interval
        self.last_received = time.time()
        self.last_sent = time.time()
        self.running = False
        self.thread: Optional[threading.Thread] = None
        self.on_timeout: Optional[Callable] = None
        self._lock = threading.Lock()
        
    def start(self, send_callback: Callable, on_timeout: Optional[Callable] = None):
        self.on_timeout = on_timeout
        self.running = True
        self.thread = threading.Thread(
            target=self._monitor_loop,
            args=(send_callback,),
            daemon=True
        )
        self.thread.start()
        
    def stop(self):
        self.running = False
        if self.thread and self.thread.is_alive():
            self.thread.join(timeout=2)
            
    def _monitor_loop(self, send_callback: Callable):
        while self.running:
            current_time = time.time()
            #print("循环")
            # 检查是否超时
            with self._lock:
                time_since_last = current_time - self.last_received
                if time_since_last > self.timeout:
                    print(f"心跳超时: {time_since_last:.1f}秒未收到消息")
                    if self.on_timeout:
                        self.on_timeout()
                    break
                
                # 发送心跳
                if current_time - self.last_sent > self.interval:
                    send_callback()
                    self.last_sent = current_time
            
            time.sleep(1)
            
    def update_received(self):
        with self._lock:
            self.last_received = time.time()
        
    def update_sent(self):
        with self._lock:
            self.last_sent = time.time()

class CSharpSocketComm:
    """
    与C#程序通信的Socket连接类
    使用单消息槽存储最近的非心跳消息
    """
    
    def __init__(self, host: str = '127.0.0.1', port: int = 12111):
        self.host = host
        self.port = port
        self.socket: Optional[socket.socket] = None
        self._connected = False
        self._lock = threading.Lock()
        self.heartbeat = HeartbeatMonitor(timeout=60, interval=45)
        
        # 消息接收线程
        self._receive_thread: Optional[threading.Thread] = None
        self._receive_running = False
        self._receive_buffer = b""
        
        # 单消息槽（线程安全）
        self._message_slot: Optional[Message] = None
        self._message_slot_lock = threading.Lock()
        self._message_slot_event = threading.Event()  # 用于等待新消息
        
        # 回调函数
        self.on_signal_received: Optional[Callable[[str], None]] = None
        self.on_connect: Optional[Callable] = None
        self.on_disconnect: Optional[Callable] = None
        self.on_error: Optional[Callable[[str], None]] = None
        
    @property
    def connected(self) -> bool:
        """获取连接状态（线程安全）"""
        with self._lock:
            return self._connected
    
    @connected.setter
    def connected(self, value: bool):
        """设置连接状态（线程安全）"""
        with self._lock:
            self._connected = value
            
    def connect(self) -> bool:
        """连接到C#程序"""
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.settimeout(10.0)  # 连接超时
            self.socket.connect((self.host, self.port))
            
            # 连接成功后设置短超时，用于非阻塞接收
            self.socket.settimeout(0.1)
            
            self.connected = True
            
            # 启动心跳监控
            self.heartbeat.start(
                send_callback=self._send_heartbeat,
                on_timeout=self._on_heartbeat_timeout
            )
            
            # 启动消息接收线程
            self._start_receive_thread()
            
            print(f"已连接到 {self.host}:{self.port}")
            if self.on_connect:
                self.on_connect()
            return True
        except Exception as e:
            print(f"连接失败: {e}")
            if self.on_error:
                self.on_error(f"连接失败: {e}")
            return False
    
    def _start_receive_thread(self):
        """启动消息接收线程"""
        self._receive_running = True
        self._receive_thread = threading.Thread(
            target=self._receive_loop,
            daemon=True
        )
        self._receive_thread.start()
        
    def _stop_receive_thread(self):
        """停止消息接收线程"""
        self._receive_running = False
        if self._receive_thread and self._receive_thread.is_alive():
            self._receive_thread.join(timeout=2)
    
    def _receive_loop(self):
        """消息接收循环（在后台线程中运行）"""
        while self._receive_running and self.connected:
            try:
                messages = self._receive_messages()
                for message in messages:
                    self._process_received_message(message)
            except Exception as e:
                if self.connected:
                    print(f"接收循环错误: {e}")
                    if self.on_error:
                        self.on_error(f"接收循环错误: {e}")
                
            time.sleep(0.01)  # 短暂休眠
    
    def _process_received_message(self, message: Message):
        """处理接收到的消息"""
        # 更新心跳时间
        self.heartbeat.update_received()
        
        # 根据消息类型处理
        if message.type == MessageType.HEARTBEAT.value:
            # 回复心跳确认
            self._send_heartbeat_ack()
        elif message.type == MessageType.HEARTBEAT_ACK.value:
            # 收到心跳确认，不执行操作
            print("收到心跳确认")
            pass
        else:
            # 非心跳消息，放入消息槽并触发回调
            with self._message_slot_lock:
                self._message_slot = message
                self._message_slot_event.set()  # 通知等待的线程
            
            if self.on_signal_received:
                self.on_signal_received(message.type)
    
    def _on_heartbeat_timeout(self):
        """心跳超时回调"""
        print("心跳超时，连接可能已断开")
        if self.on_error:
            self.on_error("心跳超时，连接已断开")
        self._safe_close()
        
    def _send_heartbeat(self):
        """发送心跳信号"""
        if not self.connected:
            return
            
        try:
            #print("发送心跳")
            message = Message(
                type=MessageType.HEARTBEAT.value,
                data={"source": "python"}
            )
            self._send_message(message)
            
        except Exception as e:
            print(f"发送心跳失败: {e}")
            if self.on_error:
                self.on_error(f"发送心跳失败: {e}")
            self.connected = False
            
    def _send_message(self, message: Message):
        """发送消息"""
        if not self.connected or not self.socket:
            return
            
        try:
            json_str = message.to_json()
            data = f"{json_str}\n".encode('utf-8')
            self.socket.sendall(data)
        except Exception as e:
            print(f"发送消息失败: {e}")
            if self.on_error:
                self.on_error(f"发送消息失败: {e}")
            raise
    
    def _receive_messages(self) -> list[Message]:
        """接收并解析所有可用消息"""
        if not self.connected or not self.socket:
            return []
            
        messages = []
        try:
            # 尝试接收数据
            try:
                chunk = self.socket.recv(4096)
                if not chunk:  # 连接已关闭
                    self.connected = False
                    return messages
                    
                self._receive_buffer += chunk
            except socket.timeout:
                # 没有数据可读是正常的
                return messages
            except Exception as e:
                print(f"接收数据失败: {e}")
                self.connected = False
                return messages
            
            # 解析完整消息（以换行符分隔）
            while b'\n' in self._receive_buffer:
                line, self._receive_buffer = self._receive_buffer.split(b'\n', 1)
                if line:
                    try:
                        json_str = line.decode('utf-8').strip()
                        if json_str:
                            message = Message.from_json(json_str)
                            messages.append(message)
                    except (json.JSONDecodeError, UnicodeDecodeError) as e:
                        print(f"解析消息失败: {e}")
                        if self.on_error:
                            self.on_error(f"解析消息失败: {e}")
                        
        except Exception as e:
            print(f"接收消息失败: {e}")
            if self.on_error:
                self.on_error(f"接收消息失败: {e}")
            self.connected = False
            
        return messages
    
    def clear_message_slot(self):
        """清空消息槽"""
        with self._message_slot_lock:
            self._message_slot = None
            self._message_slot_event.clear()
    
    def wait_for_signal(self, signal_name: str, 
                        timeout: Optional[float] = None, 
                        clear_first: bool = False, 
                        after_timestamp: Optional[float] = None) -> Optional[Message]:
        """
        等待特定信号
        
        Args:
            signal_name: 要等待的信号名称
            timeout: 超时时间（秒），None表示无限等待
            clear_first: 是否先清空消息槽
            
        Returns:
            收到的消息，超时返回None
        """
        if clear_first:
            self.clear_message_slot()
        
        start_time = time.time()
        
        while self.connected:
            # 检查超时
            if timeout is not None:
                elapsed = time.time() - start_time
                if elapsed > timeout:
                    print(f"等待信号 {signal_name} 超时 ({timeout}秒)")
                    if self.on_error:
                        self.on_error(f"等待信号 {signal_name} 超时 ({timeout}秒)")
                    return None
            
            # 检查消息槽
            with self._message_slot_lock:
                if (self._message_slot and 
                    self._message_slot.type == signal_name and
                    (after_timestamp is None or self._message_slot.timestamp > after_timestamp)):
                    message = self._message_slot
                    self._message_slot = None  # 取走消息
                    return message
            
            # 等待新消息到达（带超时）
            if timeout is not None:
                remaining = timeout - (time.time() - start_time)
                if remaining <= 0:
                    return None
                self._message_slot_event.wait(timeout=min(remaining, 0.1))
            else:
                self._message_slot_event.wait(timeout=0.1)
            
            # 重置事件，等待下次通知
            self._message_slot_event.clear()
        
        return None
    
    def wait_for_execution_signal(self, timeout: Optional[float] = None, 
                                 clear_first: bool = False,
                                 after_timestamp: Optional[float] = None) -> Optional[Message]:
        """
        等待执行信号（便捷方法）
        """
        return self.wait_for_signal(MessageType.EXECUTE.value, timeout, clear_first, after_timestamp)
    
    def send_signal(self, signal_name: str, data: Optional[dict] = None) -> tuple[bool, Optional[float]]:
        """发送信号"""
        if not self.connected:
            print("未连接到C#程序")
            return False, None
            
        try:
            message = Message(
                type=signal_name,
                data=data or {}
            )
            self._send_message(message)
            print(f"信号已发送: {signal_name}")
            return True, message.timestamp
        except Exception as e:
            print(f"发送信号失败: {e}")
            if self.on_error:
                self.on_error(f"发送信号失败: {e}")
            return False, None
    
    def send_ready(self) -> tuple[bool, Optional[float]]:
        """发送准备就绪信号（便捷方法）"""
        return self.send_signal(MessageType.READY.value)
    
    def send_acknowledgment(self, success: bool = True) -> tuple[bool, Optional[float]]:
        """发送确认信号（便捷方法）"""
        return self.send_signal(MessageType.ACK.value, {"success": success})
    
    def _send_heartbeat_ack(self):
        """发送心跳确认"""
        try:
            message = Message(
                type=MessageType.HEARTBEAT_ACK.value,
                data={"source": "python"}
            )
            self._send_message(message)
        except Exception as e:
            print(f"发送心跳确认失败: {e}")
            if self.on_error:
                self.on_error(f"发送心跳确认失败: {e}")
    
    def _safe_close(self):
        """安全关闭连接（线程安全）"""
        with self._lock:
            if not self._connected:
                return
                
            self._connected = False
            
            # 停止接收线程
            self._stop_receive_thread()
            
            # 停止心跳监控
            self.heartbeat.stop()
            
            # 清空消息槽
            self.clear_message_slot()
            
            # 关闭socket
            if self.socket:
                try:
                    self.socket.close()
                except:
                    pass
                self.socket = None
                
            print("连接已关闭")
            if self.on_disconnect:
                self.on_disconnect()
                
    def close(self):
        """关闭连接"""
        self._safe_close()
        
    def __enter__(self):
        """上下文管理器入口"""
        self.connect()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器出口"""
        self.close()

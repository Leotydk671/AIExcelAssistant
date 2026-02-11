import json
import logging
import os
import time
from datetime import datetime
from typing import Callable, Optional

from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.common.action_chains import ActionChains
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.webdriver.chrome.service import Service
from selenium.common.exceptions import StaleElementReferenceException
from webdriver_manager.chrome import ChromeDriverManager
from webdriver_manager.core.driver_cache import DriverCacheManager

from socket_comm import CSharpSocketComm, Message
from selenium.webdriver.chrome.options import Options


class BrowserManager:
    """浏览器管理器，优化chromedriver安装"""
    
    def __init__(self, headless=False, cache_days=30):
        self.headless = headless
        self.cache_days = cache_days

        os.environ['WDM_LOG'] = str(logging.NOTSET)
        os.environ['WDM_LOCAL'] = '1'
    
    def get_driver(self, options : Options):
        """获取WebDriver实例"""
        
        if self.headless:
            options.add_argument('--headless')
        
        # 获取chromedriver（使用缓存）
        driver_path = ChromeDriverManager(url='https://repo.huaweicloud.com/chromedriver/',
                                          cache_manager = DriverCacheManager (valid_range = self.cache_days)).install()
        print(f"下载到{driver_path}")
        
        service = Service(driver_path)
        driver = webdriver.Chrome(service=service, options=options)
        
        return driver


class DeepSeekWebAssistant:
    def __init__(self, base_path, on_code_saved, on_excute_receive):
        '''print("=" * 60)
        print("DeepSeek 网页对话助手 - 阻塞式完整版")
        print("=" * 60)
        print("说明：本程序将自动打开浏览器并保存对话")
        print("注意：AI生成回答时，输入将被暂时禁用")
        input("准备好后，请按回车键开始...")'''
        
        self.on_code_saved : Optional[Callable[[str], None]] = on_code_saved
        self.on_execute_receive : Optional[Callable[[dict], bool]] = on_excute_receive
        self.on_quit : Optional[Callable] = None

        self.base_path = base_path
        # 初始化驱动和选项
        self.setup_driver()
        
        # 核心选择器（根据你的发现）
        self.message_selector = ".ds-markdown"  # 消息块
        self.end_marker_selector = ".ds-flex._0a3d93b"  # 回答结束标记
        self.input_selector = "textarea" # 输入消息
        
        # 状态管理
        self.ai_generating = False
        self.conversation_history = []
        self.last_known_message_count = 0
        
        # 打开DeepSeek并登录
        self.open_deepseek()
        
        # 定位页面元素
        self.locate_page_elements()
        
    def setup_driver(self):
        """设置Chrome驱动"""
        options = webdriver.ChromeOptions()
        
        # 使用日常的Chrome用户数据（保持登录状态）
        user_data_dir = r"C:\MyAutomationProfile"
        if os.path.exists(user_data_dir):
            options.add_argument(f"user-data-dir={user_data_dir}")
            options.add_argument("profile-directory=Default")
            print("✅ 使用现有Chrome用户数据")
        else:
            print("⚠️  未找到用户数据，将打开新会话")
        
        # 防止被检测为自动化工具
        options.add_argument('--disable-blink-features=AutomationControlled')
        options.add_experimental_option("excludeSwitches", ["enable-automation"])
        options.add_experimental_option('useAutomationExtension', False)
        
        # 启动浏览器
        '''service = Service(ChromeDriverManager().install())
        self.driver = webdriver.Chrome(service=service, options=options)
        self.wait = WebDriverWait(self.driver, 15)'''

        broser = BrowserManager(headless=False, cache_days=30)
        self.driver = broser.get_driver(options)
        self.wait = WebDriverWait(self.driver, 15)
        
    def open_deepseek(self):
        """打开DeepSeek网站并等待登录"""
        print("正在打开DeepSeek...")
        self.driver.get("https://chat.deepseek.com")
        
        print("\n" + "=" * 60)
        print("✅ 浏览器窗口已打开！")
        print("请现在浏览器中完成登录操作（如果尚未登录）。")
        print("登录后，请确保停留在对话页面。")
        print("=" * 60 + "\n")
        
        # 等待可能的登录过程
        try:
            # 等待聊天输入框出现（登录成功的标志）
            chat_input_present = EC.presence_of_element_located(
                (By.CSS_SELECTOR, "textarea, [contenteditable='true'], input[type='text']")
            )
            WebDriverWait(self.driver, 30).until(chat_input_present)
            print("✅ 登录状态检测成功！")
        except:
            print("⚠️  未自动检测到登录状态，请确保你已登录。")
        
        #input("登录完成后，请按回车键继续...")
    
    def locate_page_elements(self):
        """定位页面关键元素"""
        print("\n" + "=" * 50)
        print("元素定位")
        print("=" * 50)
        
        # 消息选择器（使用已知的选择器）
        print(f"消息选择器已预设为: {self.message_selector}")
        print(f"输入框已预设为: {self.input_selector}")
        
        '''
        # 发送方式
        use_enter = input("\n发送方式: 按Enter发送消息，按Ctrl+Enter换行。需要程序自动点击发送按钮吗？(y/n): ").lower()
        if use_enter == 'y':
            self.send_selector = input("请输入发送按钮的CSS选择器: ").strip()
        else:
            self.send_selector = None
        '''
        print("\n✅ 元素定位完成！")
    
    def is_in_thinking_content(self, element):
        """
        检查元素是否在思考内容区域内
        直接检查父元素是否有.ds-think-content类
        """
        try:
            # 获取父元素
            parent = element.find_element(By.XPATH, "..")
            # 检查父元素的class属性
            parent_classes = parent.get_attribute("class") or ""
            return "ds-think-content" in parent_classes
        except:
            return False
    
    def determine_message_role(self, element, index, text):
        """判断消息角色"""
        try:
            class_name = element.get_attribute("class") or ""
            parent = self.driver.execute_script("return arguments[0].parentNode;", element)
            parent_class = parent.get_attribute("class") or ""
            
            if any(keyword in class_name.lower() or keyword in parent_class.lower() 
                  for keyword in ["user", "human", "我", "提问"]):
                #return "user"
                return "assistant"
            elif any(keyword in class_name.lower() or keyword in parent_class.lower() 
                    for keyword in ["assistant", "bot", "ai", "deepseek", "模型", "回答"]):
                return "assistant"
        except:
            pass
        
        # 默认基于索引判断（假设用户和AI交替发言）
        return "user" if index % 2 == 0 else "assistant"
    
    
    def wait_for_end_marker_simple(self, initial_marker_count, timeout=120):
        """
        通过监测结束标记的数量是否增加来判断
        :param initial_marker_count: 开始等待时结束标记的数量
        :param timeout: 最大等待时间
        :return: True如果标记数量增加
        """
        start_time = time.time()
        
        while time.time() - start_time < timeout:
            current_markers = self.driver.find_elements(By.CSS_SELECTOR, self.end_marker_selector)
            current_count = len(current_markers)
            
            # 如果标记数量增加了，说明有新的回答完成了
            if current_count > initial_marker_count:
                return True
            
            time.sleep(2)
        
        return False
    
    def copy_code_blocks_simple(self, message_element):
        """代码提取"""
        code_blocks = []
        
        # 方法1：直接找pre标签
        try:
            pre_elements = message_element.find_elements(By.TAG_NAME, "pre")
            for pre in pre_elements:
                if pre.is_displayed():
                    text = pre.text.strip()
                    if text and len(text) > 10:
                        code_blocks.append(text)
        except:
            pass
        
       
        return code_blocks
    

    def save_codes_to_same_file(self, code_blocks, filename):
        """将代码块保存到固定文件，每次覆盖"""
        if not code_blocks:
            print("⚠️  没有代码块可保存")
            return
        
        with open(filename, 'w', encoding='utf-8') as f:  # 使用覆盖写入
            for i, code in enumerate(code_blocks, 1):
                # 添加时间戳和分隔符
                timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
                f.write(f"import xlwings as xw\nfrom xlwings import Sheet\n")
                f.write(f"# {'='*60}\n")
                f.write(f"# 代码块 {i} - 提取时间: {timestamp}\n")
                f.write(f"# {'='*60}\n\n")
                f.write(code)
                f.write("\n\n" + "#"*60 + "\n\n")
        
        print(f"✅ 代码块已保存到: {filename}")

        self.on_code_saved(filename)


    def capture_messages(self):
        """捕获新消息"""
        try:
            current_elements = self.driver.find_elements(By.CSS_SELECTOR, self.message_selector)
            current_count = len(current_elements)
            
            if current_count <= self.last_known_message_count:
                return []
            
            new_messages = []
            
            for i in range(self.last_known_message_count, current_count):
                try:
                    # 重新获取元素列表，避免过时引用
                    elements = self.driver.find_elements(By.CSS_SELECTOR, self.message_selector)
                    if i >= len(elements):
                        continue
                    
                    msg_element = elements[i]
                    text = msg_element.text.strip()
                    
                    if len(text) < 2:
                        continue
                    
                    #role = self.determine_message_role(msg_element, i, text)

                    role = "assistant"
                    if(self.is_in_thinking_content(msg_element)):
                        role = "thinker"
                    
                    # 如果是AI消息，等待其完成
                    '''if role == "assistant":
                        self.ai_generating = True
                        print("⏳ AI正在生成回答...（输入已禁用）")
                        
                        if self.wait_for_end_marker():
                            # 结束标记出现后，重新获取最新文本
                            elements = self.driver.find_elements(By.CSS_SELECTOR, self.message_selector)
                            if i < len(elements):
                                text = elements[i].text.strip()
                            print("✅ AI回答完成！")
                        else:
                            print("⚠️  等待AI回答完成超时")
                        
                        self.ai_generating = False
                        print("🔄 输入已启用")'''
                    
                    code_blocks = []
        
                    if role == "assistant": #判断如果是生成内容
                        self.ai_generating = True
                        # 记录当前的结束标记数量
                        initial_marker_count = len(self.driver.find_elements(By.CSS_SELECTOR, self.end_marker_selector))
                        print(f"⏳ 等待AI回答完成 (当前结束标记数: {initial_marker_count})...")
                        
                        if self.wait_for_end_marker_simple(initial_marker_count):
                            print(f"✅ 检测到新的结束标记，回答完成。")
                            text = msg_element.text.strip()  # 重新获取最新文本

                        code_blocks = self.copy_code_blocks_simple(msg_element)
                        if code_blocks:
                            print(f"✅ 发现 {len(code_blocks)} 个代码块")
                        
                        self.ai_generating = False

                    if role == "thinker":
                        print("思考内容，暂不记录")
                    
                    # 去重检查
                    msg_hash = hash(f"{role}_{text[:200]}")
                    is_duplicate = any(
                        hash(f"{m.get('role', '')}_{m.get('content', '')[:200]}") == msg_hash
                        for m in self.conversation_history[-5:]
                    )
                    
                    if not is_duplicate and text:
                        message_data = {
                            "role": role,
                            "content": text,
                            "timestamp": datetime.now().isoformat(),
                            "length": len(text)
                        }

                        if code_blocks:
                           #message_data["code_blocks"] = code_blocks
                           self.save_codes_to_same_file(code_blocks, 
                                                        filename = os.path.join(self.base_path, "InterFiles/gen_pycode.py"))

                        new_messages.append(message_data)
                        self.conversation_history.append(message_data)
                        
                        indicator = "🤖" if role == "assistant" else "👤"
                        status = " (等待完成)" if role == "assistant" else ""
                        print(f"{indicator} [{role}]{status}: {text[:100]}{'...' if len(text) > 100 else ''}")
                        
                except StaleElementReferenceException:
                    print(f"⚠️  消息元素已更新，跳过索引 {i}")
                    continue
                except Exception as e:
                    print(f"⚠️  处理消息时出错: {e}")
                    continue
            
            # 更新计数器
            if new_messages:
                self.last_known_message_count = current_count
            
            return new_messages
            
        except Exception as e:
            # 确保异常时重置AI生成状态
            self.ai_generating = False
            print(f"❌ 捕获消息过程出错: {e}")
            return []
    
    def send_ctrl_enter(self, input_element):
        """发送Ctrl+Enter组合键（用于换行）"""
        # 方法1：使用ActionChains
        actions = ActionChains(self.driver)
        actions.key_down(Keys.CONTROL)
        actions.send_keys(Keys.ENTER)
        actions.key_up(Keys.CONTROL)
        actions.perform()
        time.sleep(0.05)

    def send_message(self, text):
        """向网页发送消息"""
        try:
            if not hasattr(self, 'input_selector'):
                print("❌ 未设置输入框选择器")
                return False
            
            # 定位输入框
            input_box = self.wait.until(
                EC.element_to_be_clickable((By.CSS_SELECTOR, self.input_selector))
            )
            
            # 清除并输入文本
            input_box.clear()
            
            # 逐字符输入（模拟真人输入）
            '''for char in text:
                input_box.send_keys(char)
                time.sleep(0.01)  # 减慢输入速度'''
            
            if '\n' in text:
                lines = text.split('\n')
                
                for i, line in enumerate(lines):
                    # 输入当前行
                    if line:
                        input_box.send_keys(line)
                    
                    # 如果不是最后一行，发送Ctrl+Enter换行
                    if i < len(lines) - 1:
                        # 方法1：使用ActionChains（最可靠）
                        self.send_ctrl_enter(input_box)
                        
                        # 或者方法2：使用JavaScript
                        # self.send_ctrl_enter_js(input_box)
                
                print(f"✅ 已发送多行问题（{len(lines)} 行）")
            else:
                # 单行问题，直接输入
                input_box.send_keys(text)
                print(f"✅ 已发送单行问题")
            
            # 发送消息
            if hasattr(self, 'send_selector') and self.send_selector:
                send_button = self.driver.find_element(By.CSS_SELECTOR, self.send_selector)
                send_button.click()
            else:
                # 按Enter键发送
                input_box.send_keys(Keys.RETURN)
            
            print(f"✅ 已发送: {text[:50]}...")
            time.sleep(1)  # 等待消息发送完成
            
            # 发送后预期AI将开始生成
            self.ai_generating = True
            return True
            
        except Exception as e:
            print(f"❌ 发送消息失败: {e}")
            return False
    
    def save_conversation(self):
        """保存对话到文件"""
        if not self.conversation_history:
            print("⚠️  没有对话内容可保存")
            return
        
        #filename = f"deepseek_chat_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
        filename = os.path.join(self.base_path, "deepseek_chat_record.json")
        
        # 去重
        seen = set()
        unique_history = []
        for msg in self.conversation_history:
            msg_hash = hash(msg['content'][:200] + msg['role'])
            if msg_hash not in seen:
                seen.add(msg_hash)
                unique_history.append(msg)
        
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(unique_history, f, ensure_ascii=False, indent=2)
        
        print(f"💾 对话已保存到: {filename}")
        return filename

    def run_main_loop(self, comm : CSharpSocketComm):
        """主循环：阻塞式输入控制"""
        print("\n" + "=" * 60)
        print("开始对话监控")
        print("=" * 60)
        print("命令说明:")
        print("  'save' - 立即保存对话")
        print("  'status' - 查看当前状态")
        print("  'quit' - 退出程序")
        print("-" * 60)
        
        print("开始连接C#")


        last_save_time = time.time()
        
        try:
            while comm.connected:
                current_time = time.time()
                
                # 1. 捕获消息（这会更新AI生成状态）
                self.capture_messages()
                
                # 2. 如果AI不在生成中，等待用户输入
                if not self.ai_generating:
                    print("\n💭 请输入你的问题: ", end="", flush=True)
                    
                    user_input = None

                    # 阻塞式等待用户输入
                    try:
                        ready_flag, timestamp = comm.send_ready()
                        exemessage : Message = None
                        if (ready_flag and 
                            (exemessage := comm.wait_for_execution_signal(timeout=None, after_timestamp=timestamp)) is not None     ):
                            
                            with open(os.path.join(self.base_path, 'InterFiles/input.txt'), 'r', encoding='utf-8') as file1:
                                user_input = file1.read()

                            success = self.on_execute_receive(exemessage.data)
                            # 发送确认
                            comm.send_acknowledgment(success = success)
                        else:
                            print("等待信号失败或超时")
                            break
                    except EOFError:
                        print("\n检测到输入结束")
                        break
                    except KeyboardInterrupt:
                        print("\n检测到中断信号")
                        break
                    except Exception as e:
                        print(f"出现了错误: {e}")
                    
                    if user_input:
                        user_input = user_input.strip()
                    
                    if not user_input:
                        continue
                    
                    # 处理命令
                    if user_input.lower() == 'quit':
                        print("正在退出...")
                        break
                    elif user_input.lower() == 'save':
                        self.save_conversation()
                        continue
                    elif user_input.lower() == 'status':
                        print(f"\n当前状态:")
                        print(f"  AI生成中: {'是' if self.ai_generating else '否'}")
                        print(f"  已保存消息数: {len(self.conversation_history)}")
                        print(f"  最后消息计数: {self.last_known_message_count}")
                        continue
                    
                    # 发送用户消息
                    success = self.send_message(user_input)
                    if success:
                        # 记录用户消息
                        self.conversation_history.append({
                            "role": "user",
                            "content": user_input,
                            "timestamp": datetime.now().isoformat()
                        })
                        print("✅ 已发送，等待AI回答...")
                
                # 3. 如果AI正在生成，显示等待提示
                else:
                    # 显示等待动画（简单版本）
                    dots = int(time.time() * 2) % 4
                    print(f"\r⏳ AI生成中{'.' * dots}   ", end="", flush=True)
                    time.sleep(0.5)
                
                # 4. 定期自动保存（每5分钟）
                if current_time - last_save_time > 300:
                    self.save_conversation()
                    last_save_time = current_time
                
        except KeyboardInterrupt:
            print("\n\n停止监控...")
        except Exception as e:
            print(f"\n程序运行出错: {e}")
            import traceback
            traceback.print_exc()
        finally:
            print("\n" + "=" * 60)
            print("程序结束")
            print("=" * 60)
            
            # 最终保存
            self.save_conversation()
            
            
            self.driver.quit()

            if self.on_quit:
                self.on_quit()

            '''
            close_browser = input("\n是否关闭浏览器窗口？(y/n): ").lower()
            if close_browser == 'y':
                self.driver.quit()
                print("浏览器已关闭")
            else:
                print("浏览器窗口保持打开")
            '''

            print("再见！")



import importlib
import os
from excel_protector import ProtectedExcelManager
from typing import Any, Optional
from socket_comm import CSharpSocketComm
from deepseek_assistant import DeepSeekWebAssistant

def import_work_module(work_path: str):
    """
    从指定路径导入 work.py 模块
    """
    # 确保路径存在
    if not os.path.exists(work_path):
        raise FileNotFoundError(f"找不到文件: {work_path}")
    
    # 获取模块名
    module_name = os.path.splitext(os.path.basename(work_path))[0]
    
    # 使用 importlib 从文件路径导入
    spec = importlib.util.spec_from_file_location(module_name, work_path)
    if spec is None:
        raise ImportError(f"无法从 {work_path} 创建模块规格")
    
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    
    return module

def safe_call_p_work(work_path: str, *args, **kwargs) -> Optional[Any]:
    """
    安全调用指定路径的 work.py 中的 sheet_work 函数
    """
    try:
        # 导入模块
        work_module = import_work_module(work_path)
        
        # 检查函数是否存在
        if not hasattr(work_module, 'sheet_work'):
            print(f"警告: {work_path} 中没有 sheet_work 函数")
            return None
        
        # 调用函数
        return work_module.sheet_work(*args, **kwargs)
        
    except FileNotFoundError as e:
        print(f"错误: {e}")
        return None
    except ImportError as e:
        print(f"导入错误: {e}")
        return None
    except Exception as e:
        print(f"执行错误: {type(e).__name__}: {e}")
        return None



def main():
    manager = MainManager()
    manager.start()
    

class MainManager():
    def __init__(self):
        self.excel = ProtectedExcelManager(visible=True)
        self.comm = CSharpSocketComm(host='127.0.0.1', port=12111)
        self.comm.on_connect = self.on_connect
        self.comm.on_disconnect = self.on_disconnect
        self.comm.on_error = self.on_error
        self.comm.on_signal_received = self.on_signal_received

    def start(self):
        self.excel.initialize()

        if self.comm.connect():
            assistant = DeepSeekWebAssistant(base_path = os.path.dirname(os.path.abspath(__file__)), 
                                            on_code_saved = self.on_code_saved,
                                            on_excute_receive = self.on_execute_received)
            assistant.on_quit = self.on_quit
            assistant.run_main_loop(self.comm)

    def on_connect(self):
        print("连接成功回调")
        
    def on_disconnect(self):
        print("连接断开回调")
        
    def on_error(self, error_msg):
        print(f"错误回调: {error_msg}")
        
    def on_signal_received(self, signal_name : str):
        print(f"收到信号{signal_name}")
    
    def on_quit(self):
        print(f"结束回调")
        self.comm.close()
        self.excel.close()

    def on_code_saved(self, file : str):
        try :
            safe_call_p_work(file, work_sheet=self.excel.ws)
        except FileNotFoundError:
            print("错误，找不到文件")

    def on_execute_received(self, data : dict) -> bool:
        operate_file : str = data.get("rfile")
        print("设置关联文件")
        if(operate_file):
            return self.excel.set_workelements(operate_file)
        else:
            print("错误，无效的关联文件")
            return False

if __name__ == "__main__":
    main()
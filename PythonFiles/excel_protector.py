import xlwings as xw
import pythoncom
import os

class ProtectedExcelManager:
    """受保护的Excel管理器：用户只能看，不能改"""
    
    def __init__(self, visible=True, sheet_password="protected123", workbook_password="structure456"):
        self.history_filepath = None
        self.visible = visible
        self.sheet_password = sheet_password
        self.workbook_password = workbook_password
        self.app = None
        self.wb = None
        self.ws = None
        self.is_initialized = False
        
    def initialize(self, filepath=None):
        """初始化Excel应用"""
        try:
            pythoncom.CoInitialize()
            self.is_initialized = True
            return True
        except Exception as e:
            print(f"COM初始化失败: {e}")
            return False
            
    def set_workelements(self, filepath=None):
        """设置工作簿和工作表"""
        print(f"尝试设置工作簿: {filepath}")
        
        # 如果文件路径相同且已设置，直接返回
        if filepath == self.history_filepath and self.wb is not None:
            print("工作簿已设置，跳过")
            return True
            
        try:
            # 清理现有实例
            self._cleanup()
            
            # 创建新的Excel实例
            self.app = xw.App(visible=self.visible, add_book=False)
            
            # 打开或创建工作簿
            if filepath and os.path.exists(filepath):
                try:
                    # 尝试打开文件
                    self.wb = self.app.books.open(filepath)
                    print(f"成功打开工作簿: {filepath}")
                except Exception as e:
                    print(f"无法打开文件 {filepath}: {e}")
                    # 创建空白工作簿
                    self.wb = self.app.books.add()
                    print("创建空白工作簿")
            else:
                if filepath:
                    print(f"文件不存在: {filepath}")
                self.wb = self.app.books.add()
                print("创建空白工作簿")
            
            # 设置活动工作表
            if len(self.wb.sheets) > 0:
                self.ws = self.wb.sheets.active or self.wb.sheets[0]
            else:
                self.ws = self.wb.sheets.add()
            
            # 应用保护
            success = self._apply_protection_safely()
            
            if success:
                self.history_filepath = filepath
                print(f"工作簿设置完成: {self.wb.name}")
                return True
            else:
                print("工作簿设置完成，但保护未完全应用")
                self.history_filepath = filepath
                return True  # 即使保护失败，仍然返回True（工作簿已打开）
                
        except Exception as e:
            print(f"工作簿设置失败: {e}")
            self._cleanup()
            self.history_filepath = None
            return False
    
    def _apply_protection_safely(self):
        """安全地应用保护，处理各种异常情况"""
        try:
            # 1. 确保工作表未受保护
            self._unprotect_sheet_safely()
            
            # 2. 锁定所有单元格
            try:
                self.ws.api.Cells.Locked = True
            except Exception as e:
                print(f"警告: 无法锁定单元格: {e}")
                # 继续执行，可能工作表处于特殊状态
            
            # 3. 保护工作表
            try:
                self.ws.api.Protect(
                    Password=self.sheet_password,
                    UserInterfaceOnly=True,
                    AllowFormattingCells=False,
                    AllowFormattingColumns=False,
                    AllowFormattingRows=False,
                    AllowInsertingColumns=False,
                    AllowInsertingRows=False,
                    AllowInsertingHyperlinks=False,
                    AllowDeletingColumns=False,
                    AllowDeletingRows=False,
                    AllowSorting=False,
                    AllowFiltering=False,
                    AllowUsingPivotTables=False
                )
            except Exception as e:
                print(f"警告: 无法保护工作表: {e}")
                return False
            
            # 4. 设置应用级别保护
            try:
                self.app.api.Interactive = False
                self.app.api.DisplayAlerts = False
            except Exception as e:
                print(f"警告: 无法设置应用级别保护: {e}")
            
            # 5. 保护工作簿结构
            try:
                # 先尝试取消现有保护
                try:
                    if self.wb.api.ProtectStructure:
                        self.wb.api.Unprotect(self.workbook_password)
                except:
                    pass
                    
                self.wb.api.Protect(
                    Password=self.workbook_password, 
                    Structure=True
                )
            except Exception as e:
                print(f"警告: 无法保护工作簿结构: {e}")
            
            print("保护已应用")
            return True
            
        except Exception as e:
            print(f"应用保护时发生错误: {e}")
            return False
    
    def _unprotect_sheet_safely(self):
        """安全地取消工作表保护"""
        try:
            if self.ws.api.ProtectContents:
                # 尝试用已知密码取消保护
                try:
                    self.ws.api.Unprotect(self.sheet_password)
                except:
                    # 尝试无密码取消保护
                    try:
                        self.ws.api.Unprotect()
                    except:
                        print("警告: 无法取消工作表保护，但将继续尝试")
        except Exception as e:
            print(f"取消保护时出错: {e}")
    
    def _cleanup(self):
        """清理现有Excel实例"""
        if self.wb is not None:
            try:
                self.wb.save()
                self.wb.close()
            except Exception as e:
                print(f"关闭工作簿时出错: {e}")
        
        if self.app is not None:
            try:
                self.app.quit()
            except Exception as e:
                print(f"退出Excel应用时出错: {e}")
        
        self.app = None
        self.wb = None
        self.ws = None
    
    def close(self):
        """关闭Excel"""
        self._cleanup()
        if self.is_initialized:
            pythoncom.CoUninitialize()
            self.is_initialized = False
        print("Excel已关闭")
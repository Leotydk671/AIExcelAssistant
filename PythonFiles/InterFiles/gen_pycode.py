import xlwings as xw
from xlwings import Sheet
# ============================================================
# 代码块 1 - 提取时间: 2026-02-11 01:44:23
# ============================================================

def sheet_work(work_sheet: 'Sheet') -> bool:
    import xlwings as xw
    
    # 找到最后一行
    last_row = work_sheet.used_range.last_cell.row
    
    # 写入新数据
    work_sheet.range(f"A{last_row + 1}").value = "裤子"
    work_sheet.range(f"B{last_row + 1}").value = "牛仔裤"
    work_sheet.range(f"C{last_row + 1}").value = 150
    work_sheet.range(f"D{last_row + 1}").value = "红色"
    work_sheet.range(f"E{last_row + 1}").value = "2手"
    
    return True

############################################################


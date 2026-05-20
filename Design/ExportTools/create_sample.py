import openpyxl
from openpyxl import Workbook

wb = Workbook()

# ========== Sheet 1: ItemConfig ==========
ws = wb.active
ws.title = "ItemConfig"
ws.append(["ID", "Name", "Price", "Type", "Drop IDs", "Compose"])      # Row 0: description (ignored)
ws.append(["id", "name", "price", "type", "dropIds", "compose"])       # Row 1: field name
ws.append(["int", "string", "int", "int", "list<int>", "map<int,int>"]) # Row 2: type
ws.append([1001, "Iron Sword", 500, 1, "1001,2001,3001", "2001:3,3001:1"])
ws.append([1002, "Wood Shield", 300, 2, "1002,2002", "2002:1"])
ws.append([1003, "Health Potion", 50, 3, "", ""])

# ========== Sheet 2: LevelConfig ==========
ws2 = wb.create_sheet("LevelConfig")
ws2.append(["level", "expRequired", "rewardItems", "bonus"])
ws2.append(["Level", "Exp Required", "Reward Items", "Bonus Multiplier"])
ws2.append(["int", "int", "list<int>", "map<int,float>"])
ws2.append([1, 0, "1001", "1001:1.5"])
ws2.append([2, 100, "1001,1002", "1001:1.2,1002:1.3"])
ws2.append([3, 300, "1001,1002,1003", "1001:1.1,1002:1.2,1003:1.5"])

filepath = "E:/AllProject/PureMVC_And_Server/Design/Excel/SampleConfig.xlsx"
wb.save(filepath)
print("Sample Excel created: " + filepath)

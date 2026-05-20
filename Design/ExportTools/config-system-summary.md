# Config Table System - Implementation Summary

## Overview

Built a complete config table pipeline: Excel (.xlsx) → JSON / Lua / C# data classes.

## Created Files

### Design/ (Project-External Tools)

| File | Description |
|---|---|
| `Design/Excel/` | Place .xlsx files here (empty, ready for use) |
| `Design/ExportTools/export_config.py` | Main export script (Python + openpyxl) |
| `Design/ExportTools/export_all.bat` | One-click export batch file |
| `Design/ExportTools/create_sample.py` | Sample Excel generator (for testing) |
| `Design/README.md` | Usage documentation |

### PureMVC_Framework/ (Client)

| File | Description |
|---|---|
| `Assets/GameConfig/Json/` | Exported JSON data files |
| `Assets/GameConfig/Lua/` | Exported Lua data files |
| `Assets/GameConfig/Cs/` | Auto-generated C# data classes |
| `Assets/Scripts/Manager/ConfigManager.cs` | **Updated** - added `Load<T>()` and `LoadAll()` |

### ProtoServer/ (Server)

| File | Description |
|---|---|
| `ProtoServer/Config/` | Auto-generated C# data classes (namespace: Config) |

## Key Design Decisions

1. **Export tool**: Python + openpyxl (cross-platform, flexible)
2. **Excel format**: Row 1 = field names, Row 2 = field types, Row 3+ = data
3. **Type system**: int/float/string/bool + list<T> + map<K,V>
4. **Output formats**: JSON (client runtime) + Lua (server alternative) + C# classes (both)
5. **JSON in GameConfig**: Exported to `Assets/GameConfig/Json/` for runtime loading
6. **ConfigManager**: Enhanced with `Load<T>()` (auto-path) and `LoadAll()` (batch)

## How to Use

1. Create .xlsx in `Design/Excel/` (one sheet = one config table)
2. Run `Design/ExportTools/export_all.bat`
3. In code: `ConfigManager.Load<ItemConfig>();` then `ConfigManager.Get<ItemConfig>(x => x.id == 1001);`

## Code Map Updated

- `.codemaker/CodeMap.md` - ConfigManager section + Config/ directory added
- `.codemaker/CodeMapServer.md` - Config/ directory added

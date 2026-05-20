# Design - Config Table System

## Directory Structure

```
Design/
├── Excel/                           ← Place .xlsx files here
│   ├── ItemConfig.xlsx
│   ├── LevelConfig.xlsx
│   └── ...
├── ExportTools/                     ← Export scripts
│   ├── export_config.py             ← Main export script (Python + openpyxl)
│   ├── export_all.bat               ← One-click export (Windows)
│   └── create_sample.py             ← Sample Excel generator
└── README.md
```

## Excel Format

| Row | Content | Required |
|---|---|---|
| Row 1 | Chinese description | No — ignored by exporter |
| Row 2 | Field names (English, match C# property names) | Yes — empty column = skip |
| Row 3 | Field types (see supported types below) | Yes |
| Row 4+ | Data rows | At least 1 data row |

> If Row 2 field name is empty, that column is skipped entirely.

### Supported Types

| Type | Example Value | C# Type |
|---|---|---|
| `int` | `1001` | `int` |
| `float` | `3.14` | `float` |
| `string` | `Iron Sword` | `string` |
| `bool` | `true` | `bool` |
| `list<int>` | `1001,2001,3001` | `List<int>` |
| `list<float>` | `1.5,2.3` | `List<float>` |
| `list<string>` | `a,b,c` | `List<string>` |
| `list<bool>` | `true,false` | `List<bool>` |
| `map<int,int>` | `1001:1,2001:2` | `Dictionary<int,int>` |
| `map<int,string>` | `1001:Iron,2001:Wood` | `Dictionary<int,string>` |
| `map<string,int>` | `iron:1001,wood:2001` | `Dictionary<string,int>` |
| `map<int,float>` | `1001:1.5,2001:2.3` | `Dictionary<int,float>` |
| `map<string,bool>` | `canSell:true,canTrade:false` | `Dictionary<string,bool>` |

### Separator Rules

- List: comma `,` separated
- Map: comma `,` separates pairs, colon `:` separates key:value
- String values containing comma/colon: wrap in double quotes `"val,ue"`

## How to Use

### 1. Create Excel File

Create `.xlsx` files in `Design/Excel/`. Each sheet = one config table.
Sheet name = config class name (e.g. `ItemConfig`).

### 2. Export

Double-click `Design/ExportTools/export_all.bat`

Or run manually:
```bash
cd Design/ExportTools
python export_config.py
```

### 3. Output Files

| Output | Path | Purpose |
|---|---|---|
| JSON | `PureMVC_Framework/DesignConfig/Json/` | Backup / manual inspection |
| JSON | `PureMVC_Framework/Assets/Resources/DesignConfig/` | Unity runtime loading |
| Lua | `PureMVC_Framework/DesignConfig/Lua/` | Server-side Lua loading |
| C# Class | `PureMVC_Framework/Assets/Scripts/Config/` | Client data class |
| C# Class | `ProtoServer/ProtoServer/Config/` | Server data class |

### 4. Load in Code (Client)

```csharp
// Auto-load by type name from Resources/DesignConfig/
ConfigManager.Load<ItemConfig>();

// Or with custom path
ConfigManager.Load<ItemConfig>("DesignConfig/ItemConfig");

// Query
ItemConfig item = ConfigManager.Get<ItemConfig>(x => x.id == 1001);
List<ItemConfig> all = ConfigManager.GetAll<ItemConfig>();
```

### 5. Load in Code (Server)

```csharp
// JSON mode (default)
ConfigLoader.LoadJson<ItemConfig>("DesignConfig/Json/ItemConfig.json");

// Lua mode (alternative)
ConfigLoader.LoadLua<ItemConfig>("DesignConfig/Lua/ItemConfig.lua");
```

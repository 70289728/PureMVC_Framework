## ADDED Requirements

### Requirement: Reserved Lua loader interface
The system SHALL define an `ILuaLoader` interface with methods for loading and executing Lua scripts, to be implemented later with xLua.

#### Scenario: Interface defined
- **WHEN** the xLua integration is ready
- **THEN** a developer can implement `ILuaLoader` with xLua and plug it into HotUpdateManager without changing other code

### Requirement: Reserved Lua script directory structure
The system SHALL create the directory `Assets/LuaScripts/` with subdirectories `HotUpdate/` and `BuiltIn/` for organizing Lua scripts.

#### Scenario: Directory structure exists
- **WHEN** a developer wants to add Lua scripts
- **THEN** hot-updatable scripts go in `LuaScripts/HotUpdate/` and built-in scripts go in `LuaScripts/BuiltIn/`

### Requirement: Lua files included in hot update manifest
The system SHALL support Lua script files (`.lua` extension) in the hot update manifest for future download and loading.

#### Scenario: Lua files in manifest
- **WHEN** the manifest includes "luascripts/game_logic.lua"
- **THEN** the download system downloads it to the persistent data path alongside other hot update files

## Why

The project currently has no hot update capability — all C# scripts, resources, and future Lua scripts are baked into the build. Any bug fix or content update requires a full app republish. Adding a hot update system enables rapid iteration: fix bugs, update UI, tweak configs, and add Lua game logic without going through app store review.

## What Changes

- **New**: `HotUpdateManager` — orchestrates the entire hot update lifecycle (check → download → verify → apply)
- **New**: `HotUpdateDownloader` — downloads update files via UnityWebRequest with retry and progress reporting
- **New**: `HotUpdateVersionChecker` — fetches version manifest from update server, compares with local version
- **New**: `HotUpdateAssetLoader` — loads assets from downloaded AssetBundles with interface abstraction for future Addressables migration
- **New**: `HotUpdateDllLoader` — loads hot-updated C# DLLs via HybridCLR
- **New**: `HotUpdateLuaLoader` — placeholder for xLua script loading (reserved interface)
- **New**: `HotUpdateConfig` — ScriptableObject for update server URL, version, manifest path, retry settings
- **New**: `HotUpdateManifest` — data structure for version manifest (file list, MD5, version number)
- **New**: `HotUpdateProxy` — PureMVC proxy for hot update state management
- **New**: `HotUpdateCommand` — PureMVC command triggered by STARTUP to run hot update check
- **New**: `HotUpdateUI` — simple UI mediator showing download progress and status
- **Modified**: `GameMain.cs` — integrate hot update check into startup flow (before GameStart)
- **Modified**: `AssetManager.cs` — add AssetBundle loading path alongside existing Editor AssetDatabase path
- **Modified**: `NotificationConst.cs` — add hot update related notification constants (HOT_UPDATE_PROGRESS, HOT_UPDATE_FAILED)
- **Modified**: `StartupMacroCommand` — add HotUpdateCommand as first sub-command

## Capabilities

### New Capabilities
- `hot-update-version-check`: Fetch version manifest from update server, compare with local version, determine if update is needed
- `hot-update-download`: Download update files (AssetBundles, DLLs, Lua scripts) with progress tracking, retry, and MD5 verification
- `hot-update-dll-loading`: Load hot-updated C# assemblies via HybridCLR runtime
- `hot-update-asset-loading`: Load assets from downloaded AssetBundles with interface abstraction
- `hot-update-lua-loading`: Reserved interface and directory structure for xLua script hot update
- `hot-update-lifecycle`: Full lifecycle management — check → download → verify → apply → enter game (or fallback)

### Modified Capabilities
- `asset-loading`: AssetManager gains AssetBundle loading path; Editor AssetDatabase path preserved for development
- `game-startup`: GameMain startup flow now includes hot update check before game initialization

## Impact

- **Affected code**: `GameMain.cs`, `AssetManager.cs`, `StartupMacroCommand.cs`, `NotificationConst.cs`
- **New dependencies**: HybridCLR package (`com.code-philosophy.hybridclr`), xLua package (reserved, not yet integrated)
- **New directories**: `Assets/Scripts/HotUpdate/`, `Assets/StreamingAssets/HotUpdate/` (local test server files)
- **Build pipeline**: AssetBundle build script needed (Editor tool to build bundles for hot update)
- **Server**: Local HTTP server for testing (Python `http.server` or Node `http-server`); production CDN later

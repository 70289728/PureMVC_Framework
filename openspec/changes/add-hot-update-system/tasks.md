## 1. Data Structures & Configuration

- [x] 1.1 Create `HotUpdateManifest` class — version, file list with name/md5/size
- [x] 1.2 Create `HotUpdateConfig` ScriptableObject — server URL, manifest path, retry count, timeout
- [x] 1.3 Create `HotUpdateFileEntry` class — single file entry in manifest
- [x] 1.4 Create `HotUpdateState` enum — Idle, Checking, Downloading, Verifying, Applying, Success, Failed
- [x] 1.5 Add hot update notification constants to `NotificationConst.cs` (HOT_UPDATE_PROGRESS, HOT_UPDATE_FAILED, HOT_UPDATE_STATE_CHANGED)

## 2. Core Managers

- [x] 2.1 Create `HotUpdateManager` — singleton, orchestrates full lifecycle, exposes `StartCheckAsync()`
- [x] 2.2 Create `HotUpdateVersionChecker` — fetches manifest JSON, compares versions, returns file diff list
- [x] 2.3 Create `HotUpdateDownloader` — downloads files via UnityWebRequest, retry logic, MD5 verification, progress callback
- [x] 2.4 Create `HotUpdateDllLoader` — loads AOT metadata + hot update DLL via HybridCLR (with availability check)
- [x] 2.5 Create `HotUpdateAssetLoader` — implements `IAssetLoader`, loads AssetBundles from persistentDataPath, caches bundles, fallback to Resources
- [x] 2.6 Create `HotUpdateLuaLoader` — placeholder implementing `ILuaLoader`, logs "not yet implemented"

## 3. Interfaces

- [x] 3.1 Create `IAssetLoader` interface — `LoadAsset<T>(bundlePath, assetName)`, `UnloadAllBundles()`
- [x] 3.2 Create `ILuaLoader` interface — `LoadScript(path)`, `ExecuteScript(path)`, `CallFunction(path, funcName, args)`

## 4. PureMVC Integration

- [x] 4.1 Create `HotUpdateProxy` — holds HotUpdateState, current version, progress info
- [x] 4.2 Create `HotUpdateCommand` — CommandBase, calls HotUpdateManager.StartCheckAsync(), sends notifications on state changes
- [x] 4.3 Register HotUpdateCommand in `StartupMacroCommand` as first sub-command

## 5. AssetManager Modification

- [x] 5.1 Modify `AssetManager` to accept and delegate to `IAssetLoader`
- [x] 5.2 Add AssetBundle loading path alongside existing Editor AssetDatabase path
- [x] 5.3 Add fallback logic: try AssetBundle → try Resources → return null

## 6. GameMain Startup Flow

- [x] 6.1 Modify `GameMain.Start()` to run hot update check before `InitModule()` and `GameStart()`
- [x] 6.2 Add coroutine/IEnumerator flow to wait for hot update completion
- [x] 6.3 Ensure game proceeds on both success and failure (graceful degradation)

## 7. Hot Update UI

- [x] 7.1 Create `UIHotUpdateMediator` — shows download progress bar, status text, error message
- [x] 7.2 Listen to HOT_UPDATE_PROGRESS and HOT_UPDATE_STATE_CHANGED notifications
- [x] 7.3 Auto-close on success, show retry/quit buttons on failure

## 8. Editor Tools

- [x] 8.1 Create `HotUpdateAssetBundleBuilder` Editor script — builds AssetBundles to StreamingAssets/HotUpdate/
- [x] 8.2 Create `HotUpdateManifestGenerator` Editor script — generates version manifest JSON with MD5 hashes
- [x] 8.3 Create menu items: "HotUpdate/Build AssetBundles", "HotUpdate/Generate Manifest"

## 9. Local Test Server

- [x] 9.1 Create `local_server/` directory with Python `server.py` — simple HTTP file server for StreamingAssets/HotUpdate/
- [x] 9.2 Create `local_server/start.bat` — one-click start script
- [x] 9.3 Add sample manifest and test AssetBundle to verify end-to-end flow

## 10. Lua Placeholder

- [x] 10.1 Create `Assets/LuaScripts/HotUpdate/` directory with `.gitkeep`
- [x] 10.2 Create `Assets/LuaScripts/BuiltIn/` directory with `.gitkeep`
- [x] 10.3 Add Lua file support in manifest format documentation

## 11. Code Map & Documentation

- [x] 11.1 Update `.codemaker/CodeMap.md` with new HotUpdate module entries
- [x] 11.2 Create summary document for hot update feature

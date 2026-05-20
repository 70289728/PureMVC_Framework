## Context

The PureMVC Framework project is a Unity client-server architecture. Currently all C# scripts and assets are baked into the build. The project uses PureMVC pattern with Facade/Command/Proxy/Mediator, has existing managers (UpdateManager, TimerManager, AudioManager, NetworkManager, UIManager, etc.), and uses AssetDatabase for Editor-only asset loading.

The hot update system needs to support three types of updates: C# DLLs (HybridCLR), AssetBundles (resources), and Lua scripts (xLua, reserved). The update server will initially be a local HTTP server for testing, with production CDN later.

## Goals / Non-Goals

**Goals:**
- Full hot update lifecycle: version check → download → MD5 verify → apply → enter game
- C# script hot update via HybridCLR (load hot-updated Assembly-CSharp.dll)
- AssetBundle-based resource hot update with interface abstraction for future Addressables migration
- Lua script hot update placeholder (xLua directory structure and loader interface)
- Progress reporting via PureMVC notifications for UI binding
- Local HTTP server testing support
- Fallback to built-in resources if hot update fails

**Non-Goals:**
- Server-side hot update distribution (CDN/OSS) — local HTTP only for now
- Addressables integration — interface reserved, not implemented
- xLua full integration — directory and interface reserved, not implemented
- Hot update for native code (il2cpp itself)
- Incremental/delta patching (binary diff) — full file replacement only
- Hot update while game is running (requires restart)

## Decisions

### Decision 1: HybridCLR for C# Hot Update
**Choice**: HybridCLR over ILRuntime
**Rationale**: Near-native performance, full C# feature support (generics, async/await, reflection), Unity official partnership. The project is early-stage so adopting the future-proof solution now avoids migration cost.
**Alternatives considered**: ILRuntime — rejected due to 10-100x performance penalty and limited C# feature support.

### Decision 2: AssetBundle with Interface Abstraction
**Choice**: Direct AssetBundle management with `IAssetLoader` interface
**Rationale**: Lightweight, no external dependency, full control. The `IAssetLoader` interface allows swapping to Addressables later without changing consumer code.
**Alternatives considered**: Addressables directly — rejected because user wants to start simple and migrate later.

### Decision 3: PureMVC Integration Pattern
**Choice**: HotUpdateProxy (state) + HotUpdateCommand (flow control) + HotUpdateManager (orchestration)
**Rationale**: Follows existing project patterns. Proxy holds state (version, progress, status), Command executes the flow, Manager handles the actual work. UI Mediator listens to notifications for progress display.
**Alternatives considered**: Standalone manager without PureMVC — rejected because it breaks project conventions.

### Decision 4: Version Manifest Format
**Choice**: JSON manifest file with version number, file list, MD5 hashes
**Rationale**: Simple, human-readable, easy to debug. Same format as Unity Addressables catalog for future migration.
```json
{
  "version": "1.0.1",
  "files": [
    {"name": "assetbundles/prefabs.ab", "md5": "abc123...", "size": 102400},
    {"name": "dlls/Assembly-CSharp.dll", "md5": "def456...", "size": 512000}
  ]
}
```

### Decision 5: Download Strategy
**Choice**: UnityWebRequest with retry (3 attempts), resume not supported initially
**Rationale**: UnityWebRequest is Unity's recommended HTTP client. Retry handles transient network issues. Resume support adds complexity (range requests, partial file merging) that's not needed for small update files.
**Alternatives considered**: HttpClient — rejected because it doesn't work on all Unity platforms (WebGL).

### Decision 6: Local Version Tracking
**Choice**: PlayerPrefs stores `hot_update_version` string
**Rationale**: Simple, already used by SaveManager. Survives app restarts. For production, this would move to a file in persistentDataPath.
**Alternatives considered**: Separate version file — more robust but adds complexity for same result.

### Decision 7: Hot Update Flow in Startup
**Choice**: Insert hot update check BEFORE GameStart() in GameMain, block game entry until complete
**Rationale**: Ensures all hot update code is loaded before any game logic runs. Prevents race conditions where game code tries to use types that haven't been hot-updated yet.
**Flow**: `Awake → InitManagers → HotUpdateCheck → (download/apply if needed) → InitModule → GameStart → ConnectServer → OpenLogin`

## Risks / Trade-offs

- **[Risk] HybridCLR package not yet installed** → Mitigation: HotUpdateDllLoader checks for HybridCLR availability at runtime, logs clear error if missing
- **[Risk] AssetBundle loading fails on first attempt** → Mitigation: Fallback to built-in Resources.Load, log error for debugging
- **[Risk] Large AssetBundles cause long download times** → Mitigation: Progress reporting via notifications, UI shows progress bar; future: split bundles by feature
- **[Risk] MD5 mismatch after download** → Mitigation: Re-download file (up to retry limit), then fallback to built-in
- **[Risk] Version rollback (server version lower than local)** → Mitigation: Only update when server version > local version; never downgrade
- **[Trade-off] Full file download vs delta patch** → Chose full file for simplicity; acceptable for small-to-medium projects; delta patching can be added later

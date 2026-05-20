## ADDED Requirements

### Requirement: Load assets from AssetBundle via IAssetLoader interface
The system SHALL provide an `IAssetLoader` interface for loading assets, with an AssetBundle-based implementation that loads from the persistent data path.

#### Scenario: Load asset from hot-updated AssetBundle
- **WHEN** a hot-updated AssetBundle exists at "{persistentDataPath}/HotUpdate/assetbundles/prefabs.ab"
- **THEN** calling `IAssetLoader.LoadAsset<GameObject>("assetbundles/prefabs.ab", "UILogin")` returns the UILogin prefab

#### Scenario: Fallback to built-in Resources when AssetBundle missing
- **WHEN** no hot-updated AssetBundle exists for the requested path
- **THEN** the system falls back to `Resources.Load` for the asset

### Requirement: Cache loaded AssetBundles
The system SHALL cache loaded AssetBundles to avoid repeated disk reads.

#### Scenario: Bundle cached after first load
- **WHEN** an AssetBundle is loaded for the first time
- **THEN** it is stored in a dictionary cache and reused on subsequent requests

#### Scenario: Cache cleared on unload
- **WHEN** `UnloadAllBundles()` is called
- **THEN** all cached AssetBundles are unloaded and the cache is cleared

### Requirement: Interface abstraction for future Addressables migration
The system SHALL define `IAssetLoader` interface so that AssetManager and other consumers depend on the interface, not the concrete AssetBundle implementation.

#### Scenario: Swap implementation
- **WHEN** a new `AddressablesAssetLoader : IAssetLoader` is created
- **THEN** consumers using `IAssetLoader` work without code changes

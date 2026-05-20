## ADDED Requirements

### Requirement: AssetManager supports AssetBundle loading path
The `AssetManager` class SHALL support loading assets from AssetBundles in addition to the existing Editor AssetDatabase path.

#### Scenario: Load from AssetBundle in runtime
- **WHEN** not in UNITY_EDITOR and a hot-updated AssetBundle exists
- **THEN** AssetManager loads the asset from the AssetBundle via HotUpdateAssetLoader

#### Scenario: Load from AssetDatabase in Editor
- **WHEN** in UNITY_EDITOR
- **THEN** AssetManager uses AssetDatabase.LoadAssetAtPath (existing behavior preserved)

### Requirement: AssetManager delegates to IAssetLoader
The `AssetManager` class SHALL delegate asset loading to an `IAssetLoader` implementation rather than loading directly.

#### Scenario: Delegation to AssetBundle loader
- **WHEN** HotUpdateManager has initialized the AssetBundle loader
- **THEN** AssetManager.LoadAsset calls IAssetLoader.LoadAsset which checks AssetBundle first, then falls back to Resources

## ADDED Requirements

### Requirement: Load hot-updated assembly via HybridCLR
The system SHALL load the hot-updated `Assembly-CSharp.dll` from the persistent data path using HybridCLR's `RuntimeApi.LoadMetadataForAOTAssembly` and `Assembly.Load`.

#### Scenario: Hot-updated DLL exists and loads successfully
- **WHEN** a hot-updated Assembly-CSharp.dll exists in the persistent data path
- **THEN** the system loads it via HybridCLR and all hot-updated types become available

#### Scenario: No hot-updated DLL exists
- **WHEN** no hot-updated DLL is found in the persistent data path
- **THEN** the system uses the built-in assembly (no-op)

#### Scenario: HybridCLR not installed
- **WHEN** HybridCLR package is not present in the project
- **THEN** the system logs a clear error message and skips DLL hot update

### Requirement: Load AOT metadata for HybridCLR
The system SHALL load AOT metadata DLLs required by HybridCLR before loading the hot update assembly.

#### Scenario: AOT metadata loaded
- **WHEN** HybridCLR is available and hot update DLL exists
- **THEN** the system loads AOT metadata from StreamingAssets before loading the hot update DLL

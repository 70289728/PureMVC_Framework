## ADDED Requirements

### Requirement: NetworkManager initialized in GameMain
`GameMain.InitManagers()` SHALL initialize `NetworkManager.Instance` so the singleton is created before gameplay begins.

#### Scenario: NetworkManager created on startup
- **WHEN** `GameMain.Start()` runs `InitManagers()`
- **THEN** `NetworkManager.Instance` is accessed, creating the MonoBehaviour singleton if it does not yet exist

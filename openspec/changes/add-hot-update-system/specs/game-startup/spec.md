## ADDED Requirements

### Requirement: Hot update check runs before game initialization
The `GameMain.Start()` method SHALL run the hot update check before calling `InitModule()` and `GameStart()`.

#### Scenario: Hot update check runs first
- **WHEN** GameMain.Start() executes
- **THEN** the sequence is: InitManagers → HotUpdateCheck → (wait for completion) → InitModule → GameStart → ConnectServer → OpenLogin

#### Scenario: Hot update success proceeds to game
- **WHEN** hot update completes successfully (or no update needed)
- **THEN** game initialization continues normally

#### Scenario: Hot update failure still proceeds to game
- **WHEN** hot update fails
- **THEN** game initialization continues with built-in resources (graceful degradation)

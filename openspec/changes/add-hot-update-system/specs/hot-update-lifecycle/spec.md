## ADDED Requirements

### Requirement: Full hot update lifecycle orchestration
The system SHALL execute the hot update lifecycle in this order: version check → download → verify → apply DLLs → apply assets → signal completion.

#### Scenario: Successful hot update
- **WHEN** server has a newer version
- **THEN** the system downloads all files, verifies MD5, loads DLLs, initializes asset loader, and sends HOT_UPDATE_SUCCESS

#### Scenario: No update needed
- **WHEN** server version equals local version
- **THEN** the system skips download and sends HOT_UPDATE_SUCCESS immediately

#### Scenario: Update fails
- **WHEN** download or verification fails after all retries
- **THEN** the system sends HOT_UPDATE_FAILED and proceeds with built-in resources

### Requirement: Progress notification during update
The system SHALL send HOT_UPDATE_PROGRESS notifications at each stage of the lifecycle so UI can display status.

#### Scenario: Progress stages
- **WHEN** the hot update lifecycle runs
- **THEN** notifications are sent for: CHECKING, DOWNLOADING, VERIFYING, APPLYING, SUCCESS, FAILED

### Requirement: Block game entry until hot update complete
The system SHALL prevent game logic from initializing until the hot update lifecycle finishes (success or failure).

#### Scenario: Game waits for hot update
- **WHEN** GameMain.Start() runs
- **THEN** InitModule() and GameStart() are deferred until HotUpdateManager signals completion

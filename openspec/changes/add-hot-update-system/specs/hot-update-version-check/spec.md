## ADDED Requirements

### Requirement: Fetch version manifest from update server
The system SHALL fetch a JSON version manifest from the configured update server URL at startup.

#### Scenario: Successful manifest fetch
- **WHEN** the update server is reachable and returns a valid manifest JSON
- **THEN** the system parses the manifest and stores version number and file list

#### Scenario: Server unreachable
- **WHEN** the update server is not reachable (timeout or connection refused)
- **THEN** the system logs a warning and proceeds with built-in resources (no hot update)

#### Scenario: Invalid manifest JSON
- **WHEN** the server returns malformed JSON
- **THEN** the system logs an error and proceeds with built-in resources

### Requirement: Compare server version with local version
The system SHALL compare the server manifest version against the locally stored version to determine if an update is needed.

#### Scenario: Server version is newer
- **WHEN** server version > local version
- **THEN** the system triggers the download flow

#### Scenario: Server version is same or older
- **WHEN** server version <= local version
- **THEN** the system skips download and proceeds to game entry

#### Scenario: No local version stored (first launch)
- **WHEN** no local version is found in PlayerPrefs
- **THEN** the system treats local version as "0.0.0" and triggers download if server version > "0.0.0"

### Requirement: Store version after successful update
The system SHALL persist the new version string to PlayerPrefs after all hot update files are successfully downloaded and verified.

#### Scenario: Version persisted after update
- **WHEN** all files are downloaded and MD5-verified
- **THEN** the system saves the server version to PlayerPrefs key "hot_update_version"

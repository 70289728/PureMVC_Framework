## ADDED Requirements

### Requirement: Download files from manifest
The system SHALL download each file listed in the version manifest that differs from local (by MD5 or if new).

#### Scenario: Download all new files
- **WHEN** the manifest contains 3 files and none exist locally
- **THEN** the system downloads all 3 files to the persistent data path

#### Scenario: Skip unchanged files
- **WHEN** a file's MD5 matches the locally cached file's MD5
- **THEN** the system skips downloading that file

#### Scenario: Download with progress reporting
- **WHEN** files are being downloaded
- **THEN** the system sends HOT_UPDATE_PROGRESS notification with current file index, total files, and bytes downloaded

### Requirement: Retry failed downloads
The system SHALL retry failed downloads up to 3 times before giving up.

#### Scenario: Download succeeds on retry
- **WHEN** the first download attempt fails due to network error
- **THEN** the system retries up to 2 more times and succeeds

#### Scenario: All retries exhausted
- **WHEN** all 3 download attempts fail
- **THEN** the system sends HOT_UPDATE_FAILED notification and falls back to built-in resources

### Requirement: Verify downloaded files with MD5
The system SHALL compute the MD5 hash of each downloaded file and compare it against the manifest entry.

#### Scenario: MD5 matches
- **WHEN** the computed MD5 equals the manifest MD5
- **THEN** the file is marked as verified and ready to use

#### Scenario: MD5 mismatch
- **WHEN** the computed MD5 does not match the manifest MD5
- **THEN** the system deletes the corrupted file and re-downloads it (counts toward retry limit)

### Requirement: Save files to persistent data path
The system SHALL save downloaded files to `Application.persistentDataPath/HotUpdate/` preserving the relative path structure from the manifest.

#### Scenario: File saved with directory structure
- **WHEN** manifest lists file "assetbundles/prefabs.ab"
- **THEN** the file is saved to "{persistentDataPath}/HotUpdate/assetbundles/prefabs.ab"

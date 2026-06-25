# Epic 2: Camera, Capture, and Streaming

## Epic Summary

Epic 2 delivers the production camera workflow for Shrimp Cam across Raspberry Pi/Linux and Windows hosts. This backlog covers cross-platform camera discovery, OS-specific capture and streaming command generation, manual and scheduled image capture, live MJPEG streaming, reconnect resilience, deterministic file storage, retention cleanup, daily timelapse generation, and motion-driven highlight capture. The stories are written to support incremental delivery while keeping camera behavior testable behind platform-aware abstractions.

## Stories

### SC-CC-01 - Discover Cameras on Linux Hosts

**User Story**  
As a Shrimp Cam operator, I want the system to discover available Linux camera devices so that I can configure and validate the correct capture source on Raspberry Pi deployments.

**Dependencies**
- None

**Test Expectations**
- Unit tests cover Linux discovery parsing for valid, empty, and malformed command output.
- Integration tests verify the discovery service maps detected devices into the shared camera descriptor model.
- Optional hardware smoke test verifies at least one connected UVC camera is surfaced on Raspberry Pi/Linux.

**Acceptance Criteria**

```gherkin
Scenario: Discover one or more Linux cameras
  Given Shrimp Cam is running on a Linux host
  And one or more camera devices are available to the OS
  When the application requests camera discovery
  Then the system returns each discoverable camera with a stable device path
  And each result includes a display name and platform type of Linux

Scenario: No Linux cameras are available
  Given Shrimp Cam is running on a Linux host
  And no camera devices are available to the OS
  When the application requests camera discovery
  Then the system returns an empty result set
  And the system records a warning that no Linux cameras were detected
```

### SC-CC-02 - Discover Cameras on Windows Hosts

**User Story**  
As a Shrimp Cam operator, I want the system to discover available Windows camera devices so that I can configure and validate the correct capture source on Windows deployments.

**Dependencies**
- None

**Test Expectations**
- Unit tests cover Windows discovery parsing for DirectShow-style device listings, empty output, and command failures.
- Integration tests verify discovered device names are normalized into the shared camera descriptor model.
- Optional hardware smoke test verifies a connected webcam appears with its Windows device name.

**Acceptance Criteria**

```gherkin
Scenario: Discover one or more Windows cameras
  Given Shrimp Cam is running on a Windows host
  And one or more camera devices are available to the OS
  When the application requests camera discovery
  Then the system returns each discoverable camera with a stable device identifier
  And each result includes a display name and platform type of Windows

Scenario: Windows discovery command fails
  Given Shrimp Cam is running on a Windows host
  And the camera discovery command cannot complete successfully
  When the application requests camera discovery
  Then the system returns a discovery failure result
  And the failure includes actionable diagnostics for logs and status reporting
```

### SC-CC-03 - Generate OS-Specific Camera Commands

**User Story**  
As a developer, I want Shrimp Cam to generate capture and streaming commands by operating system so that camera behavior stays consistent while platform differences remain isolated.

**Dependencies**
- SC-CC-01
- SC-CC-02

**Test Expectations**
- Unit tests verify Linux and Windows command generation for still capture, streaming, and discovery workflows.
- Unit tests verify configured resolution, frame rate, and source identifiers are correctly injected and escaped.
- Negative tests verify invalid configuration is rejected before process execution begins.

**Acceptance Criteria**

```gherkin
Scenario: Generate a Linux still capture command
  Given a Linux camera configuration with a valid device path
  When the system builds a still capture command
  Then the command targets the configured Linux device
  And the command includes the configured capture resolution

Scenario: Generate a Windows MJPEG stream command
  Given a Windows camera configuration with a valid device name
  When the system builds a live stream command
  Then the command targets the configured Windows device
  And the command includes the configured stream resolution and frame rate

Scenario: Reject invalid camera configuration
  Given a camera configuration is missing its required source identifier
  When the system builds a camera command
  Then the command is not generated
  And the system returns a validation error before process execution
```

### SC-CC-04 - Capture a Manual Snapshot

**User Story**  
As a Shrimp Cam operator, I want to trigger a manual snapshot so that I can immediately verify the camera view or capture a moment outside the schedule.

**Dependencies**
- SC-CC-03
- SC-CC-08

**Test Expectations**
- API tests verify manual capture requests return success metadata and file references.
- Integration tests verify a capture result writes the image and metadata together.
- Failure tests verify process failures surface a non-success API response without orphaned metadata.

**Acceptance Criteria**

```gherkin
Scenario: Capture a manual snapshot successfully
  Given a configured camera is online
  When an operator requests a manual capture
  Then the system stores a new image file in the configured capture layout
  And the system records capture metadata with source type Manual

Scenario: Manual capture fails because the camera is unavailable
  Given the configured camera is offline or inaccessible
  When an operator requests a manual capture
  Then the request fails with a camera unavailable result
  And no incomplete capture metadata is persisted
```

### SC-CC-05 - Run Scheduled Timelapse Capture

**User Story**  
As a Shrimp Cam operator, I want the system to capture images on a schedule so that I can build a timelapse history of the shrimp tank automatically.

**Dependencies**
- SC-CC-03
- SC-CC-08

**Test Expectations**
- Unit tests cover next-run calculation, schedule windows, and disabled schedule handling.
- Integration tests verify the scheduler triggers a single capture per due interval.
- Failure tests verify failed scheduled captures are logged and do not stop future schedule execution.

**Acceptance Criteria**

```gherkin
Scenario: Capture a scheduled timelapse frame
  Given scheduled timelapse capture is enabled
  And the current time matches a due capture interval
  When the scheduler runs
  Then the system captures one image for that interval
  And the system records metadata with source type Scheduled

Scenario: Skip a capture outside the active schedule window
  Given scheduled timelapse capture is enabled
  And the current time is outside the active schedule window
  When the scheduler runs
  Then no image is captured
  And the system records that the interval was skipped by schedule rules
```

### SC-CC-06 - Stream Live MJPEG Video

**User Story**  
As a Shrimp Cam viewer, I want a live MJPEG stream so that I can monitor the tank in near real time from the web app.

**Dependencies**
- SC-CC-03

**Test Expectations**
- API tests verify the MJPEG endpoint returns the expected content type and multipart boundary format.
- Integration tests verify the stream can start from a valid configured camera process.
- Failure tests verify stream startup errors return a non-success response without hanging the request.

**Acceptance Criteria**

```gherkin
Scenario: Start a live MJPEG stream
  Given a configured camera is online
  When a client requests the live stream endpoint
  Then the response content type is MJPEG compatible
  And the response begins streaming image frames from the configured camera

Scenario: Live stream cannot start because the camera is unavailable
  Given the configured camera is offline or inaccessible
  When a client requests the live stream endpoint
  Then the request returns a stream unavailable result
  And the system records the startup failure for diagnostics
```

### SC-CC-07 - Recover from Camera Disconnects and Stream Failures

**User Story**  
As a Shrimp Cam operator, I want camera capture and streaming to recover from disconnects so that temporary device failures do not require a full application restart.

**Dependencies**
- SC-CC-04
- SC-CC-05
- SC-CC-06

**Test Expectations**
- Unit tests cover retry policy decisions, backoff timing, and terminal failure thresholds.
- Integration tests verify a dropped stream or failed capture process is retried when the camera becomes available again.
- Failure tests verify repeated reconnect failures degrade status cleanly without runaway process restarts.

**Acceptance Criteria**

```gherkin
Scenario: Recover after a temporary camera disconnect
  Given the camera disconnects during capture or streaming
  And the camera becomes available again within the configured retry window
  When the reconnect workflow runs
  Then the system re-establishes camera access automatically
  And camera status returns to online without restarting the application

Scenario: Mark camera as degraded after repeated reconnect failures
  Given the camera remains unavailable across the configured retry attempts
  When the reconnect workflow exhausts its retries
  Then the system marks the camera status as degraded
  And the system stops active retry churn until the next scheduled health check or request
```

### SC-CC-08 - Store Captures with Deterministic Naming and Layout

**User Story**  
As a Shrimp Cam operator, I want captures stored in a deterministic file layout so that images are easy to browse, process, and retain across hosts.

**Dependencies**
- None

**Test Expectations**
- Unit tests cover file path generation, timestamp formatting, collision handling, and source-type naming rules.
- Integration tests verify the filesystem layout is created automatically when missing.
- Failure tests verify invalid root paths are rejected before metadata is committed.

**Acceptance Criteria**

```gherkin
Scenario: Save a capture using the standard storage layout
  Given a successful image capture is ready to persist
  When the system stores the capture
  Then the file is saved beneath the configured year/month/day folder structure
  And the filename includes a capture timestamp that is unique within the folder

Scenario: Storage root path is invalid or unavailable
  Given a successful image capture is ready to persist
  And the configured storage root cannot be written
  When the system stores the capture
  Then the capture is marked as failed to persist
  And the system does not record metadata pointing to a missing file
```

### SC-CC-09 - Clean Up Expired Capture Files

**User Story**  
As a Shrimp Cam operator, I want old captures removed according to retention settings so that the device does not run out of disk space.

**Dependencies**
- SC-CC-08

**Test Expectations**
- Unit tests cover retention cutoff calculation, protected file handling, and idempotent cleanup behavior.
- Integration tests verify expired files and metadata are removed together.
- Failure tests verify partial deletion issues are logged and retried without deleting non-expired files.

**Acceptance Criteria**

```gherkin
Scenario: Delete captures older than the retention window
  Given retention cleanup is enabled for a configured number of days
  And stored captures exist both inside and outside the retention window
  When the cleanup job runs
  Then captures older than the cutoff are deleted
  And newer captures remain available

Scenario: A file cannot be deleted during cleanup
  Given a capture is older than the retention cutoff
  And the file cannot be deleted because it is locked or inaccessible
  When the cleanup job runs
  Then the cleanup result records the item as failed
  And the job continues processing the remaining expired captures
```

### SC-CC-10 - Generate Daily Timelapse Videos

**User Story**  
As a Shrimp Cam operator, I want the system to generate a daily timelapse video from captured frames so that I can review each day in a compact format.

**Dependencies**
- SC-CC-05
- SC-CC-08

**Test Expectations**
- Unit tests cover frame selection, ordering, output naming, and duplicate-run behavior.
- Integration tests verify a day of captures can be assembled into a video artifact through the configured media process.
- Failure tests verify days with insufficient frames do not produce misleading success output.

**Acceptance Criteria**

```gherkin
Scenario: Generate a daily timelapse for a completed day
  Given a day contains enough captured frames to build a timelapse
  When the daily timelapse job runs for that day
  Then the system generates one timelapse video artifact for the day
  And the artifact is stored in the configured timelapse output location

Scenario: Skip timelapse generation when too few frames exist
  Given a day does not contain enough captured frames to build a timelapse
  When the daily timelapse job runs for that day
  Then no timelapse video is generated
  And the job records that the day was skipped due to insufficient frames
```

### SC-CC-11 - Capture Motion-Triggered Highlights

**User Story**  
As a Shrimp Cam operator, I want the system to capture motion-triggered highlights so that noteworthy tank activity is preserved without reviewing the full live feed.

**Dependencies**
- SC-CC-06
- SC-CC-08

**Test Expectations**
- Unit tests cover motion threshold rules, cooldown windows, and duplicate suppression.
- Integration tests verify a qualifying motion event produces a highlight capture and metadata record.
- Failure tests verify noisy or repeated detections within the cooldown window do not create capture storms.

**Acceptance Criteria**

```gherkin
Scenario: Create a highlight capture from qualifying motion
  Given motion highlight capture is enabled
  And incoming motion data exceeds the configured threshold
  When the motion evaluation workflow runs
  Then the system stores a highlight capture using the standard storage layout
  And the system records metadata with source type MotionHighlight

Scenario: Suppress repeated highlights during cooldown
  Given motion highlight capture is enabled
  And a highlight was already captured within the configured cooldown window
  When another motion event exceeds the threshold
  Then the system does not create another highlight capture
  And the system records that the event was suppressed by cooldown rules
```

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

### SC-CAM-16 - Coordinate Live Stream And Timelapse Camera Access

**User Story**  
As a shrimp keeper, I want live streaming and scheduled timelapse capture to coordinate access to the webcam so that viewing the live feed does not corrupt or hang camera capture workflows.

**Dependencies**
- SC-CC-05
- SC-CC-06

**Test Expectations**
- Unit tests cover camera resource arbitration, busy results, and release behavior.
- Integration tests cover live stream ownership, manual capture busy responses, scheduled capture degraded state, and one owner at a time.
- API tests verify manual capture returns an actionable busy error.

**Acceptance Criteria**

```gherkin
Scenario: Scheduled capture runs while live view is active
  Given the live stream is active for the configured camera
  And the capture schedule reaches a valid capture window
  When the scheduled capture worker attempts a timelapse frame
  Then the application coordinates camera access without corrupting the stream or the capture
  And the capture result is persisted or an actionable retry/degraded event is recorded

Scenario: Manual capture receives a clear camera busy result
  Given the camera resource is temporarily unavailable
  When a user requests a manual snapshot
  Then the API returns an actionable error instead of hanging
  And the frontend can display that the camera is busy or retrying

Scenario: Camera arbitration is covered by tests
  Given stream, manual capture, and scheduled capture use the same camera source
  When they overlap in unit or integration tests
  Then only one camera process owns the source at a time
  And backend coverage remains at or above 90% line coverage
```

### SC-CAM-17 - Report Manual Capture Health Transitions

**User Story**  
As a remote Shrimp Cam operator, I want manual snapshot attempts to update camera health so that diagnostics and the dashboard reflect current capture failures without waiting for another scheduled job.

**Dependencies**
- SC-CC-04
- SC-ASO-306
- SC-CAM-16

**Test Expectations**
- Unit tests verify successful manual capture reports the camera online.
- Unit tests verify camera busy and unavailable manual capture results report degraded camera health with actionable reasons.
- Existing API and health tests continue to prove diagnostics can surface degraded camera state.

**Acceptance Criteria**

```gherkin
Scenario: Successful manual capture restores online health
  Given the camera command succeeds
  When a manual capture is stored and persisted
  Then the camera health is reported online
  And no degraded reason is recorded for that capture

Scenario: Manual capture reports camera unavailable
  Given the camera command fails
  When a user requests a manual snapshot
  Then the manual capture returns a camera unavailable result
  And the camera health is reported degraded with an actionable reason

Scenario: Manual capture reports camera busy
  Given another workflow owns the camera resource
  When a user requests a manual snapshot
  Then the manual capture returns a camera busy result
  And the camera health is reported degraded without starting another process
```

### SC-CAM-18 - Kill Still-Capture Child Processes On Cancellation And Timeout

**User Story**  
As a Shrimp Cam operator, I want cancelled or timed-out still captures to terminate their child process so that `ffmpeg` cannot keep the Logitech camera busy after the app has given up.

**Dependencies**
- SC-CC-03
- SC-CC-04
- SC-CC-05
- SC-CAM-16

**Test Expectations**
- Integration tests prove the process runner terminates a still-running child process when its cancellation token is cancelled.
- Capture services continue to release camera resource leases when process execution is cancelled.
- Existing manual and scheduled capture tests continue to prove cleanup and health behavior.

**Acceptance Criteria**

```gherkin
Scenario: Cancelled capture process is terminated
  Given a still capture command is running
  When the capture operation is cancelled or times out
  Then the child process is killed if it has not exited
  And the camera resource lease is released
  And the failure is reported as actionable capture status
```

### SC-CAM-19 - Make Scheduled Capture Worker Exception-Resilient

**User Story**  
As a Shrimp Cam operator, I want the scheduled capture worker to survive unexpected iteration failures so that one storage, settings, or camera exception cannot stop timelapse capture permanently.

**Dependencies**
- SC-CC-05
- SC-CAM-18
- SC-ASO-310

**Test Expectations**
- Worker tests cover thrown settings and scheduled capture service exceptions.
- Tests prove a subsequent scheduled iteration can still run after an unexpected failure.
- Host cancellation still stops the worker promptly instead of being swallowed as a retryable failure.

**Acceptance Criteria**

```gherkin
Scenario: Scheduler continues after an unexpected exception
  Given the scheduled capture worker encounters an exception during one iteration
  When the next delay interval elapses
  Then the worker logs the failure
  And it attempts the next scheduled iteration
  And health diagnostics report the degraded condition without terminating the background service
```

### SC-CAM-20 - Define Retry And Catch-Up Semantics For Failed Scheduled Intervals

**User Story**  
As a Shrimp Cam operator, I want failed scheduled intervals to have explicit retry and catch-up semantics so that temporary camera failures do not create duplicate capture storms or unexpected backfill after recovery.

**Dependencies**
- SC-CC-05
- SC-CAM-16
- SC-CAM-19

**Test Expectations**
- Unit tests verify camera command retry attempts happen only within the active scheduled capture run according to configured camera retry settings.
- Unit tests verify a failed interval is persisted as processed and is not retried again while the clock remains in that same interval.
- Unit tests verify the scheduler moves forward to the next due interval after a failed interval and does not backfill the older failed slot.
- Existing worker resilience tests continue to prove unexpected exceptions do not terminate future scheduled iterations.

**Acceptance Criteria**

```gherkin
Scenario: Failed interval is not retried repeatedly
  Given a scheduled interval has already failed and been persisted
  And the clock still falls inside that same interval
  When the scheduled capture service runs again
  Then it waits instead of starting another camera process
  And it does not overwrite scheduled capture state

Scenario: Scheduler moves forward after a failed interval
  Given a scheduled interval failed previously
  And the clock reaches the next due interval
  When the scheduled capture service runs successfully
  Then it captures the current interval
  And it does not backfill the older failed interval
  And it records the current interval as captured
```

### SC-CAM-21 - Define Shared Camera Frame Source Strategy

**User Story**  
As a Shrimp Cam developer, I want Windows and Linux camera integrations to converge behind a shared frame-source contract so that live streaming, scheduled timelapse, manual capture, and diagnostics can use the same frame pipeline while each operating system keeps the best native adapter.

**Implementation Notes**  
The shared contract should expose camera discovery, provider selection, lifecycle, frame delivery, health, restart, and settings-change behavior. Platform-specific implementations should be selected by host OS and settings, but downstream capture and stream code should consume the same frame bus or latest-frame cache.

**Dependencies**
- SC-CC-06
- SC-CAM-16
- SC-CAM-20

**Test Expectations**
- Unit tests verify provider selection chooses Windows Media Foundation on Windows and V4L2/FFmpeg on Linux when configured.
- Unit tests verify live stream and timelapse consumers can read from the shared frame contract without owning the camera device directly.
- Integration tests verify settings changes stop the current provider and start the newly selected provider without requiring an app restart.
- Failure tests verify provider startup failures produce actionable health diagnostics and do not block unrelated API endpoints.

**Acceptance Criteria**

```gherkin
Scenario: Select the Windows frame provider
  Given Shrimp Cam is running on a Windows host
  And the camera backend mode is set to automatic
  When the camera frame source is initialized
  Then the system selects the Windows Media Foundation provider
  And live stream and timelapse workflows consume frames through the shared contract

Scenario: Select the Linux frame provider
  Given Shrimp Cam is running on a Linux host
  And the camera backend mode is set to automatic
  When the camera frame source is initialized
  Then the system selects the Linux V4L2/FFmpeg provider
  And live stream and timelapse workflows consume frames through the shared contract

Scenario: Restart provider after camera settings change
  Given a camera frame provider is running
  When an administrator changes camera source, resolution, frame rate, or backend settings
  Then the current provider is stopped safely
  And the new provider starts with the saved settings
  And active health diagnostics report the transition without requiring application restart
```

### SC-CAM-22 - Implement Windows Media Foundation Frame Source

**User Story**  
As a Shrimp Cam operator running Windows, I want the application to capture webcam frames through Windows Media Foundation so that Logitech USB webcams can stream and feed timelapse capture reliably without depending on an external `ffmpeg.exe` process for primary camera access.

**Implementation Notes**  
The Windows adapter may use a thin native interop layer, a maintained .NET wrapper, or a dedicated isolated adapter process, but the application boundary should expose the shared frame-source contract from `SC-CAM-21`. FFmpeg may remain as a fallback or diagnostic backend, not the preferred Windows path.

**Dependencies**
- SC-CC-02
- SC-CAM-21

**Test Expectations**
- Unit tests verify Media Foundation device descriptors map into the shared camera descriptor model.
- Unit tests verify adapter lifecycle handles start, frame delivery, stop, restart, and disposal idempotently.
- Integration tests with a mocked Media Foundation boundary verify frames are published to the shared frame bus and latest-frame cache.
- Failure tests verify missing devices, busy devices, unsupported formats, and adapter startup failures surface actionable diagnostics.
- Host-marked smoke tests verify a Logitech USB webcam can provide frames on a Windows PC.

**Acceptance Criteria**

```gherkin
Scenario: Start the Windows Media Foundation provider
  Given Shrimp Cam is running on Windows
  And a configured Logitech webcam is available
  When the Windows camera provider starts
  Then it opens the configured Media Foundation device
  And it publishes camera frames through the shared frame-source contract
  And camera health reports the provider as online

Scenario: Keep timelapse evaluation active while viewers connect
  Given the Windows Media Foundation provider is running
  And no user is watching the live stream
  When the scheduled timelapse evaluator reaches a due interval
  Then it can use the latest available frame without starting a second camera owner
  And later live stream viewers receive frames from the same provider

Scenario: Surface unsupported Windows camera format
  Given the configured Windows camera cannot provide a supported frame format
  When the provider attempts to start
  Then startup fails with an actionable unsupported-format diagnostic
  And the application remains available in degraded camera mode
```

### SC-CAM-23 - Implement Linux V4L2/FFmpeg Logitech UVC Adapter

**User Story**  
As a Shrimp Cam operator running Raspberry Pi or Linux with a Logitech USB webcam, I want the application to capture frames through a V4L2/FFmpeg adapter so that UVC webcams work reliably on headless Linux deployments while sharing the same downstream live stream and timelapse pipeline.

**Implementation Notes**  
The Linux adapter should target V4L2 device paths such as `/dev/video0` and use FFmpeg as the adapter process for UVC Logitech webcams unless a later native V4L2 frame reader is added. The adapter must never assume Raspberry Pi camera-module behavior for the Logitech webcam path.

**Dependencies**
- SC-CC-01
- SC-CAM-21

**Test Expectations**
- Unit tests verify V4L2/FFmpeg command generation for device path, input format, resolution, frame rate, MJPEG output, and quoting.
- Integration tests with a mocked process runner verify stdout frames are parsed and published to the shared frame-source contract.
- Failure tests verify missing `/dev/video*` devices, process exit, unsupported resolution, and permission failures degrade camera health cleanly.
- Host-marked smoke tests verify a Logitech UVC webcam provides frames on Raspberry Pi OS Lite or Linux.

**Acceptance Criteria**

```gherkin
Scenario: Start the Linux V4L2/FFmpeg provider
  Given Shrimp Cam is running on Linux
  And a Logitech UVC webcam is configured at a valid V4L2 device path
  When the Linux camera provider starts
  Then it launches the FFmpeg V4L2 adapter with the configured device, resolution, and frame rate
  And it publishes frames through the shared frame-source contract
  And camera health reports the provider as online

Scenario: Recover after Linux adapter process exits
  Given the Linux V4L2/FFmpeg provider is running
  When the FFmpeg adapter process exits unexpectedly
  Then the provider marks camera health degraded with the exit reason
  And it follows the configured reconnect policy without spawning duplicate camera owners

Scenario: Preserve storage safety during Linux camera failure
  Given the Linux camera provider cannot read from the configured V4L2 device
  When a scheduled timelapse interval becomes due
  Then no empty or corrupt capture file is persisted
  And the failed interval is recorded according to scheduled retry semantics
```

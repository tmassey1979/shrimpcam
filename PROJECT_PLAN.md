# Shrimp Cam Project Plan

## Goal

Build a system that can run on either a Raspberry Pi or a Windows PC and:

- captures scheduled timelapse images of a shrimp tank
- provides a live video feed from a Logitech webcam
- stores images and metadata locally
- exposes a C# backend API
- serves a React-based PWA for viewing timelapse images and the live feed

## Product Scope

### Core features

1. Live camera preview in the web app
2. Timelapse image capture on a schedule
3. Browse captured images by date and time
4. PWA access from phones, tablets, and desktops on the local network
5. Raspberry Pi or Windows deployment with automatic startup

### Nice-to-have features

1. Snapshot button for manual capture
2. Retention policy for old images
3. Daily timelapse video generation from captured frames
4. Health page for disk usage, camera status, and uptime
5. Basic authentication for remote access
6. Motion-triggered highlight capture

## Recommended Architecture

### Backend

- **Runtime:** .NET 8 ASP.NET Core
- **App type:** single backend app serving both API and built frontend assets
- **Camera integration:** Linux webcam access through `ffmpeg` and/or `v4l2`-compatible tooling invoked by the backend
- **Background work:** `BackgroundService` workers for timelapse scheduling, cleanup, and health checks
- **Storage:** local filesystem for images, SQLite for metadata/config
- **Code style:** clean architecture with small focused services, explicit boundaries, and testable abstractions

### Frontend

- **Framework:** React + TypeScript + Vite
- **PWA:** installable app with offline shell for UI chrome and cached recent metadata
- **UI areas:**
  - dashboard
  - live view
  - gallery
  - settings/status

### Deployment

- **Primary hosts:** Raspberry Pi 4/5 and Windows PC
- **OS targets:** Raspberry Pi OS 64-bit and Windows 10/11
- **Startup:** `systemd` on Linux, Windows Service or scheduled startup task on Windows
- **Reverse proxy:** optional `nginx` on Linux or IIS/nginx on Windows, but not required for first version

## Suggested Solution Layout

```text
shrimpcam/
  src/
    ShrimpCam.Api/
    ShrimpCam.Core/
    ShrimpCam.Infrastructure/
    ShrimpCam.Web/
  tests/
    ShrimpCam.Core.Tests/
    ShrimpCam.Infrastructure.Tests/
    ShrimpCam.Api.Tests/
  docs/
  scripts/
  deploy/
```

### Project responsibilities

- `ShrimpCam.Api`
  - HTTP API
  - static file hosting for the PWA
  - app startup and dependency wiring

- `ShrimpCam.Core`
  - domain models
  - scheduling rules
  - service interfaces
  - business logic

- `ShrimpCam.Infrastructure`
  - SQLite access
  - filesystem image storage
  - camera process integration
  - background job implementations

- `ShrimpCam.Web`
  - React PWA

- test projects
  - unit, integration, and API tests

## Engineering Standards

### Clean code expectations

- prefer small classes with one clear responsibility
- keep business rules in `ShrimpCam.Core`
- keep infrastructure concerns out of domain logic
- depend on abstractions at boundaries
- avoid static service state and tightly coupled helpers
- use meaningful names over comments where possible
- keep methods short and intention-revealing
- fail fast on invalid configuration and invalid state

### Architecture guardrails

- `ShrimpCam.Core` must not depend on infrastructure or ASP.NET Core
- `ShrimpCam.Api` should coordinate requests, not hold business logic
- camera, storage, and persistence integrations must be behind interfaces
- background workers should delegate real logic into testable services
- configuration models should be strongly typed and validated

### Definition of done

Work is not done unless it includes:

1. tests written first or alongside the implementation in TDD style
2. passing automated test suite
3. passing formatting and analyzer checks
4. no unresolved warnings in changed code
5. coverage maintained at or above the agreed threshold

## Camera Strategy

### Cross-platform camera support

The backend should treat camera access as platform-specific infrastructure behind shared interfaces:

- **Linux/Raspberry Pi:** USB webcam via `ffmpeg` + `/dev/video0` using Video4Linux2
- **Windows PC:** USB webcam via `ffmpeg` using DirectShow device input

Recommendation:

1. Keep camera orchestration in a platform-neutral service contract
2. Provide separate Linux and Windows implementations or command builders
3. Detect the current OS at runtime and activate the correct camera integration

This keeps the application logic shared while allowing host-specific capture commands.

### Live feed

Use the Logitech webcam as a USB UVC camera on the target host.

Recommended first implementation:

1. Use `ffmpeg` to read from the host camera device
2. On Linux, use `/dev/video0`
3. On Windows, use a DirectShow device name such as `"video=Logitech BRIO"`
2. Produce an MJPEG stream for simplest browser compatibility on the local network
3. Expose the stream from ASP.NET Core at `/live/mjpeg`

Why MJPEG first:

- low implementation complexity
- easy to display in browsers with a normal `<img>` tag
- good enough for a local-network monitoring app

Possible later upgrade:

- HLS for lower bandwidth and better scaling
- WebRTC for lower latency if interactive viewing becomes important

### Timelapse capture

Recommended capture approach:

1. Use a scheduled background worker
2. Invoke `ffmpeg` for a single-frame image capture
3. Save image to a date-based folder structure
4. Write capture metadata to SQLite

Platform note:

- Linux capture commands will target Video4Linux2 devices
- Windows capture commands will target DirectShow devices

Example storage layout:

```text
/data/shrimpcam/images/2026/06/24/20260624-213000.jpg
```

### Camera abstraction

Hide camera access behind an interface so we can swap implementations later:

```csharp
public interface ICameraService
{
    Task<Stream> GetLiveMjpegStreamAsync(CancellationToken cancellationToken);
    Task<CapturedImage> CaptureStillAsync(CancellationToken cancellationToken);
    Task<CameraStatus> GetStatusAsync(CancellationToken cancellationToken);
}
```

For the first version, the live stream may be handled by a dedicated endpoint/service rather than returning a .NET-managed stream object for long-lived sessions. The important part is keeping camera concerns isolated from the rest of the app.

Suggested implementation split:

- `ICameraService`
- `LinuxCameraService`
- `WindowsCameraService`
- shared `FfmpegCommandBuilder` abstractions as needed

## Data Model

### SQLite tables

#### `captures`

- `id`
- `captured_at_utc`
- `file_path`
- `width`
- `height`
- `file_size_bytes`
- `source_name`
- `notes`

#### `app_settings`

- `key`
- `value`

#### `health_events`

- `id`
- `occurred_at_utc`
- `level`
- `message`

## API Plan

### Public endpoints

- `GET /api/health`
  - app, disk, and camera status

- `GET /api/live/status`
  - live stream availability and current camera info

- `GET /live/mjpeg`
  - MJPEG live feed

- `POST /api/captures/manual`
  - trigger a manual still capture

- `GET /api/captures`
  - paged capture list with filters by date

- `GET /api/captures/{id}`
  - metadata for one capture

- `GET /api/captures/{id}/file`
  - image file response

- `GET /api/settings`
  - current app settings

- `PUT /api/settings`
  - update schedule, retention, and camera preferences

## Frontend Plan

### Screens

#### Dashboard

- camera online/offline
- latest snapshot
- next scheduled capture
- storage usage

#### Live View

- embedded live feed
- manual snapshot button
- last capture timestamp

#### Gallery

- reverse chronological image list
- day filter
- full-screen image viewer

#### Settings

- capture interval
- active capture hours
- retention days
- camera resolution/FPS options

### PWA behavior

- installable on mobile and desktop
- cached app shell
- cached recent gallery metadata
- graceful offline message when the Pi is unavailable

## Operational Plan

## Testing Strategy

### TDD policy

The project should follow TDD by default:

1. write a failing test
2. implement the smallest change that makes it pass
3. refactor while keeping tests green

This is especially important for:

- scheduling logic
- retention policy logic
- capture metadata handling
- camera command building
- API request/response behavior
- health/status calculations

### Test layers

#### Unit tests

- core business rules
- scheduling calculations
- configuration validation
- command generation for Linux and Windows camera operations
- retention and storage path logic

#### Integration tests

- SQLite repositories
- filesystem storage services
- end-to-end capture workflow with mocked camera process execution

#### API tests

- health endpoints
- settings endpoints
- capture endpoints
- gallery filtering and paging

### Coverage target

Target at least **90% line coverage** for backend production code.

Practical guidance:

- require 90%+ coverage for `ShrimpCam.Core`
- aim for 90%+ overall backend coverage across `Api`, `Core`, and `Infrastructure`
- allow thin host/bootstrap files to be excluded only when justified
- do not use coverage exclusions to hide untested business logic

### Coverage enforcement

Add CI/local checks that:

1. run all backend tests
2. collect coverage
3. fail the build if coverage drops below threshold

Recommended tools:

- `xUnit` for tests
- `FluentAssertions` for readable assertions
- `coverlet` for coverage collection
- `ReportGenerator` for readable coverage reports

### Testing design rules

- avoid logic hidden inside controllers, Program setup, or hosted service loops
- isolate time behind a clock abstraction
- isolate process execution behind an interface
- isolate filesystem access behind abstractions where behavior matters
- make camera integrations testable by validating generated commands and process outcomes
- keep end-to-end hardware tests separate from repeatable automated tests

### Hardware test approach

Because webcam access is environment-specific, use two test categories:

1. automated tests that run without real hardware
2. optional manual or host-marked smoke tests for real webcam verification on Raspberry Pi and Windows

### Configuration

Store config in `appsettings.json` plus database-backed mutable settings.

Suggested settings:

- image root path
- capture interval minutes
- active schedule window
- webcam device path or device name
- camera input backend
- stream resolution
- capture resolution
- retention days

### Logging

- use ASP.NET Core structured logging
- write rolling log files locally
- include camera start/stop, capture success/failure, and cleanup events

### Startup and resilience

- run under `systemd` on Linux or Windows Service on Windows
- restart on failure
- validate camera presence at startup
- degrade gracefully if the camera disconnects

## Risks And Tradeoffs

### 1. Live streaming approach

MJPEG is easiest, but uses more bandwidth than HLS/WebRTC.

Recommendation:

- start with MJPEG
- upgrade only if performance becomes a real issue

### 2. Process-based camera integration

Calling `ffmpeg` is pragmatic and Pi-friendly, but adds an external dependency.

Recommendation:

- use `ffmpeg` first for speed of delivery
- keep camera logic abstracted so we can replace it later

### 3. Storage growth

Timelapse images will accumulate quickly.

Recommendation:

- implement retention early
- optionally add daily timelapse compilation and raw-image pruning later

### 4. Raspberry Pi performance

Pi hardware is capable, but simultaneous streaming, capture, and UI serving can compete for CPU and disk. Windows PCs will typically have more headroom, but device enumeration and webcam driver differences add their own variability.

Recommendation:

- target Pi 4/5
- keep first release simple
- test with real capture intervals and real webcam resolutions on both Linux and Windows

### 5. Cross-platform device differences

Camera naming, supported resolutions, and driver behavior can differ between Raspberry Pi and Windows.

Recommendation:

- add a camera discovery/status feature early
- keep device configuration editable
- test against the specific Logitech webcam model you plan to use

### 6. Coverage target cost

Requiring 90%+ coverage and TDD will improve maintainability, but it adds delivery overhead and forces more design discipline up front.

Recommendation:

- accept slightly slower early implementation in exchange for a cleaner long-term codebase
- keep infrastructure thin so most logic stays easy to unit test
- review coverage quality, not just the percentage

## Delivery Roadmap

### Phase 1: Foundation

1. Create solution and projects
2. Add SQLite and filesystem abstractions
3. Add OS-aware camera abstraction
4. Add health endpoint
5. Add React PWA shell
6. Add test projects, analyzers, and coverage enforcement

### Phase 2: Camera capture

1. Integrate still image capture via `ffmpeg`
2. Support Linux and Windows capture command paths
3. Save images and metadata
4. Add gallery API and UI
5. Add manual capture button

### Phase 3: Live stream

1. Add MJPEG streaming endpoint
2. Build live-view page
3. Add camera status and reconnect handling

### Phase 4: Operations

1. Add editable settings
2. Add retention cleanup worker
3. Add `systemd` deployment files
4. Add installation/setup documentation

### Phase 5: Polish

1. Improve mobile UX
2. Add authentication if needed
3. Add daily timelapse video generation
4. Add alerts/notifications if desired

## First Build Slice

The fastest useful first milestone is:

1. ASP.NET Core app starts successfully on Raspberry Pi and Windows
2. React PWA loads from the same app
3. Manual still capture works with the Logitech webcam on at least one host
4. Camera abstraction supports adding the second host cleanly
5. Captured images appear in a gallery page
6. Tests and coverage reporting are already active

This gives us a working vertical slice before adding full scheduled capture and live video, without locking the backend to a single OS.

## Recommended Next Tasks

1. Scaffold the .NET solution and React app structure
2. Add analyzers, test projects, and 90% coverage gates
3. Implement OS-aware camera configuration and health checks
4. Build manual capture end to end
5. Add scheduled timelapse capture
6. Add live MJPEG feed

## Open Decisions

These are the only decisions that meaningfully affect implementation direction:

1. Should the app be local-network only, or do you want remote internet access later?
2. Do you want authentication in version 1, or can the first release stay trusted/local?
3. Do you want the backend to serve the PWA, or do you prefer separate frontend/backend processes during development?

My recommendation:

- local-network first
- no auth in v1
- single deployed backend serving the built PWA
- design camera integration for Raspberry Pi and Windows from day one
- enforce TDD and coverage gates from the first commit

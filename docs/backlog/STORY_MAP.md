# Shrimp Cam Story Map

This story map turns the four epic backlogs into an execution order for the production-ready first release.

## Release waves

### Wave 0: Foundation blockers

These stories should land before most feature work begins:

- `SC-PF-001` Scaffold the solution structure
- `SC-PF-002` Enforce clean architecture boundaries
- `SC-PF-003` Establish shared abstractions
- `SC-PF-004` Validate configuration at startup
- `SC-PF-005` Add analyzer baseline
- `SC-PF-006` Standardize formatting commands
- `SC-PF-007` Treat warnings as errors
- `SC-PF-008` Enable TDD-oriented test workflow
- `SC-PF-009` Enforce coverage thresholds
- `SC-PF-011` Provide CI and local quality commands

### Wave 1: Security and platform bootstrapping

- `SC-PF-012` Publish backend OpenAPI docs with Swashbuckle
- `SC-ASO-301` SQLite schema and repository foundation
- `SC-ASO-302` Local account authentication
- `SC-ASO-303` Bootstrap administrator flow
- `SC-ASO-304` Authorization roles and policy enforcement
- `SC-ASO-305` Session or token lifecycle management
- `SC-ASO-306` Health and readiness endpoint
- `SC-PWA-01` App shell and mobile routing
- `SC-PWA-02` Sign-in and session experience

### Wave 2: Core capture and product flows

- `SC-CC-01` Discover cameras on Linux hosts
- `SC-CC-02` Discover cameras on Windows hosts
- `SC-CC-03` Generate OS-specific camera commands
- `SC-CC-08` Store captures with deterministic naming and layout
- `SC-CC-04` Capture a manual snapshot
- `SC-CC-05` Run scheduled timelapse capture
- `SC-ASO-307` Settings management API
- `SC-ASO-308` Capture browsing APIs
- `SC-PWA-03` Dashboard overview
- `SC-PWA-04` Live view and manual snapshot UX
- `SC-PWA-05` Gallery browsing and full-screen viewer
- `SC-PWA-06` Settings and system status experience

### Wave 3: Resilience and production hardening

- `SC-CC-06` Stream live MJPEG video
- `SC-CC-07` Recover from camera disconnects and stream failures
- `SC-CC-09` Clean up expired capture files
- `SC-CAM-16` Coordinate live stream and timelapse camera access
- `SC-CAM-17` Report manual capture health transitions
- `SC-ASO-309` Audit and security event logging
- `SC-ASO-310` Degraded startup and runtime resilience
- `SC-ASO-311` Structured application logs
- `SC-ASO-312` Diagnostics and support bundle API
- `SC-ASO-315` Deployment support for Windows service and `systemd`
- `SC-ASO-316` External-hosting hardening baseline
- `SC-ASO-317` Startup default administrator initialization
- `SC-ASO-318` Protect camera operation endpoints
- `SC-ASO-319` Redact authentication secrets from audit logs
- `SC-PWA-07` Offline shell and cached core experience
- `SC-PWA-08` Installable PWA behavior
- `SC-PWA-09` Loading, error, and reconnect states
- `SC-PWA-10` Accessibility and touch usability

### Wave 3B: Reference-led UI cleanup

- `SC-PWA-11` Reference-led aquarium visual system
- `SC-PWA-12` Dashboard reference cleanup
- `SC-PWA-13` Live view reference cleanup
- `SC-PWA-14` Gallery timeline reference cleanup
- `SC-PWA-15` Settings reference cleanup

### Wave 4: Backup, restore, and advanced media

- `SC-ASO-313` Backup and export workflow
- `SC-ASO-314` Restore and import workflow
- `SC-CC-10` Generate daily timelapse videos
- `SC-CC-11` Capture motion-triggered highlights
- `SC-PF-010` Publish build and version metadata

## Priority guidance

- `P0`: `SC-PF-001` through `SC-PF-009`, `SC-PF-011`, `SC-PF-012`, `SC-ASO-301` through `SC-ASO-319`, `SC-CC-01` through `SC-CC-09`, `SC-CAM-16`, `SC-CAM-17`, `SC-PWA-01` through `SC-PWA-15`
- `P1`: `SC-ASO-313`, `SC-ASO-314`, `SC-CC-10`
- `P2`: `SC-CC-11`, `SC-PF-010`

## Cross-epic dependencies that need coordination

- PWA auth and protected routes depend on `SC-ASO-302` through `SC-ASO-305`
- Dashboard, live view, gallery, and settings screens depend on `SC-ASO-306` through `SC-ASO-308`
- Manual capture, scheduled capture, and live view depend on `SC-CC-03` and `SC-CC-08`
- degraded mode, reconnect UX, and diagnostics depend on `SC-CC-07`, `SC-ASO-306`, `SC-ASO-310`, and `SC-PWA-09`
- reference-led UI cleanup depends on the approved render assets in `docs/Generated image 1.png` through `docs/Generated image 4.png`
- production release sign-off depends on `SC-ASO-315`, `SC-ASO-316`, and the `P0` test suite

## Release completion bar

The production-ready release is complete when:

1. every `P0` story is accepted
2. backend line coverage remains at or above 90%
3. Raspberry Pi and Windows smoke tests pass for the supported camera flow
4. internet-exposed security safeguards are enabled and verified
5. the PWA is installable and usable on a Samsung-class phone viewport

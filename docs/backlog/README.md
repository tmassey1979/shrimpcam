# Shrimp Cam Production Backlog

This backlog defines the production-ready story set for `Shrimp Cam`.

## Scope

- internet-exposed first release
- single-host ASP.NET Core deployment serving the React PWA
- Raspberry Pi and Windows as first-class runtime targets
- local account authentication
- clean architecture, TDD, and at least 90% backend line coverage

## Workstreams

1. [Epic 1 - Platform Foundation and Quality Gates](./EPIC-1-platform-foundation.md)
2. [Epic 2 - Camera, Capture, and Streaming](./EPIC-2-camera-capture-streaming.md)
3. [Epic 3 - API, Security, and Operations](./EPIC-3-api-security-operations.md)
4. [Epic 4 - PWA Product Experience](./EPIC-4-pwa-product-experience.md)
5. [Execution Plan](./EXECUTION_PLAN.md)

## Story conventions

Every story must include:

- a unique story ID
- one user story sentence in `As a / I want / so that` form
- dependency tags
- test expectations
- Gherkin acceptance criteria
- non-happy-path behavior where relevant

## Completion rules

A story is only complete when:

1. its acceptance criteria pass
2. TDD evidence exists in the related test commit history or change set
3. automated tests pass
4. changed backend code keeps overall backend line coverage at or above 90%
5. analyzers and formatting checks pass

## Priority model

- `P0`: blocks external release or security/compliance posture
- `P1`: core product capability required for production value
- `P2`: production enhancement included in release scope

## Dependency notation

- `Depends on:` required predecessor stories
- `Enables:` downstream stories materially unblocked by this story

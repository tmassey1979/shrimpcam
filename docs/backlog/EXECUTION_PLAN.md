# Shrimp Cam 4-Agent Execution Plan

## Objective

Complete the production story backlog for `Shrimp Cam` with four coordinated workstreams while maintaining TDD discipline and at least 90% backend line coverage.

## Agent ownership

### Agent A: Platform Foundation

- epic owner: `EPIC-1-platform-foundation`
- focus: solution scaffolding, architecture guardrails, analyzers, CI, coverage, shared abstractions

### Agent B: Camera and Capture

- epic owner: `EPIC-2-camera-capture-streaming`
- focus: device discovery, capture workflow, scheduling, streaming, retention, derived media generation

### Agent C: API, Security, and Operations

- epic owner: `EPIC-3-api-security-operations`
- focus: persistence, auth, authorization, API surface, diagnostics, hardening, deployment

### Agent D: PWA Product Experience

- epic owner: `EPIC-4-pwa-product-experience`
- focus: sign-in UX, dashboard, live view, gallery, settings, offline/install, responsive behavior

## Dependency strategy

### Foundation-first blockers

These should be completed before most downstream work:

- platform scaffolding
- architecture boundaries
- test projects and coverage gates
- configuration validation framework
- shared process, clock, and filesystem abstractions

### Parallelizable work after foundation

- camera abstractions can proceed in parallel with auth and API persistence
- PWA shell and routing can proceed in parallel with backend auth and capture APIs
- deployment hardening can proceed once auth, configuration, and runtime diagnostics are stable

## Execution rules

1. Agents own their workstream files and features unless a dependency handoff is explicit.
2. Shared interfaces must be agreed in the main thread before parallel implementation touches both sides.
3. No story is accepted without tests matching the listed expectations.
4. Backend changes that drop coverage below 90% are not mergeable.
5. Cross-platform behavior must be verified for both Raspberry Pi and Windows before the related epic closes.

## Exit criteria

- all `P0` and `P1` stories completed
- all `P2` stories in scope completed
- backend test suite passing with at least 90% line coverage
- deployment guidance verified for Raspberry Pi and Windows
- production release has no unresolved architecture, auth, or camera-operability decisions

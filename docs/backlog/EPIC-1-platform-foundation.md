# Epic 1: Platform Foundation and Quality Gates

## Epic Summary

Establish the production-ready foundation for Shrimp Cam by scaffolding the solution, enforcing clean architectural boundaries, and making code quality gates non-optional from the first implementation slice. This epic creates the baseline developer experience and CI behavior needed for safe feature delivery across Raspberry Pi and Windows targets.

## Story Overview

| ID | Title | Primary Outcome | Depends On |
| --- | --- | --- | --- |
| SC-PF-001 | Scaffold the solution structure | Create the initial solution, project layout, and references | None |
| SC-PF-002 | Enforce clean architecture boundaries | Prevent invalid project dependencies and misplaced business logic | SC-PF-001 |
| SC-PF-003 | Establish shared abstractions | Define reusable cross-cutting interfaces and primitives for testable services | SC-PF-001, SC-PF-002 |
| SC-PF-004 | Validate configuration at startup | Fail fast on invalid platform and application configuration | SC-PF-001, SC-PF-003 |
| SC-PF-005 | Add analyzer baseline | Apply analyzers and static code quality rules across the solution | SC-PF-001 |
| SC-PF-006 | Standardize formatting commands | Make formatting deterministic locally and in automation | SC-PF-001, SC-PF-005 |
| SC-PF-007 | Treat warnings as errors | Prevent warning debt from entering the codebase | SC-PF-005 |
| SC-PF-008 | Enable TDD-oriented test workflow | Stand up test projects, test categories, and repeatable test commands | SC-PF-001, SC-PF-003 |
| SC-PF-009 | Enforce coverage thresholds | Fail builds when backend coverage drops below the agreed minimum | SC-PF-008 |
| SC-PF-010 | Publish build and version metadata | Produce deterministic build identity and runtime version visibility | SC-PF-001 |
| SC-PF-011 | Provide CI and local quality commands | Align developer and CI execution paths for build verification | SC-PF-004, SC-PF-006, SC-PF-007, SC-PF-008, SC-PF-009, SC-PF-010 |

---

## SC-PF-001 - Scaffold the solution structure

**User Story**  
As a developer, I want the Shrimp Cam solution and project structure scaffolded consistently so that new features can be implemented in the intended layers without setup ambiguity.

**Dependencies**  
None

**Test Expectations**  
Verify the solution contains the planned `src`, `tests`, `docs`, `scripts`, and `deploy` structure; verify all projects restore and build in a clean environment; verify project references align with the intended dependency direction.

**Acceptance Criteria**

```gherkin
Scenario: Create the baseline solution layout
  Given a clean checkout of the repository
  When the developer restores and builds the solution
  Then the solution includes the planned application and test projects
  And every project loads successfully
  And the build completes without missing-project or missing-reference errors

Scenario: Reject incomplete scaffolding
  Given the solution is missing a required project or folder from the agreed layout
  When the developer runs the standard build command
  Then the build or validation step fails
  And the output identifies the missing scaffold element
```

## SC-PF-002 - Enforce clean architecture boundaries

**User Story**  
As an architect, I want project dependencies and code placement constrained so that domain logic remains isolated from infrastructure and host concerns.

**Dependencies**  
SC-PF-001

**Test Expectations**  
Verify `ShrimpCam.Core` has no dependency on ASP.NET Core or infrastructure packages; verify `Api` references orchestration layers only; verify automated boundary checks fail on invalid cross-layer references.

**Acceptance Criteria**

```gherkin
Scenario: Allow only approved layer dependencies
  Given the solution uses the agreed Core, Infrastructure, and Api projects
  When architecture validation runs
  Then Core depends only on shared .NET and approved abstraction packages
  And Api can reference Core and Infrastructure through approved composition boundaries
  And invalid reverse dependencies are not present

Scenario: Block an invalid infrastructure dependency in Core
  Given a developer adds an infrastructure or ASP.NET Core reference to ShrimpCam.Core
  When architecture validation runs
  Then the validation fails
  And the failure identifies the violating project or namespace dependency
```

## SC-PF-003 - Establish shared abstractions

**User Story**  
As a developer, I want shared abstractions for time, process execution, filesystem access, and camera integration so that application logic stays testable and platform-neutral.

**Dependencies**  
SC-PF-001, SC-PF-002

**Test Expectations**  
Verify shared interfaces and result models exist in approved layers; verify core services can be tested against fakes or mocks without touching hardware, filesystem, or OS-specific processes; verify abstractions are documented by intended usage.

**Acceptance Criteria**

```gherkin
Scenario: Use shared abstractions in business logic
  Given a service that needs time, filesystem, or process behavior
  When the service is implemented
  Then it depends on shared interfaces instead of concrete OS or framework types
  And the service can be exercised in an automated test with test doubles

Scenario: Prevent direct platform calls in testable logic
  Given a developer calls platform-specific APIs directly from business logic
  When code review or automated validation runs
  Then the change is rejected
  And the implementation is redirected behind the approved abstraction
```

## SC-PF-004 - Validate configuration at startup

**User Story**  
As an operator, I want strongly typed configuration validated during startup so that invalid camera, storage, or scheduling settings fail fast before the app begins running.

**Dependencies**  
SC-PF-001, SC-PF-003

**Test Expectations**  
Verify configuration models bind from configuration sources; verify required values, ranges, and enum-like options are validated; verify startup fails with actionable messages when configuration is invalid.

**Acceptance Criteria**

```gherkin
Scenario: Start successfully with valid configuration
  Given the application configuration contains valid storage, capture, and camera settings
  When the application starts
  Then the configuration binds to strongly typed options
  And validation passes before hosted services begin work

Scenario: Fail fast on invalid configuration
  Given the application configuration contains a missing or invalid required setting
  When the application starts
  Then startup fails before the app begins serving requests or background work
  And the error message identifies the invalid configuration area
```

## SC-PF-005 - Add analyzer baseline

**User Story**  
As a maintainer, I want analyzers enabled across the solution so that common correctness, design, and code-quality issues are detected automatically.

**Dependencies**  
SC-PF-001

**Test Expectations**  
Verify analyzer packages or built-in analyzers are enabled for production and test projects; verify analysis runs in local build and CI; verify the ruleset or editor configuration is committed and shared.

**Acceptance Criteria**

```gherkin
Scenario: Run analyzers during the standard build
  Given a developer builds the solution locally or in CI
  When compilation completes
  Then analyzer rules run automatically across the solution
  And analyzer findings are reported in the build output

Scenario: Surface an analyzer violation
  Given code is introduced that violates an enabled analyzer rule
  When the build runs
  Then the violation appears in the output with rule context
  And the developer can identify the affected file or project
```

## SC-PF-006 - Standardize formatting commands

**User Story**  
As a contributor, I want deterministic formatting rules and commands so that code style stays consistent across contributors and automation.

**Dependencies**  
SC-PF-001, SC-PF-005

**Test Expectations**  
Verify formatting configuration exists for the solution; verify local formatting commands can both check and apply formatting; verify CI can run a no-change formatting validation mode.

**Acceptance Criteria**

```gherkin
Scenario: Check formatting without modifying files
  Given source files in the repository
  When the formatting validation command runs in check mode
  Then the command verifies formatting against the shared rules
  And the command succeeds when no formatting changes are required

Scenario: Detect formatting drift
  Given a source file that does not match the shared formatting rules
  When the formatting validation command runs
  Then the command fails
  And the output identifies that formatting corrections are required
```

## SC-PF-007 - Treat warnings as errors

**User Story**  
As a team lead, I want compiler and configured analyzer warnings treated as errors so that warning debt does not accumulate in active development.

**Dependencies**  
SC-PF-005

**Test Expectations**  
Verify warnings-as-errors is enabled for production code and agreed test scopes; verify allowed exceptions are explicit and minimal; verify builds fail on newly introduced warnings.

**Acceptance Criteria**

```gherkin
Scenario: Block builds with warnings
  Given a developer introduces a compiler or configured analyzer warning
  When the standard build command runs
  Then the build fails
  And the warning is surfaced as an error in the output

Scenario: Allow explicit and approved exceptions only
  Given a warning is intentionally suppressed for a justified case
  When the build and review checks run
  Then the suppression is limited to the approved scope
  And the build still fails for unrelated warnings
```

## SC-PF-008 - Enable TDD-oriented test workflow

**User Story**  
As a developer, I want test projects and repeatable test workflows in place so that features can be delivered using TDD from the first implementation slice.

**Dependencies**  
SC-PF-001, SC-PF-003

**Test Expectations**  
Verify unit, integration, and API test projects exist; verify shared test dependencies and conventions are set up; verify local commands support running targeted and full backend test suites; verify hardware-dependent tests are separable from automated CI coverage.

**Acceptance Criteria**

```gherkin
Scenario: Run the automated backend test suite
  Given the solution contains the standard backend test projects
  When the developer runs the documented test command
  Then unit, integration, and API tests execute successfully in automation-safe mode
  And hardware-specific smoke tests are excluded by default

Scenario: Isolate hardware-dependent tests
  Given a test requires a real webcam or host-specific dependency
  When the default CI test command runs
  Then the hardware-dependent test is not executed automatically
  And the suite still reports how to run that test intentionally
```

## SC-PF-009 - Enforce coverage thresholds

**User Story**  
As a maintainer, I want backend coverage collected and enforced so that core behavior remains well-tested as the codebase grows.

**Dependencies**  
SC-PF-008

**Test Expectations**  
Verify coverage is collected for backend test runs; verify `ShrimpCam.Core` is held to at least 90% line coverage and overall backend coverage to the agreed threshold; verify build failure occurs when thresholds are missed; verify reports are readable locally and in CI artifacts.

**Acceptance Criteria**

```gherkin
Scenario: Pass with acceptable coverage
  Given the backend test suite completes successfully
  When coverage enforcement runs
  Then coverage reports are produced
  And the build succeeds when module and overall thresholds are met

Scenario: Fail when coverage drops below threshold
  Given a change reduces enforced coverage below the agreed threshold
  When coverage enforcement runs
  Then the build fails
  And the output identifies the threshold that was not met
```

## SC-PF-010 - Publish build and version metadata

**User Story**  
As an operator, I want deterministic build and version metadata available in the application so that deployed binaries can be traced to a source revision and build output.

**Dependencies**  
SC-PF-001

**Test Expectations**  
Verify builds produce consistent version metadata from the configured source of truth; verify runtime surfaces application version/build information in an agreed endpoint, log, or startup output; verify local and CI builds stamp metadata consistently.

**Acceptance Criteria**

```gherkin
Scenario: Stamp build metadata consistently
  Given the application is built from a known source revision
  When the build completes
  Then the produced artifacts include the configured version and build metadata
  And the metadata format is consistent between local and CI builds

Scenario: Handle missing build metadata source
  Given the expected version metadata input is missing or invalid
  When the build or startup validation runs
  Then the process fails or falls back according to the agreed rule
  And the outcome is explicit in the build or runtime output
```

## SC-PF-011 - Provide CI and local quality commands

**User Story**  
As a contributor, I want a documented and scriptable set of local and CI quality commands so that I can validate the same gates before pushing changes.

**Dependencies**  
SC-PF-004, SC-PF-006, SC-PF-007, SC-PF-008, SC-PF-009, SC-PF-010

**Test Expectations**  
Verify a single documented command set covers restore, build, format-check, test, and coverage verification; verify CI uses the same or equivalent commands; verify failures are attributable to a specific quality gate.

**Acceptance Criteria**

```gherkin
Scenario: Run the full local quality workflow
  Given a developer is working on the repository
  When the developer runs the documented quality command sequence
  Then restore, build, analyzer, formatting, test, and coverage checks run in the expected order
  And the workflow succeeds when all gates pass

Scenario: Fail with actionable gate output
  Given one quality gate fails during local or CI execution
  When the workflow stops
  Then the failing gate is clearly identified
  And the developer can determine the next corrective action from the output
```

## Exit Criteria for the Epic

- The Shrimp Cam solution can be restored, built, formatted, tested, and coverage-checked through a repeatable local and CI workflow.
- Architectural boundaries are enforced automatically rather than informally.
- Invalid configuration, warnings, formatting drift, or coverage regressions cause fast feedback and failed verification.
- The team can begin feature delivery on a stable TDD-first platform foundation.

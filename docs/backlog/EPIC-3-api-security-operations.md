# Epic 3: API, Security, and Operations

## Epic Summary

Epic 3 establishes the production backbone for Shrimp Cam's backend: durable SQLite persistence, authenticated and authorized API access, observable runtime behavior, resilient startup, operable deployment on Windows and Linux, and recovery workflows for hosted systems. The goal is to make the API safe to expose beyond a single trusted machine while keeping local-first deployment practical for Raspberry Pi and Windows hosts.

## Story Backlog

### SC-ASO-301 - SQLite schema and repository foundation

**User story**  
As a backend developer, I want a versioned SQLite schema and repository layer so that application data can be stored and retrieved consistently across hosts.

**Dependencies**  
None.

**Test expectations**  
Migration integration tests cover initial database creation and upgrade from the prior schema version. Repository tests cover CRUD behavior for users, roles, settings, captures, sessions, and audit records against SQLite. Startup tests verify the app fails fast on unrecoverable schema errors.

**Acceptance criteria**

```gherkin
Scenario: Create a new database on first startup
  Given the application starts with an empty data directory
  When the persistence initializer runs
  Then a SQLite database is created with the required tables, indexes, and schema version record

Scenario: Read and write repository records
  Given a valid initialized database
  When a repository stores and retrieves a settings, user, session, capture, or audit record
  Then the returned data matches the persisted values

Scenario: Detect an unrecoverable schema mismatch
  Given the database schema version is newer than the running application supports
  When the persistence initializer runs
  Then startup is blocked with a clear operator-facing error
```

### SC-ASO-302 - Local account authentication

**User story**  
As an operator, I want local username and password authentication so that access to protected Shrimp Cam features does not depend on an external identity provider.

**Dependencies**  
SC-ASO-301

**Test expectations**  
Unit tests cover password hashing, verification, and credential validation rules. API tests cover login success, invalid credentials, and disabled account handling. Security tests verify passwords are never returned by API models or logs.

**Acceptance criteria**

```gherkin
Scenario: Authenticate with valid local credentials
  Given an active local user account with a stored password hash
  When the user submits the correct username and password
  Then authentication succeeds and the API issues an authenticated session or token

Scenario: Reject invalid credentials
  Given an active local user account
  When the user submits an incorrect password
  Then authentication fails with an unauthorized response
  And no password details are exposed in the response body

Scenario: Reject disabled accounts
  Given a local user account marked disabled
  When the user submits valid credentials
  Then authentication fails with an unauthorized response
```

### SC-ASO-303 - Bootstrap administrator flow

**User story**  
As an installer, I want a one-time bootstrap administrator flow so that a new deployment can be secured before normal sign-in begins.

**Dependencies**  
SC-ASO-301

**Test expectations**  
API tests cover first-run bootstrap, repeat bootstrap rejection, and invalid password policy failures. Integration tests verify bootstrap state is derived from persisted users rather than in-memory state.

**Acceptance criteria**

```gherkin
Scenario: Create the first administrator on a new installation
  Given the system has no local user accounts
  When an installer submits valid bootstrap administrator details
  Then the administrator account is created with the admin role
  And bootstrap is marked complete

Scenario: Block bootstrap after initialization
  Given the system already contains at least one administrator account
  When a client calls the bootstrap administrator endpoint
  Then the request is rejected as no longer available

Scenario: Reject weak bootstrap credentials
  Given the system has no local user accounts
  When an installer submits a password that violates policy
  Then the request is rejected with validation errors
```

### SC-ASO-304 - Authorization roles and policy enforcement

**User story**  
As an administrator, I want role-based authorization so that viewers, operators, and administrators can access only the actions they need.

**Dependencies**  
SC-ASO-301, SC-ASO-302, SC-ASO-303

**Test expectations**  
Authorization tests cover endpoint policy mappings for anonymous, viewer, operator, and admin identities. API tests verify forbidden responses for insufficient roles. Unit tests cover role resolution from stored assignments.

**Acceptance criteria**

```gherkin
Scenario: Allow access when the caller has the required role
  Given a signed-in user with the administrator role
  When the user calls an administrator-only endpoint
  Then the request succeeds

Scenario: Deny access when the caller lacks the required role
  Given a signed-in user with the viewer role
  When the user calls an administrator-only endpoint
  Then the API returns a forbidden response

Scenario: Require authentication for protected endpoints
  Given an anonymous caller
  When the caller requests a protected settings endpoint
  Then the API returns an unauthorized response
```

### SC-ASO-305 - Session or token lifecycle management

**User story**  
As a signed-in user, I want secure session or token management so that I can stay authenticated safely and be forced to re-authenticate when access should end.

**Dependencies**  
SC-ASO-301, SC-ASO-302

**Test expectations**  
API tests cover issuance, refresh or renewal behavior if supported, logout, expiration, and revoked-session handling. Security tests verify secure cookie or token settings, server-side revocation, and inactivity timeout behavior.

**Acceptance criteria**

```gherkin
Scenario: Use a valid active session
  Given a user has successfully authenticated
  When the user calls a protected endpoint with a valid active session or token
  Then the API authorizes the request

Scenario: Expire an inactive or outdated session
  Given a previously issued session or token has expired
  When the caller uses it on a protected endpoint
  Then the API returns an unauthorized response

Scenario: Revoke access on logout
  Given a signed-in user with an active session or token
  When the user signs out
  Then the session or token is revoked
  And future use of that credential is rejected
```

### SC-ASO-306 - Health and readiness endpoint

**User story**  
As an operator, I want a health endpoint that reports runtime status so that I can quickly assess whether the API, database, storage, and camera integrations are usable.

**Dependencies**  
SC-ASO-301

**Test expectations**  
API tests cover healthy, degraded, and unhealthy responses. Integration tests simulate database, storage, and camera dependency failures. Contract tests verify a stable response shape for dashboards and probes.

**Acceptance criteria**

```gherkin
Scenario: Report healthy status when dependencies are available
  Given the API, database, storage path, and camera integration are available
  When a client calls the health endpoint
  Then the response reports healthy status for the service and its dependencies

Scenario: Report degraded status when a non-fatal dependency is unavailable
  Given the API is running but the camera integration is temporarily unavailable
  When a client calls the health endpoint
  Then the response reports degraded overall status
  And the camera component is identified as unhealthy

Scenario: Report unhealthy status when startup-critical dependencies are unavailable
  Given the API cannot access the configured database
  When a client calls the health endpoint after startup handling completes
  Then the response reports unhealthy status
```

### SC-ASO-307 - Settings management API

**User story**  
As an administrator, I want a validated settings API so that I can manage capture, retention, storage, and hosting configuration without editing raw files.

**Dependencies**  
SC-ASO-301, SC-ASO-304

**Test expectations**  
API tests cover reading and updating settings with role enforcement. Validation tests cover allowed ranges, required fields, and immutable or computed settings. Integration tests verify persisted settings are reloaded correctly.

**Acceptance criteria**

```gherkin
Scenario: Update validated settings
  Given an authenticated administrator
  When the administrator submits a valid settings update
  Then the API persists the settings
  And the response returns the saved values

Scenario: Reject invalid settings
  Given an authenticated administrator
  When the administrator submits settings outside allowed limits
  Then the API returns a validation error
  And the existing settings remain unchanged

Scenario: Block unauthorized settings changes
  Given an authenticated viewer
  When the viewer submits a settings update
  Then the API returns a forbidden response
```

### SC-ASO-308 - Capture browsing APIs

**User story**  
As a viewer, I want capture browsing APIs with filtering and pagination so that I can reliably navigate stored timelapse images from the web app.

**Dependencies**  
SC-ASO-301, SC-ASO-304

**Test expectations**  
API tests cover listing captures by date range, pagination, detail retrieval, and invalid filter handling. Repository integration tests verify sorting and index-backed query performance assumptions for realistic datasets.

**Acceptance criteria**

```gherkin
Scenario: Browse captures with filters and paging
  Given capture records exist across multiple dates
  When a viewer requests captures for a specific date range and page
  Then the API returns captures in the expected sort order
  And the response includes paging metadata

Scenario: Retrieve a single capture by identifier
  Given a capture record exists
  When a viewer requests that capture by identifier
  Then the API returns the capture metadata

Scenario: Reject unsupported filters
  Given an authenticated caller
  When the caller submits an invalid date range or paging request
  Then the API returns a validation error
```

### SC-ASO-309 - Audit and security event logging

**User story**  
As an administrator, I want audit and security event logs so that authentication, authorization, and privileged configuration changes can be reviewed after the fact.

**Dependencies**  
SC-ASO-301, SC-ASO-302, SC-ASO-304, SC-ASO-307

**Test expectations**  
Integration tests verify audit entries are written for sign-in, sign-out, bootstrap, role changes, settings updates, and restore operations. Security tests verify secrets and passwords are redacted. API tests verify only authorized roles can query audit history if exposed.

**Acceptance criteria**

```gherkin
Scenario: Record a successful privileged action
  Given an administrator updates system settings
  When the update succeeds
  Then an audit record is stored with the action type, actor, outcome, and timestamp

Scenario: Record a denied security event
  Given a viewer attempts an administrator-only action
  When authorization fails
  Then a security event is recorded with a denied outcome

Scenario: Redact sensitive values from audit data
  Given a user changes authentication-related settings
  When the action is logged
  Then no plaintext secret or password value is stored in the audit record
```

### SC-ASO-310 - Degraded startup and runtime resilience

**User story**  
As an operator, I want the service to start in a degraded mode when non-critical dependencies fail so that diagnostics and recovery actions remain available.

**Dependencies**  
SC-ASO-301, SC-ASO-306

**Test expectations**  
Integration tests simulate missing camera devices, unavailable export directories, and corrupted optional configuration while the API remains partially available. Startup tests verify truly critical failures still stop the service with clear diagnostics.

**Acceptance criteria**

```gherkin
Scenario: Start in degraded mode when the camera is unavailable
  Given the configured camera device cannot be opened at startup
  When the application starts
  Then the API process remains available
  And the health status reports degraded camera availability

Scenario: Stop startup when the database is unavailable
  Given the configured database file cannot be opened or created
  When the application starts
  Then the service does not enter ready state
  And startup logs describe the blocking failure

Scenario: Preserve recovery endpoints in degraded mode
  Given the application is running in degraded mode
  When an administrator requests health or diagnostics information
  Then the request succeeds
```

### SC-ASO-311 - Structured application logs

**User story**  
As an operator, I want structured logs with correlation details so that requests, background jobs, and failures can be diagnosed consistently across environments.

**Dependencies**  
SC-ASO-306, SC-ASO-310

**Test expectations**  
Logging tests verify stable event fields for timestamp, level, event name, correlation identifier, user identifier when available, and component name. Integration tests verify request correlation flows through API and background job logs. Security tests verify secrets are excluded.

**Acceptance criteria**

```gherkin
Scenario: Emit structured logs for API requests
  Given a request reaches the API
  When request processing completes
  Then the application writes a structured log entry with correlation and outcome fields

Scenario: Correlate logs for a failing operation
  Given a request triggers a downstream failure
  When error logging occurs
  Then all log entries for the request share the same correlation identifier

Scenario: Avoid logging secret values
  Given a request contains credentials or secret configuration fields
  When the application logs request processing details
  Then secret values are omitted or redacted
```

### SC-ASO-312 - Diagnostics and support bundle API

**User story**  
As an operator, I want diagnostics endpoints and a support bundle export so that I can troubleshoot incidents without direct database or host access.

**Dependencies**  
SC-ASO-306, SC-ASO-311

**Test expectations**  
API tests cover authorized diagnostics retrieval, bundle generation, and denied access for non-admin users. Integration tests verify bundles include expected metadata, health state, recent logs, and configuration snapshots with secret redaction.

**Acceptance criteria**

```gherkin
Scenario: Generate a diagnostics bundle
  Given an authenticated administrator
  When the administrator requests a diagnostics bundle
  Then the API returns a generated bundle containing current health data and recent diagnostics artifacts

Scenario: Redact secrets from diagnostic output
  Given the application has stored credentials or secret settings
  When a diagnostics bundle is generated
  Then those values are removed or redacted from the bundle

Scenario: Block unauthorized diagnostics access
  Given an authenticated viewer
  When the viewer requests diagnostics data
  Then the API returns a forbidden response
```

### SC-ASO-313 - Backup and export workflow

**User story**  
As an administrator, I want a backup and export workflow so that configuration, metadata, and selected captures can be preserved before maintenance or migration.

**Dependencies**  
SC-ASO-301, SC-ASO-307, SC-ASO-312

**Test expectations**  
Integration tests cover backup manifest generation, archive creation, and export of configuration and metadata with integrity checks. API tests verify authorization and conflict handling when another export is already running.

**Acceptance criteria**

```gherkin
Scenario: Export a complete backup package
  Given an authenticated administrator
  When the administrator starts a backup export
  Then the system creates an archive containing the configured backup contents and a manifest

Scenario: Reject overlapping export operations
  Given a backup export is already in progress
  When another export request is submitted
  Then the API returns a conflict response

Scenario: Fail export with a clear error when storage is insufficient
  Given the target backup location does not have enough free space
  When a backup export starts
  Then the export fails with an operator-visible error
```

### SC-ASO-314 - Restore and import workflow

**User story**  
As an administrator, I want a validated restore and import workflow so that a replacement host can be brought back to a known-good state safely.

**Dependencies**  
SC-ASO-301, SC-ASO-309, SC-ASO-313

**Test expectations**  
Integration tests cover restore into an empty environment, partial import validation, schema compatibility checks, and rollback on failed restore. Security tests verify only authorized administrators can run restore operations and that restore actions are audited.

**Acceptance criteria**

```gherkin
Scenario: Restore from a valid backup package
  Given an authenticated administrator and a valid backup package
  When the administrator starts a restore
  Then the system imports the supported data and reports completion status

Scenario: Reject an incompatible backup package
  Given an authenticated administrator and a backup package with an unsupported schema version
  When the administrator starts a restore
  Then the restore is rejected before data changes are committed

Scenario: Roll back on restore failure
  Given a restore encounters an error after validation begins
  When the restore process fails
  Then partial changes are rolled back or the failure is surfaced with the system left in a consistent state
```

### SC-ASO-315 - Deployment support for Windows service and systemd

**User story**  
As an operator, I want first-class Windows service and `systemd` deployment support so that Shrimp Cam can start automatically and run predictably on both supported host platforms.

**Dependencies**  
SC-ASO-306, SC-ASO-310, SC-ASO-311

**Test expectations**  
Deployment validation covers Windows service hosting configuration, Linux `systemd` unit behavior, working directory and data path handling, restart policy behavior, and log location conventions. Smoke tests verify the service reaches ready or degraded state on both platforms.

**Acceptance criteria**

```gherkin
Scenario: Start automatically under the host service manager
  Given Shrimp Cam is installed as a Windows service or `systemd` service
  When the host machine boots
  Then the service starts automatically with the configured content root and data paths

Scenario: Recover from a transient process failure
  Given the deployed service exits unexpectedly
  When the host service manager applies the configured restart policy
  Then the service is restarted without manual intervention

Scenario: Surface invalid deployment configuration
  Given the service configuration references a missing required data path
  When the service starts
  Then startup fails with a clear operator-visible error
```

### SC-ASO-316 - External-hosting hardening baseline

**User story**  
As an administrator, I want external-hosting hardening controls so that Shrimp Cam can be exposed beyond the local network with a safer default security posture.

**Dependencies**  
SC-ASO-302, SC-ASO-304, SC-ASO-305, SC-ASO-307, SC-ASO-311

**Test expectations**  
Security tests cover secure headers, HTTPS-aware forwarding behavior, cookie or token transport protections, rate limiting or lockout rules for authentication endpoints, and default rejection of unsafe hosting settings. Deployment tests verify local-only defaults remain intact unless explicitly changed.

**Acceptance criteria**

```gherkin
Scenario: Apply safer defaults for externally hosted deployments
  Given the application is configured for external access
  When the API starts
  Then external-hosting safeguards such as secure transport requirements and stricter auth protections are enabled

Scenario: Resist repeated invalid sign-in attempts
  Given a client repeatedly submits invalid credentials
  When the configured threshold is exceeded
  Then the authentication endpoint applies the configured lockout or throttling response

Scenario: Reject unsafe external-hosting configuration
  Given the application is configured for external access without the required hardening prerequisites
  When the API starts or validates settings
  Then the configuration is rejected with actionable validation errors
```

### SC-ASO-317 - Startup default administrator initialization

**User story**  
As a Shrimp Cam operator, I want a default administrator initialized during startup so that a fresh Raspberry Pi or Windows install can be accessed without running a separate PowerShell bootstrap command.

**Implementation notes**  
The first-release default account is `admin` with initial password `AdminPass1234`. The initializer must only create this account when no Administrator role assignment exists. The password should be configurable for deployment overrides and treated as an initial setup credential that should be changed after first sign-in.

**Dependencies**  
SC-ASO-301, SC-ASO-302, SC-ASO-303, SC-ASO-304

**Test expectations**  
Unit and API/startup tests cover fresh database initialization, no-op behavior when an administrator already exists, successful sign-in with the startup-created default user, and preservation of existing administrator accounts.

**Acceptance criteria**

```gherkin
Scenario: Fresh install creates the default administrator
  Given Shrimp Cam starts with an initialized database that has no administrator
  When startup initialization completes
  Then an enabled user named "admin" exists
  And the user has the Administrator role
  And the user can sign in with the configured initial password

Scenario: Existing administrator is preserved
  Given Shrimp Cam starts with an existing administrator account
  When startup initialization completes
  Then no replacement default administrator is created
  And the existing administrator credentials remain valid

Scenario: Configured startup credential is invalid
  Given the configured initial administrator password does not satisfy the password policy
  When startup initialization attempts to create the default administrator
  Then startup fails with an actionable configuration or initialization error
```

### SC-ASO-318 - Protect camera operation endpoints

**User story**  
As a Shrimp Cam administrator, I want live stream and camera-triggering endpoints protected by authorization so that internet-exposed deployments cannot be viewed or controlled anonymously.

**Dependencies**  
SC-ASO-302, SC-ASO-304, SC-ASO-305, SC-CC-04, SC-CC-06, SC-CC-11

**Test expectations**  
API tests cover anonymous, viewer, and administrator authorization boundaries for live stream, manual capture, and motion highlight camera operations.

**Acceptance criteria**

```gherkin
Scenario: Anonymous users cannot view or trigger camera operations
  Given no valid session token is present
  When a request is made to live stream, manual capture, or motion highlight endpoints
  Then the API rejects the request with 401

Scenario: Viewer can watch live feed but cannot trigger privileged camera work
  Given a signed-in viewer session
  When the viewer opens the live stream
  Then the live stream request is allowed
  When the viewer triggers manual capture or motion highlight ingestion
  Then the request is forbidden

Scenario: Admin can perform camera operations
  Given a signed-in administrator session
  When the admin opens live stream or triggers capture operations
  Then the API authorizes the request before invoking camera services
```

### SC-ASO-319 - Redact authentication secrets from audit logs

**User story**  
As a Shrimp Cam administrator, I want passwords and tokens excluded from audit records so that diagnostics and audit history never expose credentials.

**Dependencies**  
SC-ASO-302, SC-ASO-305, SC-ASO-309

**Test expectations**  
API/audit tests verify failed sign-in, bootstrap, and successful sign-in audit records do not contain submitted passwords, raw bearer tokens, or credential fields.

**Acceptance criteria**

```gherkin
Scenario: Failed sign-in is audited without password disclosure
  Given a user submits invalid credentials
  When the sign-in attempt is audited
  Then the audit detail does not contain the submitted password
  And the audit detail does not include a password field

Scenario: Successful sign-in is audited without token disclosure
  Given a user signs in successfully
  When the audit event is stored
  Then the audit detail includes non-secret session metadata
  And the audit detail does not contain the raw bearer token
```

## Delivery Notes

- Recommended implementation order: `SC-ASO-301` through `SC-ASO-316` in sequence, with `SC-ASO-306` to `SC-ASO-311` parallelizable after authentication foundations land.
- Startup default administrator initialization should be treated as a first-run convenience story and revisited before internet-exposed release hardening.
- All API-facing stories should include OpenAPI updates, authorization annotations, and problem-details error responses as part of implementation.
- All operational stories should prefer redaction-by-default for secrets, credentials, and host-specific sensitive paths unless explicitly needed for recovery.

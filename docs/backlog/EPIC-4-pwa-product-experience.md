# Epic 4: PWA Product Experience

## Epic Summary

Deliver a production-ready, mobile-first Progressive Web App for Shrimp Cam that makes the camera system easy to use on phones, tablets, and desktops across normal, degraded, and offline conditions. This epic covers the end-user experience for navigation, authentication flow, dashboard monitoring, live viewing, gallery browsing, settings and status visibility, installability, resilience states, and accessible touch-friendly interactions.

## Story Backlog

### SC-PWA-01 App Shell And Mobile Routing

**User story:** As a Shrimp Cam user, I want a fast mobile-first app shell with clear routing so that I can move between core areas of the app without confusion.

**Dependencies:** None

**Test expectations:**
- Component tests for shared shell layout, active navigation states, and protected route handling.
- Routing tests for direct URL entry, browser refresh, and unknown route fallback.
- End-to-end test covering navigation between dashboard, live view, gallery, and settings/status on a mobile viewport.

**Acceptance criteria:**

```gherkin
Scenario: User navigates between core app sections
  Given the user opens the Shrimp Cam PWA while the service is reachable
  When the app finishes loading
  Then the user sees the shared app shell with navigation to Dashboard, Live, Gallery, and Settings
  And the current route is visually identified
  When the user selects another navigation destination
  Then the destination screen loads without a full page refresh
  And the app shell remains visible and consistent

Scenario: User enters an unknown route
  Given the user opens a URL that does not match a valid route
  When the app resolves routing
  Then the user sees a not found state with a clear path back to the dashboard
```

### SC-PWA-02 Sign-In And Session Experience

**User story:** As a user accessing a protected Shrimp Cam instance, I want a simple sign-in and session experience so that I can securely reach the app and recover cleanly when my session expires.

**Dependencies:** SC-PWA-01

**Test expectations:**
- Component tests for sign-in form validation, loading state, and session-expired messaging.
- API integration tests for authenticated bootstrap, unauthorized responses, and logout behavior.
- End-to-end test for sign-in, persisted session restore, and forced re-authentication after session expiration.

**Acceptance criteria:**

```gherkin
Scenario: User signs in successfully
  Given authentication is enabled for the Shrimp Cam instance
  And the user is not signed in
  When the user submits valid credentials
  Then the user is signed in
  And the app redirects to the last requested protected screen or the dashboard
  And the session is restored on reload until it expires or the user signs out

Scenario: User submits invalid credentials
  Given authentication is enabled for the Shrimp Cam instance
  When the user submits invalid credentials
  Then the user remains on the sign-in screen
  And the app shows a non-technical error message
  And the password field is cleared or masked according to security rules

Scenario: Session expires during use
  Given the user is signed in on a protected screen
  When the backend reports the session is no longer valid
  Then the user is returned to sign-in
  And the app explains that the session expired
  And unsaved settings edits are not submitted silently
```

### SC-PWA-03 Dashboard Overview

**User story:** As a Shrimp Cam user, I want a concise dashboard so that I can quickly understand camera health, the latest capture, next scheduled capture, and storage usage.

**Dependencies:** SC-PWA-01

**Test expectations:**
- Component tests for status cards, latest snapshot preview, and empty data presentation.
- API integration tests for successful dashboard data load and partial-data degradation.
- End-to-end test validating the dashboard on first load and after a manual refresh.

**Acceptance criteria:**

```gherkin
Scenario: Dashboard loads current system overview
  Given the Shrimp Cam backend is reachable
  When the user opens the dashboard
  Then the user sees camera status, latest snapshot, next scheduled capture, and storage usage
  And each value is labeled clearly for quick scanning on mobile
  And the latest snapshot can be opened from the dashboard

Scenario: Dashboard data is partially unavailable
  Given the dashboard loads while one or more overview fields cannot be retrieved
  When the app receives the partial response or error
  Then the available data still renders
  And unavailable sections show an inline fallback state
  And the user is told how to retry or check status
```

### SC-PWA-04 Live View And Manual Snapshot UX

**User story:** As a Shrimp Cam user, I want a stable live view with clear stream status and manual capture controls so that I can monitor the tank and capture a still image when needed.

**Dependencies:** SC-PWA-01

**Test expectations:**
- Component tests for live stream container states, manual snapshot button states, and last-capture timestamp updates.
- API and stream integration tests for live availability polling and manual capture success/failure handling.
- End-to-end test for opening live view, seeing the stream, and taking a manual snapshot.

**Acceptance criteria:**

```gherkin
Scenario: User views the live camera feed and captures a snapshot
  Given the camera stream is available
  When the user opens the live view
  Then the live feed is displayed within a mobile-friendly viewport
  And the stream status is shown as online
  When the user taps the manual snapshot action
  Then the app shows capture progress
  And the new capture timestamp is updated after success

Scenario: Live stream is unavailable
  Given the live view is opened while the camera stream cannot be started
  When the app checks stream availability
  Then the live feed area shows an offline or unavailable state
  And the user sees guidance to retry or check device status
  And the manual snapshot action is disabled or handled with a clear error if capture is impossible
```

### SC-PWA-05 Gallery Browsing And Full-Screen Viewer

**User story:** As a Shrimp Cam user, I want to browse captures by recency and date so that I can quickly review tank activity and open images in a focused viewer.

**Dependencies:** SC-PWA-01

**Test expectations:**
- Component tests for gallery list rendering, day filtering, empty results, and full-screen viewer controls.
- API integration tests for paged capture retrieval, filter changes, and image load failure handling.
- End-to-end test for browsing recent captures, filtering by day, and opening an image viewer on mobile.

**Acceptance criteria:**

```gherkin
Scenario: User browses captures and opens an image
  Given capture metadata and image files are available
  When the user opens the gallery
  Then captures are listed in reverse chronological order
  And the user can filter by day
  When the user selects a capture
  Then the image opens in a focused viewer with capture time and navigation controls

Scenario: Gallery filter returns no captures
  Given the user applies a date filter with no matching captures
  When the filtered gallery finishes loading
  Then the app shows an empty state explaining that no captures were found
  And the user can clear or change the filter easily
```

### SC-PWA-06 Settings And System Status Experience

**User story:** As a Shrimp Cam user, I want a combined settings and system status area so that I can safely review device health and update supported preferences with confidence.

**Dependencies:** SC-PWA-01, SC-PWA-02

**Test expectations:**
- Component tests for editable settings forms, validation, dirty-state warnings, and health/status summaries.
- API integration tests for reading current settings, successful updates, validation failures, and stale-session handling.
- End-to-end test covering settings update, success confirmation, and device status review.

**Acceptance criteria:**

```gherkin
Scenario: User updates supported settings successfully
  Given the user is authorized to manage Shrimp Cam settings
  When the user changes supported capture or retention settings with valid values
  And submits the form
  Then the app saves the settings
  And shows a success confirmation
  And the refreshed values match what was saved

Scenario: User submits invalid settings
  Given the user is editing settings
  When the user enters an invalid value or conflicting schedule
  Then the app blocks submission
  And highlights the invalid fields with clear guidance
  And preserves the user's unsaved edits for correction
```

### SC-PWA-07 Offline Shell And Cached Core Experience

**User story:** As a user on an unstable local network, I want the Shrimp Cam shell to open even when the device is offline so that I can understand the connection state and access recently cached core metadata.

**Dependencies:** SC-PWA-01, SC-PWA-03, SC-PWA-05

**Test expectations:**
- Service worker tests for shell caching, cache versioning, and stale asset replacement.
- Component tests for offline banners, cached metadata markers, and unavailable action states.
- End-to-end test simulating offline launch after a successful prior visit.

**Acceptance criteria:**

```gherkin
Scenario: User opens the app while offline after a previous successful visit
  Given the user has previously loaded the Shrimp Cam PWA successfully
  And the app shell and recent metadata have been cached
  When the user opens the app while the backend is unreachable
  Then the app shell still loads
  And the user sees that the app is offline
  And any cached dashboard or gallery metadata is marked as potentially stale

Scenario: User opens the app offline with no prior cached visit
  Given the app has not been loaded successfully on this device before
  When the user opens the app while offline
  Then the app shows an offline-first failure state
  And the message explains that an initial online visit is required
```

### SC-PWA-08 Installable PWA Behavior

**User story:** As a frequent Shrimp Cam user, I want to install the app on my device so that I can launch it like a native app and keep it easy to access.

**Dependencies:** SC-PWA-01, SC-PWA-07

**Test expectations:**
- Manifest and service worker verification tests for installability requirements.
- Component tests for install prompt affordances, dismissed prompt behavior, and platform-specific fallback guidance.
- Manual acceptance checks on iOS Safari, Android Chrome, and desktop Chromium-class browsers.

**Acceptance criteria:**

```gherkin
Scenario: User installs the PWA on a supported device
  Given the Shrimp Cam PWA meets installability requirements
  And the user is on a supported browser
  When the install prompt is available and the user chooses to install
  Then the app installs with the Shrimp Cam name, icon, and standalone launch behavior
  And the user can reopen it from the device app launcher or home screen

Scenario: Install prompt is not available
  Given the user is on a browser that does not expose a standard install prompt
  When the user opens the app
  Then the app does not show a broken install action
  And the user sees platform-appropriate guidance if manual installation is supported
```

### SC-PWA-09 Loading, Error, And Reconnect States

**User story:** As a Shrimp Cam user, I want clear loading, error, and reconnect feedback so that I always know whether the app is working, waiting, or needs my attention.

**Dependencies:** SC-PWA-03, SC-PWA-04, SC-PWA-05, SC-PWA-06, SC-PWA-07

**Test expectations:**
- Component tests for skeleton states, retry actions, polling backoff, and reconnect banners across key screens.
- API integration tests for timeout, server error, and recovery-after-retry behavior.
- End-to-end test for temporary backend loss followed by successful reconnection.

**Acceptance criteria:**

```gherkin
Scenario: Screen data loads successfully after a brief delay
  Given the user opens a data-driven screen
  When the response is still pending
  Then the app shows a loading state appropriate to that screen
  And the layout remains stable enough to avoid accidental taps
  When the response succeeds
  Then the loading state is replaced with the final content

Scenario: Connection is lost and later restored
  Given the user is viewing a screen that refreshes live or recent data
  When the backend becomes temporarily unreachable
  Then the app shows a clear error or reconnect state
  And the user can retry manually when appropriate
  When connectivity returns
  Then the app recovers without requiring a full app restart
```

### SC-PWA-10 Accessibility And Touch Usability

**User story:** As a mobile and assistive-technology user, I want the Shrimp Cam PWA to be accessible and touch-friendly so that I can use every core feature reliably and comfortably.

**Dependencies:** SC-PWA-01, SC-PWA-03, SC-PWA-04, SC-PWA-05, SC-PWA-06, SC-PWA-09

**Test expectations:**
- Automated accessibility checks for color contrast, semantic landmarks, labels, focus order, and aria usage on all core screens.
- Component tests for keyboard navigation, visible focus treatment, and screen-reader naming of major controls.
- Manual QA on mobile for touch target size, orientation changes, and one-handed use of primary actions.

**Acceptance criteria:**

```gherkin
Scenario: User completes core navigation and actions with accessible controls
  Given the user relies on keyboard, switch, or screen-reader navigation
  When the user moves through the app shell and core screens
  Then interactive elements have accessible names and logical focus order
  And status changes are announced appropriately
  And primary actions meet mobile touch target expectations

Scenario: Accessibility or touch constraints would hide or block a primary action
  Given a screen is displayed on a small mobile viewport or under zoom
  When a primary action would otherwise be clipped, overlapped, or too small to use
  Then the layout adapts to keep the action reachable
  And no core workflow depends on gesture-only interaction
```

### SC-PWA-11 Reference-Led Aquarium Visual System

**User story:** As a Shrimp Cam user, I want the PWA to match the approved aquarium reference renders so that the app feels polished, intentional, and consistent across every screen.

**Implementation notes:** Use `docs/Generated image 1.png` through `docs/Generated image 4.png` as the visual target. Preserve existing functionality while replacing the current heavy header/card styling with a deep aquarium background, glass panels, teal/coral accent system, rounded mobile surfaces, and bottom-first navigation.

**Dependencies:** SC-PWA-01, SC-PWA-03, SC-PWA-04, SC-PWA-05, SC-PWA-06

**Test expectations:**
- UI/component tests or snapshots for shared shell classes, navigation state, and major card variants.
- Responsive layout checks for Samsung-class mobile viewport and desktop fallback.
- Accessibility checks for contrast, focus visibility, and touch target size after the visual refresh.

**Acceptance criteria:**

```gherkin
Scenario: App shell matches the reference visual direction
  Given the user opens the Shrimp Cam PWA on a Samsung-class phone viewport
  When the app shell renders
  Then the page uses an aquarium-inspired background with glassy panels and teal/coral accents
  And the previous heavy header bar is not shown
  And primary navigation is reachable from a mobile bottom navigation area

Scenario: Visual refresh preserves usability and accessibility
  Given the visual system has been applied
  When the user navigates through core screens
  Then text remains readable against the aquarium background
  And interactive controls have visible focus and touch-friendly hit areas
```

### SC-PWA-12 Dashboard Reference Cleanup

**User story:** As a Shrimp Cam operator, I want the dashboard to resemble the approved overview render so that I can quickly understand tank status from a polished mobile-first home screen.

**Implementation notes:** Follow `docs/Generated image 1.png`: large Shrimp Cam branding, compact camera/next-timelapse cards, storage usage treatment, latest snapshot feature card, and quick action tiles.

**Dependencies:** SC-PWA-03, SC-PWA-11

**Test expectations:**
- UI tests for dashboard health, next capture, storage, latest snapshot, and quick action rendering.
- Empty/degraded-state checks for missing capture, camera offline, or unavailable health data.
- Mobile viewport verification that quick actions and status cards do not overflow.

**Acceptance criteria:**

```gherkin
Scenario: Dashboard renders the approved overview layout
  Given the dashboard data is available
  When the user opens Dashboard
  Then camera status, next timelapse, storage usage, latest snapshot, and quick actions are visible
  And the layout follows the reference render hierarchy and spacing

Scenario: Dashboard handles missing operational data
  Given the latest snapshot or camera status is unavailable
  When the dashboard loads
  Then the dashboard still renders the reference-style shell
  And the unavailable data is shown with clear fallback messaging
```

### SC-PWA-13 Live View Reference Cleanup

**User story:** As a Shrimp Cam viewer, I want the live view to resemble the approved camera render so that monitoring the tank feels immersive while controls stay easy to reach.

**Implementation notes:** Follow `docs/Generated image 2.png`: edge-to-edge stream, translucent top status pills, floating snapshot/retry controls, and a lower glass status panel.

**Dependencies:** SC-PWA-04, SC-PWA-09, SC-PWA-11

**Test expectations:**
- UI tests for live stream loaded, stream error, reconnect, and manual snapshot states.
- Mobile checks for floating control tray reachability and status panel layout.
- Accessibility checks for all icon controls having visible labels.

**Acceptance criteria:**

```gherkin
Scenario: Live stream renders as an immersive camera view
  Given the camera stream is available
  When the user opens Live
  Then the stream uses the dominant screen area
  And stream status, quality, snapshot, and refresh controls overlay or sit near the feed without blocking it

Scenario: Live view communicates stream failure clearly
  Given the stream cannot start or disconnects
  When the Live screen renders the failure state
  Then the feed area keeps the reference-style layout
  And the user sees a clear retry action and camera status context
```

### SC-PWA-14 Gallery Timeline Reference Cleanup

**User story:** As a Shrimp Cam user, I want the gallery to resemble the approved timeline render so that browsing captures feels fast, visual, and organized by time.

**Implementation notes:** Follow `docs/Generated image 3.png`: search/filter affordances, date chips, timeline/time-of-day filters, prominent selected capture, thumbnail rail, and bottom action bar.

**Dependencies:** SC-PWA-05, SC-PWA-09, SC-PWA-11

**Test expectations:**
- UI tests for populated gallery, empty filter result, image-load failure, and selected-capture behavior.
- API/UI integration tests for date filtering and reverse chronological capture ordering.
- Mobile viewport checks for horizontal chip/thumbnail scrolling without page breakage.

**Acceptance criteria:**

```gherkin
Scenario: Gallery renders captures in a visual timeline
  Given capture records and images are available
  When the user opens Gallery
  Then captures are shown in a reference-style timeline with a prominent latest or selected image
  And date filters and thumbnail browsing are reachable on mobile

Scenario: Gallery filters return no captures
  Given the user applies a filter with no matching captures
  When the gallery finishes loading
  Then the reference-style gallery shell remains visible
  And the user sees an empty state with a clear way to adjust filters
```

### SC-PWA-15 Settings Reference Cleanup

**User story:** As a Shrimp Cam administrator, I want Settings to resemble the approved system-status render so that camera, schedule, storage, and system controls feel clear and production-ready.

**Implementation notes:** Follow `docs/Generated image 4.png`: grouped settings rows, segmented interval/resolution controls where appropriate, camera-source picker, system-status section, and prominent save action.

**Dependencies:** SC-PWA-06, SC-PWA-11, SC-ASO-307

**Test expectations:**
- UI tests for settings load, edit, validation, save success, save failure, and camera-source selection.
- Mobile checks for grouped rows, save button reachability, and form controls at Samsung-class viewport.
- Accessibility checks for labels, grouped controls, error text, and keyboard navigation.

**Acceptance criteria:**

```gherkin
Scenario: Settings render with reference-style grouped controls
  Given the administrator is signed in
  When Settings load successfully
  Then capture interval, active hours, retention, stream resolution, camera source, and system status are grouped clearly
  And the screen follows the reference render visual direction

Scenario: Settings save feedback is clear
  Given the administrator changes valid settings
  When the administrator saves
  Then the save action shows progress and completion feedback
  And validation or server errors remain attached to the affected fields
```

### SC-PWA-16 Complete PWA Lifecycle Playwright Coverage

**User story:** As a Shrimp Cam operator, I want the PWA lifecycle and failure states covered by Playwright tests so that the mobile UI remains reliable across install, offline, auth expiration, dashboard failures, and unknown routes.

**Dependencies:** SC-PWA-07, SC-PWA-08, SC-PWA-09, SC-PWA-11

**Test expectations:**
- Samsung S26 Playwright tests cover offline cached shell metadata, reconnect messaging, install prompt lifecycle, app-installed state, auth expiration, unknown-route recovery, and dashboard API failures.
- Tests use mocked API/browser events rather than timing-sensitive real service worker or hardware behavior.
- Existing PWA happy-path and negative-path tests continue to pass.

**Acceptance criteria:**

```gherkin
Scenario: Offline shell uses cached metadata
  Given a signed-in user has loaded dashboard and gallery data
  When the browser goes offline
  Then the shell shows cached metadata and stale/offline guidance
  And returning online updates the connection message

Scenario: Install prompt lifecycle is represented
  Given the browser emits a beforeinstallprompt event
  When the user opens the install action
  Then Shrimp Cam exposes install-specific guidance or prompt state
  And installed or dismissed outcomes are displayed clearly

Scenario: Auth and error routes are resilient
  Given an authenticated API call returns unauthorized or an unknown route is opened
  When the app handles the response or route
  Then the user receives clear recovery guidance
  And protected session state is not trusted

Scenario: Dashboard API failures are visible
  Given health or capture loading fails
  When the dashboard renders
  Then the failure state is actionable
  And stale success messaging is not shown
```

### SC-PWA-17 Automated Accessibility Regression Coverage

**User story:** As a Shrimp Cam operator, I want automated accessibility scans across every production PWA route so that visual polish does not regress landmarks, labels, focusability, or color-contrast safety before release.

**Dependencies:** SC-PWA-10, SC-PWA-11, SC-PWA-16

**Test expectations:**
- Samsung S26 Playwright tests run axe accessibility scans on sign-in, dashboard, live, gallery, settings, offline, and not-found states.
- Tests assert there are no serious or critical accessibility violations for authenticated and unauthenticated routes.
- Tests continue to validate install-panel visibility behavior so installed users are not shown install guidance.

**Acceptance criteria:**

```gherkin
Scenario: Core PWA routes pass accessibility scans
  Given the PWA is rendered on a Samsung-class mobile viewport
  When automated accessibility scans run on sign-in, dashboard, live, gallery, settings, and not-found screens
  Then no serious or critical accessibility violations are reported

Scenario: Offline shell remains accessible
  Given a signed-in user has cached shell metadata
  When the browser goes offline
  Then the offline shell passes automated accessibility checks
  And reconnect guidance remains available to assistive technology
```

### SC-PWA-18 Reference-Led Aquarium Visual Fidelity Pass

**User story:** As a Shrimp Cam operator, I want the dashboard, live, gallery, and settings screens to more closely match the approved aquarium reference renders so that the installed PWA feels like a polished shrimp-tank control surface instead of a generic admin app.

**Dependencies:** SC-PWA-11, SC-PWA-12, SC-PWA-13, SC-PWA-14, SC-PWA-15, SC-PWA-17

**Test expectations:**
- Samsung S26 Playwright tests assert reference-led visual traits for the dashboard shell, live camera stage, gallery timeline/viewer, settings rows, bottom navigation, and primary actions.
- Accessibility scans continue to pass after the visual refresh.
- Existing sign-in, dashboard, live, gallery, settings, install, offline, and negative-path flows continue to pass.

**Acceptance criteria:**

```gherkin
Scenario: Core screens follow the aquarium reference direction
  Given the user opens Shrimp Cam on a Samsung-class phone viewport
  When dashboard, live, gallery, and settings render
  Then each screen uses a deep aquarium atmosphere with glass panels, glowing teal accents, coral primary actions, rounded bottom navigation, and screen-specific layouts
  And the UI no longer reads as a generic boxed admin dashboard

Scenario: Reference styling remains covered by Playwright
  Given the reference-led visual pass is implemented
  When Playwright runs the UI suite
  Then tests assert the key style traits that match the approved renders
  And accessibility checks still report no serious or critical violations
```

### SC-PWA-19 PWA Manifest And Service Worker Installability Verification

**User story:** As a Shrimp Cam operator, I want automated verification of the production manifest and service worker output so that installability does not regress before the app is flashed or deployed.

**Dependencies:** SC-PWA-08, SC-PWA-16, SC-PWA-17

**Test expectations:**
- Samsung S26 Playwright tests fetch the production `manifest.webmanifest` from the preview server and assert installability fields, theme colors, standalone display mode, scope, start URL, and icon metadata.
- Playwright verifies the production service worker is emitted and precaches the app shell, manifest, and icon assets.
- Existing install prompt lifecycle tests continue to cover supported prompt, dismissed prompt, and installed app states.

**Acceptance criteria:**

```gherkin
Scenario: Production bundle includes an installable manifest
  Given the PWA production build has completed
  When the manifest is requested from the hosted app
  Then it declares the Shrimp Cam app name, standalone display, root scope, root start URL, theme colors, and install icon
  And the referenced icon is served successfully

Scenario: Production bundle includes a service worker
  Given the PWA production build has completed
  When the service worker is requested from the hosted app
  Then it is served successfully as JavaScript
  And it precaches the app shell, manifest, and install icon assets
```

### SC-PWA-20 Auth Session Restore, Expiry, And Sign-Out Edge Coverage

**User story:** As a Shrimp Cam operator, I want automated coverage for saved sessions, expired sessions, corrupt session storage, and sign-out so that users do not get stuck in unsafe or confusing authentication states.

**Dependencies:** SC-PWA-02, SC-PWA-16, SC-PWA-17

**Test expectations:**
- Samsung S26 Playwright tests restore a valid saved session without requiring another sign-in and verify authenticated requests use the stored bearer token.
- Playwright verifies expired and corrupt saved sessions are cleared before protected content renders.
- Playwright verifies sign-out calls the backend with the active bearer token, removes local session state, hides protected navigation, and returns the user to sign-in with clear feedback.

**Acceptance criteria:**

```gherkin
Scenario: Valid saved session is restored
  Given the browser has a saved unexpired Shrimp Cam session
  When the user opens a protected route
  Then the protected screen renders without another sign-in
  And authenticated API requests use the saved bearer token

Scenario: Invalid saved sessions are cleared
  Given the browser has an expired or corrupt saved session
  When the user opens a protected route
  Then the saved session is removed
  And the user is returned to sign-in before protected content is shown

Scenario: User signs out
  Given the user is signed in
  When the user chooses Sign out
  Then the backend logout endpoint receives the active bearer token
  And the saved session is cleared
  And protected navigation is hidden until the user signs in again
```

### SC-PWA-21 Gallery And Settings Edge-State Playwright Coverage

**User story:** As a Shrimp Cam operator, I want gallery and settings edge-state recovery covered by Playwright so that capture browsing and administration stay usable after transient API failures.

**Dependencies:** SC-PWA-05, SC-PWA-06, SC-PWA-09, SC-PWA-17, SC-PWA-18

**Test expectations:**
- Samsung S26 Playwright tests cover capture-history load failure, visible gallery fallback messaging, and successful recovery after a subsequent reload.
- Playwright verifies gallery recovery restores timeline chips, selected protected image loading, and capture summary text.
- Playwright covers settings API load failure, unavailable-form presentation, missing save action while no form is loaded, and recovery through the Refresh action.
- Existing gallery empty/filter/image-failure and settings validation/save-rejection tests continue to pass.

**Acceptance criteria:**

```gherkin
Scenario: Gallery recovers after capture history is temporarily unavailable
  Given the user is signed in
  And capture history loading fails
  When the user opens Gallery
  Then the app shows actionable gallery failure messaging without a stale capture list
  When the next gallery reload succeeds
  Then timeline chips, capture summary, and the selected protected image render again

Scenario: Settings recovers after settings loading is temporarily unavailable
  Given the user is signed in
  And settings loading fails
  When the user opens Settings
  Then the app shows a settings unavailable state
  And no save action is available without a loaded form
  When the user refreshes after the service recovers
  Then the settings summary, camera discovery message, and disabled clean save action render again
```

## Delivery Notes

- Story sequencing should start with shell and routing, then move through authentication, core screens, resilience behavior, and final accessibility hardening.
- Reference-led cleanup should start with `SC-PWA-11`, then proceed by screen from `SC-PWA-12` through `SC-PWA-15`.
- Each story is expected to ship with automated coverage at the appropriate UI, API contract, and end-to-end layers in line with the project definition of done.
- Any backend dependency not yet available should be mocked behind stable contracts so PWA implementation can proceed in parallel without weakening acceptance coverage.

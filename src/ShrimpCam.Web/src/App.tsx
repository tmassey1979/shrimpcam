import { useEffect, useMemo, useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { NavLink, Navigate, Route, Routes, useLocation, useNavigate } from "react-router-dom";
import type { NavigateFunction } from "react-router-dom";
import { useOnlineStatus } from "./useOnlineStatus";

type NavItem = {
  to: string;
  label: string;
  icon: string;
};

type Session = {
  sessionId: string;
  userId: string;
  userName: string;
  token: string;
  expiresAtUtc: string;
};

type LoginResponse = Session & {
  status: string;
};

type LoginState = {
  message: string | null;
  returnTo: string;
};

type HealthResponse = {
  status: string;
  checkedAtUtc: string;
  components: Record<string, { status: string; detail: string | null }>;
};

type CaptureListResponse = {
  items: CaptureSummary[];
  paging: {
    pageNumber: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
  };
};

type CaptureSummary = {
  id: string;
  fileName: string;
  sourceType: string;
  capturedAtUtc: string;
  imageUrl: string;
  metadataUrl: string;
};

type DashboardState = {
  health: HealthResponse | null;
  latestCapture: CaptureSummary | null;
  totalCaptures: number | null;
  healthError: string | null;
  capturesError: string | null;
  isLoading: boolean;
};

type LiveStreamStatus = "connecting" | "online" | "offline";

type ManualCaptureResponse = {
  status: string;
  reason?: string;
  capturedAtUtc?: string;
  fileName?: string;
};

type GalleryState = {
  captures: CaptureSummary[];
  selectedCapture: CaptureSummary | null;
  totalItems: number;
  hasNextPage: boolean;
  isLoading: boolean;
  error: string | null;
  imageFailed: boolean;
};

type SettingsResponse = {
  camera: {
    platform: string;
    source: string;
    captureWidth: number;
    captureHeight: number;
    streamWidth: number;
    streamHeight: number;
    streamFramesPerSecond: number;
    reconnectRetryAttempts: number;
    reconnectBackoffSeconds: number;
  };
  capture: {
    enabled: boolean;
    intervalMinutes: number;
    activeStartHourUtc: number;
    activeEndHourUtc: number;
    motionHighlightsEnabled: boolean;
    motionThreshold: number;
    motionCooldownSeconds: number;
  };
  storage: {
    retentionDays: number;
  };
  security: {
    hostMode: string;
  };
};

type SettingsState = {
  form: SettingsResponse | null;
  health: HealthResponse | null;
  isLoading: boolean;
  isSaving: boolean;
  isDirty: boolean;
  message: string | null;
  errors: Record<string, string>;
};

type ValidationProblemResponse = {
  errors?: Record<string, string[]>;
};

type CachedShellMetadata = {
  cachedAtUtc: string;
  dashboard?: {
    healthStatus: string;
    latestCaptureFileName: string | null;
    latestCaptureAtUtc: string | null;
    totalCaptures: number | null;
  };
  gallery?: {
    captureCount: number;
    newestCaptureFileName: string | null;
    newestCaptureAtUtc: string | null;
  };
};

type BeforeInstallPromptEvent = Event & {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed"; platform: string }>;
};

type InstallPromptState = {
  canPrompt: boolean;
  isInstalled: boolean;
  message: string;
  promptInstall: () => Promise<void>;
};

type AuthContext = {
  session: Session | null;
  isAuthenticated: boolean;
  signIn: (userName: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
  authenticatedFetch: (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;
};

type SignInScreenProps = {
  auth: AuthContext;
};

type ProtectedRouteProps = {
  auth: AuthContext;
  children: ReactNode;
};

type StatCardProps = {
  eyebrow: string;
  value: string;
  detail: string;
};

type ScreenFrameProps = {
  title: string;
  description: string;
  children: ReactNode;
};

const sessionStorageKey = "shrimpcam.session";
const shellMetadataStorageKey = "shrimpcam.shellMetadata";

const navItems: NavItem[] = [
  { to: "/dashboard", label: "Dashboard", icon: "DB" },
  { to: "/live", label: "Live", icon: "LV" },
  { to: "/gallery", label: "Gallery", icon: "GL" },
  { to: "/settings", label: "Settings", icon: "ST" }
];

function App() {
  const auth = useAuthSession();
  const isOnline = useOnlineStatus();
  const installPrompt = useInstallPrompt();
  const reconnectNotice = useReconnectNotice(isOnline);
  const [cachedShellMetadata, setCachedShellMetadata] = useState<CachedShellMetadata | null>(() => readCachedShellMetadata());
  const statusLabel = isOnline ? "Connected" : "Offline";
  const shellMessage = isOnline
    ? auth.isAuthenticated
      ? `Signed in as ${auth.session?.userName}.`
      : "Sign in to reach your protected camera workspace."
    : "Offline shell active. Cached content may be stale.";

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <header className="topbar">
        <div className="brand-lockup">
          <span className="brand-mark" aria-hidden="true">
            SC
          </span>
          <div>
            <p className="eyebrow">Shrimp Cam</p>
            <h1>Reef watch</h1>
            <span className="brand-subtitle">Timelapse, live view, and tank status</span>
          </div>
        </div>
        <div className="topbar-actions">
          <span className={`status-pill ${isOnline ? "online" : "offline"}`}>{statusLabel}</span>
          {auth.isAuthenticated ? (
            <button type="button" className="ghost-button" onClick={() => void auth.signOut()}>
              Sign out
            </button>
          ) : null}
        </div>
      </header>

      <main id="main-content" className="content" tabIndex={-1}>
        {!isOnline ? (
          <OfflineShellPanel metadata={cachedShellMetadata} />
        ) : null}
        {reconnectNotice ? (
          <div className="reconnect-banner" role="status" aria-live="polite">
            {reconnectNotice}
          </div>
        ) : null}

        <section className="hero-card">
          <p className="eyebrow">Secure Shell</p>
          <h2>{auth.isAuthenticated ? "Session active" : "Sign-in required"}</h2>
          <p>{shellMessage}</p>
          <InstallPromptPanel installPrompt={installPrompt} />
        </section>

        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/sign-in" element={<SignInScreen auth={auth} />} />
          <Route
            path="/dashboard"
            element={
              <ProtectedRoute auth={auth}>
                <DashboardScreen auth={auth} onMetadataCached={setCachedShellMetadata} />
              </ProtectedRoute>
            }
          />
          <Route
            path="/live"
            element={
              <ProtectedRoute auth={auth}>
                <LiveViewScreen auth={auth} />
              </ProtectedRoute>
            }
          />
          <Route
            path="/gallery"
            element={
              <ProtectedRoute auth={auth}>
                <GalleryScreen auth={auth} onMetadataCached={setCachedShellMetadata} />
              </ProtectedRoute>
            }
          />
          <Route
            path="/settings"
            element={
              <ProtectedRoute auth={auth}>
                <SettingsScreen auth={auth} />
              </ProtectedRoute>
            }
          />
          <Route
            path="*"
            element={
              <ScreenFrame title="Not Found" description="This route does not exist yet.">
                <div className="panel">
                  <p>Head back to the dashboard to keep exploring the shell.</p>
                  <NavLink className="primary-button inline-link" to="/dashboard">
                    Return to Dashboard
                  </NavLink>
                </div>
              </ScreenFrame>
            }
          />
        </Routes>
      </main>

      {auth.isAuthenticated ? (
        <nav className="bottom-nav" aria-label="Primary">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) => (isActive ? "nav-link active" : "nav-link")}
              aria-label={item.label}
            >
              <span className="nav-icon" aria-hidden="true">
                {item.icon}
              </span>
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
      ) : null}
    </div>
  );
}

function useAuthSession(): AuthContext {
  const [session, setSession] = useState<Session | null>(() => readStoredSession());
  const navigate = useNavigate();

  useEffect(() => {
    if (!session) {
      return;
    }

    const expiresAt = Date.parse(session.expiresAtUtc);
    const millisecondsUntilExpiry = expiresAt - Date.now();
    if (millisecondsUntilExpiry <= 0) {
      expireSession(setSession, navigate);
      return;
    }

    const timeoutId = window.setTimeout(
      () => expireSession(setSession, navigate),
      Math.min(millisecondsUntilExpiry, 2_147_483_647)
    );

    return () => window.clearTimeout(timeoutId);
  }, [navigate, session]);

  return useMemo(
    () => ({
      session,
      isAuthenticated: session !== null,
      signIn: async (userName: string, password: string) => {
        const response = await fetch("/auth/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ userName, password })
        });

        if (!response.ok) {
          throw new Error("Invalid username or password.");
        }

        const payload = (await response.json()) as LoginResponse;
        const nextSession: Session = {
          sessionId: payload.sessionId,
          userId: payload.userId,
          userName: payload.userName,
          token: payload.token,
          expiresAtUtc: payload.expiresAtUtc
        };

        window.localStorage.setItem(sessionStorageKey, JSON.stringify(nextSession));
        setSession(nextSession);
      },
      signOut: async () => {
        const token = session?.token;
        if (token) {
          await fetch("/auth/logout", {
            method: "POST",
            headers: { Authorization: `Bearer ${token}` }
          }).catch(() => undefined);
        }

        clearStoredSession();
        setSession(null);
        navigate("/sign-in", { replace: true, state: { message: "You have signed out.", returnTo: "/dashboard" } });
      },
      authenticatedFetch: async (input: RequestInfo | URL, init?: RequestInit) => {
        if (!session) {
          throw new Error("No active session.");
        }

        const headers = new Headers(init?.headers);
        headers.set("Authorization", `Bearer ${session.token}`);

        const response = await fetch(input, { ...init, headers });
        if (response.status === 401) {
          expireSession(setSession, navigate);
        }

        return response;
      }
    }),
    [navigate, session]
  );
}

function useInstallPrompt(): InstallPromptState {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);
  const [isInstalled, setIsInstalled] = useState(() => isStandaloneDisplay());
  const [message, setMessage] = useState(
    isStandaloneDisplay()
      ? "Shrimp Cam is installed and running in standalone mode."
      : "Install Shrimp Cam from your browser menu if the install button is not available."
  );

  useEffect(() => {
    const handleBeforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      setDeferredPrompt(event as BeforeInstallPromptEvent);
      setMessage("Install Shrimp Cam for home-screen launch and standalone display.");
    };

    const handleAppInstalled = () => {
      setDeferredPrompt(null);
      setIsInstalled(true);
      setMessage("Shrimp Cam was installed. Reopen it from your app launcher or home screen.");
    };

    window.addEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
    window.addEventListener("appinstalled", handleAppInstalled);

    return () => {
      window.removeEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
      window.removeEventListener("appinstalled", handleAppInstalled);
    };
  }, []);

  return {
    canPrompt: deferredPrompt !== null,
    isInstalled,
    message,
    promptInstall: async () => {
      if (!deferredPrompt) {
        setMessage(getManualInstallGuidance());
        return;
      }

      await deferredPrompt.prompt();
      const choice = await deferredPrompt.userChoice;
      setDeferredPrompt(null);
      setMessage(
        choice.outcome === "accepted"
          ? "Install accepted. Reopen Shrimp Cam from your app launcher or home screen."
          : getManualInstallGuidance()
      );
    }
  };
}

function useReconnectNotice(isOnline: boolean) {
  const [wasOffline, setWasOffline] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!isOnline) {
      setWasOffline(true);
      setNotice(null);
      return;
    }

    if (wasOffline) {
      setNotice("Connection restored. Refresh a screen to pull the newest camera data.");
      const timeoutId = window.setTimeout(() => setNotice(null), 8_000);
      return () => window.clearTimeout(timeoutId);
    }
  }, [isOnline, wasOffline]);

  return notice;
}

function InstallPromptPanel({ installPrompt }: { installPrompt: InstallPromptState }) {
  return (
    <div className="install-panel">
      <div>
        <p className="eyebrow">Installable PWA</p>
        <strong>{installPrompt.isInstalled ? "Installed" : "Add Shrimp Cam to this device"}</strong>
        <span>{installPrompt.message}</span>
      </div>
      {!installPrompt.isInstalled ? (
        <button
          type="button"
          className={installPrompt.canPrompt ? "primary-button" : "secondary-button"}
          onClick={() => void installPrompt.promptInstall()}
        >
          {installPrompt.canPrompt ? "Install app" : "How to install"}
        </button>
      ) : null}
    </div>
  );
}

function SignInScreen({ auth }: SignInScreenProps) {
  const location = useLocation();
  const navigate = useNavigate();
  const locationState = location.state as LoginState | null;
  const returnTo = locationState?.returnTo ?? "/dashboard";
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (auth.isAuthenticated) {
      navigate(returnTo, { replace: true });
    }
  }, [auth.isAuthenticated, navigate, returnTo]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    if (!userName.trim() || !password) {
      setError("Enter your username and password to continue.");
      return;
    }

    setIsSubmitting(true);
    try {
      await auth.signIn(userName.trim(), password);
      setPassword("");
      navigate(returnTo, { replace: true });
    } catch {
      setPassword("");
      setError("We could not sign you in. Check your username and password, then try again.");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="screen sign-in-screen">
      <div className="screen-header">
        <div>
          <p className="eyebrow">Authentication</p>
          <h2>Sign in to Shrimp Cam</h2>
        </div>
        <p className="screen-copy">Use your local Shrimp Cam account to reach the protected camera workspace.</p>
      </div>
      {locationState?.message ? (
        <div className="banner" role="status">
          {locationState.message}
        </div>
      ) : null}
      <form className="auth-form" onSubmit={(event) => void handleSubmit(event)}>
        <label>
          <span>Username</span>
          <input
            autoComplete="username"
            disabled={isSubmitting}
            name="username"
            onChange={(event) => setUserName(event.target.value)}
            value={userName}
          />
        </label>
        <label>
          <span>Password</span>
          <input
            autoComplete="current-password"
            disabled={isSubmitting}
            name="password"
            onChange={(event) => setPassword(event.target.value)}
            type="password"
            value={password}
          />
        </label>
        {error ? (
          <p className="form-error" role="alert">
            {error}
          </p>
        ) : null}
        <button type="submit" className="primary-button" disabled={isSubmitting}>
          {isSubmitting ? "Signing in..." : "Sign in"}
        </button>
      </form>
    </section>
  );
}

function ProtectedRoute({ auth, children }: ProtectedRouteProps) {
  const location = useLocation();

  if (!auth.isAuthenticated) {
    return (
      <Navigate
        to="/sign-in"
        replace
        state={{
          returnTo: `${location.pathname}${location.search}`,
          message: "Sign in to continue."
        }}
      />
    );
  }

  return <>{children}</>;
}

function OfflineShellPanel({ metadata }: { metadata: CachedShellMetadata | null }) {
  const hasCachedMetadata = Boolean(metadata?.dashboard || metadata?.gallery);

  return (
    <div className="offline-panel" role="status" aria-live="polite">
      <div>
        <p className="eyebrow">Offline Shell</p>
        <h2>{hasCachedMetadata ? "Cached view available" : "Initial online visit required"}</h2>
        <p>
          {hasCachedMetadata
            ? `The app shell is cached. Metadata shown below may be stale from ${formatDateTime(metadata!.cachedAtUtc)}.`
            : "The app shell can load offline after a successful online visit, but this device has no cached dashboard or gallery metadata yet."}
        </p>
      </div>
      {hasCachedMetadata ? (
        <div className="offline-cache-grid">
          <StatCard
            eyebrow="Dashboard Cache"
            value={metadata?.dashboard?.healthStatus ?? "No dashboard"}
            detail={
              metadata?.dashboard?.latestCaptureFileName
                ? `Potentially stale latest capture: ${metadata.dashboard.latestCaptureFileName}`
                : "No cached latest capture yet."
            }
          />
          <StatCard
            eyebrow="Gallery Cache"
            value={`${metadata?.gallery?.captureCount ?? 0} cached`}
            detail={
              metadata?.gallery?.newestCaptureFileName
                ? `Potentially stale newest capture: ${metadata.gallery.newestCaptureFileName}`
                : "No cached gallery metadata yet."
            }
          />
        </div>
      ) : null}
    </div>
  );
}

function DashboardScreen({
  auth,
  onMetadataCached
}: {
  auth: AuthContext;
  onMetadataCached: (metadata: CachedShellMetadata | null) => void;
}) {
  const [dashboard, setDashboard] = useState<DashboardState>({
    health: null,
    latestCapture: null,
    totalCaptures: null,
    healthError: null,
    capturesError: null,
    isLoading: true
  });

  async function loadDashboard() {
    setDashboard((current) => ({ ...current, isLoading: true, healthError: null, capturesError: null }));

    const [healthResult, captureResult] = await Promise.allSettled([
      fetch("/health"),
      auth.authenticatedFetch("/captures?page=1&pageSize=1")
    ]);

    let health: HealthResponse | null = null;
    let latestCapture: CaptureSummary | null = null;
    let totalCaptures: number | null = null;
    let healthError: string | null = null;
    let capturesError: string | null = null;

    if (healthResult.status === "fulfilled" && healthResult.value.ok) {
      health = (await healthResult.value.json()) as HealthResponse;
    } else {
      healthError = "Health data is unavailable. Check diagnostics if this continues.";
    }

    if (captureResult.status === "fulfilled" && captureResult.value.ok) {
      const payload = (await captureResult.value.json()) as CaptureListResponse;
      latestCapture = payload.items[0] ?? null;
      totalCaptures = payload.paging.totalItems;
    } else {
      capturesError = "Capture history is unavailable. Try again after the service reconnects.";
    }

    setDashboard({ health, latestCapture, totalCaptures, healthError, capturesError, isLoading: false });
    if (health || latestCapture || totalCaptures !== null) {
      onMetadataCached(
        updateCachedShellMetadata({
          dashboard: {
            healthStatus: health?.status ?? "Unknown",
            latestCaptureFileName: latestCapture?.fileName ?? null,
            latestCaptureAtUtc: latestCapture?.capturedAtUtc ?? null,
            totalCaptures
          }
        })
      );
    }
  }

  useEffect(() => {
    void loadDashboard();
  }, []);

  const camera = dashboard.health?.components.camera;
  const storage = dashboard.health?.components.storage;
  const nextCapture = getNextCaptureEstimate();

  return (
    <ScreenFrame
      title="Dashboard"
      description="A quick mobile-first overview of camera health, latest capture activity, upcoming capture timing, and storage state."
    >
      <div className="dashboard-toolbar">
        <span>{dashboard.isLoading ? "Refreshing dashboard..." : "Dashboard data loaded"}</span>
        <button type="button" className="secondary-button" onClick={() => void loadDashboard()}>
          Refresh
        </button>
      </div>

      {dashboard.isLoading ? (
        <LoadingSkeleton label="Loading dashboard cards" />
      ) : (
        <div className="stat-grid">
          <StatCard
            eyebrow="Camera"
            value={camera?.status ?? "Unavailable"}
            detail={camera?.detail ?? "Current camera component status."}
          />
          <StatCard
            eyebrow="Latest Capture"
            value={dashboard.latestCapture ? formatRelativeTime(dashboard.latestCapture.capturedAtUtc) : "No captures"}
            detail={
              dashboard.latestCapture
                ? `${dashboard.latestCapture.fileName} from ${dashboard.latestCapture.sourceType}`
                : "Capture history will appear after the first snapshot."
            }
          />
          <StatCard eyebrow="Next Capture" value={nextCapture.value} detail={nextCapture.detail} />
          <StatCard
            eyebrow="Storage"
            value={storage?.status ?? "Unavailable"}
            detail={storage?.detail ?? `${dashboard.totalCaptures ?? 0} captures currently indexed.`}
          />
        </div>
      )}

      <div className="dashboard-grid">
        <article className="snapshot-card">
          <p className="eyebrow">Latest Snapshot</p>
          {dashboard.latestCapture ? (
            <>
              <div className="snapshot-preview" aria-label="Latest snapshot preview">
                <span>{dashboard.latestCapture.fileName}</span>
              </div>
              <p>Captured {formatDateTime(dashboard.latestCapture.capturedAtUtc)}.</p>
              <NavLink className="primary-button inline-link" to={`/gallery?capture=${dashboard.latestCapture.id}`}>
                Open snapshot
              </NavLink>
            </>
          ) : (
            <p>No snapshot is available yet. Take a manual capture from Live View or wait for the next scheduled run.</p>
          )}
        </article>

        <article className="panel stack-gap">
          <p className="eyebrow">Fallbacks</p>
          {dashboard.healthError || dashboard.capturesError ? (
            <>
              {dashboard.healthError ? (
                <p className="form-error" role="alert">
                  {dashboard.healthError}
                </p>
              ) : null}
              {dashboard.capturesError ? (
                <p className="form-error" role="alert">
                  {dashboard.capturesError}
                </p>
              ) : null}
              <p>Use refresh to retry, or open Settings to review system status.</p>
            </>
          ) : (
            <p>All dashboard sections responded. Refresh any time after a capture or reconnect.</p>
          )}
        </article>
      </div>
    </ScreenFrame>
  );
}

function GalleryScreen({
  auth,
  onMetadataCached
}: {
  auth: AuthContext;
  onMetadataCached: (metadata: CachedShellMetadata | null) => void;
}) {
  const location = useLocation();
  const selectedCaptureId = new URLSearchParams(location.search).get("capture");
  const [dateFilter, setDateFilter] = useState("");
  const [gallery, setGallery] = useState<GalleryState>({
    captures: [],
    selectedCapture: null,
    totalItems: 0,
    hasNextPage: false,
    isLoading: true,
    error: null,
    imageFailed: false
  });

  async function loadGallery() {
    setGallery((current) => ({ ...current, isLoading: true, error: null, imageFailed: false }));

    try {
      const query = buildCaptureQuery(dateFilter);
      const response = await auth.authenticatedFetch(`/captures?${query.toString()}`);
      if (!response.ok) {
        throw new Error("Capture history is unavailable.");
      }

      const payload = (await response.json()) as CaptureListResponse;
      const selectedCapture = selectedCaptureId
        ? payload.items.find((capture) => capture.id === selectedCaptureId) ?? null
        : payload.items[0] ?? null;
      const newestCapture = payload.items[0] ?? null;

      setGallery({
        captures: payload.items,
        selectedCapture,
        totalItems: payload.paging.totalItems,
        hasNextPage: payload.paging.hasNextPage,
        isLoading: false,
        error: null,
        imageFailed: false
      });
      onMetadataCached(
        updateCachedShellMetadata({
          gallery: {
            captureCount: payload.paging.totalItems,
            newestCaptureFileName: newestCapture?.fileName ?? null,
            newestCaptureAtUtc: newestCapture?.capturedAtUtc ?? null
          }
        })
      );
    } catch {
      setGallery((current) => ({
        ...current,
        isLoading: false,
        error: "We could not load captures. Check the connection and try again."
      }));
    }
  }

  useEffect(() => {
    void loadGallery();
  }, [dateFilter, selectedCaptureId]);

  function selectCapture(capture: CaptureSummary) {
    setGallery((current) => ({ ...current, selectedCapture: capture, imageFailed: false }));
  }

  function clearFilter() {
    setDateFilter("");
  }

  const emptyState = !gallery.isLoading && !gallery.error && gallery.captures.length === 0;

  return (
    <ScreenFrame
      title="Gallery"
      description="Reverse-chronological capture browsing with date filters and a focused mobile viewer."
    >
      <div className="gallery-layout">
        <section className="gallery-browser" aria-label="Capture browser">
          <div className="gallery-toolbar">
            <label>
              <span>Filter by day</span>
              <input
                type="date"
                value={dateFilter}
                onChange={(event) => setDateFilter(event.target.value)}
                aria-label="Filter captures by day"
              />
            </label>
            <button type="button" className="secondary-button" disabled={!dateFilter} onClick={clearFilter}>
              Clear filter
            </button>
          </div>

          <div className="gallery-summary" role="status" aria-live="polite">
            {gallery.isLoading
              ? "Loading captures..."
              : `${gallery.totalItems} capture${gallery.totalItems === 1 ? "" : "s"} found${
                  dateFilter ? ` for ${formatFilterDate(dateFilter)}` : ""
                }.`}
          </div>

          {gallery.error ? (
            <p className="form-error" role="alert">
              {gallery.error}
            </p>
          ) : null}

          {gallery.isLoading ? <LoadingSkeleton label="Loading capture list" compact /> : null}

          {emptyState ? (
            <div className="empty-state">
              <strong>No captures found</strong>
              <span>Try a different day, clear the filter, or take a manual snapshot from Live View.</span>
              <button type="button" className="secondary-button" disabled={!dateFilter} onClick={clearFilter}>
                Clear date filter
              </button>
            </div>
          ) : null}

          <div className="capture-list">
            {!gallery.isLoading ? gallery.captures.map((capture) => (
              <button
                type="button"
                key={capture.id}
                className={gallery.selectedCapture?.id === capture.id ? "capture-list-item active" : "capture-list-item"}
                onClick={() => selectCapture(capture)}
                aria-pressed={gallery.selectedCapture?.id === capture.id}
              >
                <span>{formatDateTime(capture.capturedAtUtc)}</span>
                <strong>{capture.fileName}</strong>
                <small>{capture.sourceType}</small>
              </button>
            )) : null}
          </div>

          {gallery.hasNextPage ? (
            <p className="support-copy">Showing the newest 25 captures. Narrow by day to focus the review.</p>
          ) : null}
        </section>

        <section className="viewer-card" aria-label="Focused capture viewer">
          {gallery.selectedCapture ? (
            <>
              <div className="viewer-frame">
                {gallery.imageFailed ? (
                  <div className="stream-fallback" role="alert">
                    <strong>Image unavailable</strong>
                    <span>The capture metadata loaded, but the image file could not be displayed.</span>
                  </div>
                ) : null}
                <img
                  alt={`Shrimp tank capture from ${formatDateTime(gallery.selectedCapture.capturedAtUtc)}`}
                  onError={() => setGallery((current) => ({ ...current, imageFailed: true }))}
                  src={gallery.selectedCapture.imageUrl}
                />
              </div>
              <div className="viewer-details">
                <p className="eyebrow">Focused Viewer</p>
                <h3>{gallery.selectedCapture.fileName}</h3>
                <p>Captured {formatDateTime(gallery.selectedCapture.capturedAtUtc)}.</p>
                <div className="action-row">
                  <a className="primary-button inline-link" href={gallery.selectedCapture.imageUrl} target="_blank" rel="noreferrer">
                    Open image
                  </a>
                  <a className="secondary-button inline-link" href={gallery.selectedCapture.metadataUrl} target="_blank" rel="noreferrer">
                    Metadata
                  </a>
                </div>
              </div>
            </>
          ) : (
            <div className="empty-state">
              <strong>Select a capture</strong>
              <span>Captured images open here with timestamp and navigation controls.</span>
            </div>
          )}
        </section>
      </div>
    </ScreenFrame>
  );
}

function LiveViewScreen({ auth }: { auth: AuthContext }) {
  const [streamVersion, setStreamVersion] = useState(1);
  const [streamStatus, setStreamStatus] = useState<LiveStreamStatus>("connecting");
  const [isCapturing, setIsCapturing] = useState(false);
  const [lastCaptureAtUtc, setLastCaptureAtUtc] = useState<string | null>(null);
  const [lastCaptureFileName, setLastCaptureFileName] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>("Connecting to the camera stream...");
  const streamUnavailable = streamStatus === "offline";

  function retryStream() {
    setStreamStatus("connecting");
    setMessage("Reconnecting to the camera stream...");
    setStreamVersion((current) => current + 1);
  }

  async function captureSnapshot() {
    setIsCapturing(true);
    setMessage("Capturing a manual snapshot...");

    try {
      const response = await auth.authenticatedFetch("/captures/manual", { method: "POST" });
      const payload = (await response.json().catch(() => null)) as ManualCaptureResponse | null;

      if (!response.ok || payload?.status === "failed") {
        throw new Error(payload?.reason ?? "The camera could not capture a snapshot.");
      }

      const capturedAtUtc = payload?.capturedAtUtc ?? new Date().toISOString();
      setLastCaptureAtUtc(capturedAtUtc);
      setLastCaptureFileName(payload?.fileName ?? null);
      setMessage("Snapshot captured. Gallery history will include the new still image.");
    } catch (error) {
      const detail = error instanceof Error ? error.message : "Try again after checking camera status.";
      setMessage(`Manual snapshot failed. ${detail}`);
    } finally {
      setIsCapturing(false);
    }
  }

  return (
    <ScreenFrame
      title="Live"
      description="A mobile-friendly camera feed with stream status, reconnect controls, and manual snapshot capture."
    >
      <div className="live-layout">
        <article className="stream-card">
          <div className="stream-header">
            <div>
              <p className="eyebrow">Camera Feed</p>
              <strong>{streamStatus === "online" ? "Live stream online" : "Waiting for stream"}</strong>
            </div>
            <span className={`status-pill ${streamStatus}`}>{streamStatus}</span>
          </div>

          <div className={`stream-frame ${streamUnavailable ? "offline" : ""}`}>
            {streamUnavailable ? (
              <div className="stream-fallback" role="status">
                <strong>Stream unavailable</strong>
                <span>Retry the stream or check camera status if the Logitech webcam is disconnected.</span>
              </div>
            ) : null}
            <img
              alt="Live shrimp tank camera feed"
              onError={() => {
                setStreamStatus("offline");
                setMessage("The live stream is unavailable. Retry the stream or check device status.");
              }}
              onLoad={() => {
                setStreamStatus("online");
                setMessage("Live stream is online.");
              }}
              src={`/stream/live?view=${streamVersion}`}
            />
          </div>

          <div className="action-row">
            <button type="button" className="secondary-button" onClick={retryStream}>
              Retry stream
            </button>
            <span className="support-copy">If the stream drops, Shrimp Cam keeps the shell usable while you reconnect.</span>
          </div>
        </article>

        <article className="capture-card">
          <p className="eyebrow">Manual Snapshot</p>
          <h3>Capture the moment</h3>
          <p>
            Take a still image from the current camera source. The timestamp updates here after a successful capture.
          </p>
          <button
            type="button"
            className="primary-button"
            disabled={isCapturing || streamUnavailable}
            onClick={() => void captureSnapshot()}
          >
            {isCapturing ? "Capturing..." : "Capture snapshot"}
          </button>
          <dl className="capture-facts">
            <div>
              <dt>Last manual capture</dt>
              <dd>{lastCaptureAtUtc ? formatDateTime(lastCaptureAtUtc) : "Not captured this session"}</dd>
            </div>
            <div>
              <dt>Saved file</dt>
              <dd>{lastCaptureFileName ?? "Waiting for snapshot"}</dd>
            </div>
          </dl>
          {message ? (
            <p className="live-message" role={message.includes("failed") || streamUnavailable ? "alert" : "status"} aria-live="polite">
              {message}
            </p>
          ) : null}
        </article>
      </div>
    </ScreenFrame>
  );
}

function SettingsScreen({ auth }: { auth: AuthContext }) {
  const [state, setState] = useState<SettingsState>({
    form: null,
    health: null,
    isLoading: true,
    isSaving: false,
    isDirty: false,
    message: null,
    errors: {}
  });

  async function loadSettings() {
    setState((current) => ({ ...current, isLoading: true, message: null, errors: {} }));

    const [settingsResult, healthResult] = await Promise.allSettled([
      auth.authenticatedFetch("/settings"),
      fetch("/health")
    ]);

    let form: SettingsResponse | null = null;
    let health: HealthResponse | null = null;
    let message: string | null = null;

    if (settingsResult.status === "fulfilled" && settingsResult.value.ok) {
      form = (await settingsResult.value.json()) as SettingsResponse;
    } else if (settingsResult.status === "fulfilled" && settingsResult.value.status === 403) {
      message = "Your account can view protected app areas, but only administrators can edit settings.";
    } else {
      message = "Settings are unavailable. Sign in as an administrator or retry after the service reconnects.";
    }

    if (healthResult.status === "fulfilled" && healthResult.value.ok) {
      health = (await healthResult.value.json()) as HealthResponse;
    }

    setState({
      form,
      health,
      isLoading: false,
      isSaving: false,
      isDirty: false,
      message,
      errors: {}
    });
  }

  useEffect(() => {
    void loadSettings();
  }, []);

  function updateForm(mutator: (current: SettingsResponse) => SettingsResponse) {
    setState((current) =>
      current.form
        ? {
            ...current,
            form: mutator(current.form),
            isDirty: true,
            message: null,
            errors: {}
          }
        : current
    );
  }

  async function saveSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!state.form) {
      return;
    }

    const clientErrors = validateSettings(state.form);
    if (Object.keys(clientErrors).length > 0) {
      setState((current) => ({
        ...current,
        errors: clientErrors,
        message: "Fix the highlighted settings before saving.",
        isSaving: false
      }));
      return;
    }

    setState((current) => ({ ...current, isSaving: true, message: "Saving settings...", errors: {} }));

    try {
      const response = await auth.authenticatedFetch("/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(state.form)
      });

      if (response.status === 403) {
        setState((current) => ({
          ...current,
          isSaving: false,
          message: "Only administrators can update Shrimp Cam settings."
        }));
        return;
      }

      if (!response.ok) {
        const payload = (await response.json().catch(() => null)) as ValidationProblemResponse | null;
        setState((current) => ({
          ...current,
          isSaving: false,
          errors: flattenValidationErrors(payload?.errors),
          message: "The server rejected one or more settings. Your edits were preserved."
        }));
        return;
      }

      const saved = (await response.json()) as SettingsResponse;
      setState((current) => ({
        ...current,
        form: saved,
        isSaving: false,
        isDirty: false,
        message: "Settings saved. Refreshed values match the service response.",
        errors: {}
      }));
    } catch {
      setState((current) => ({
        ...current,
        isSaving: false,
        message: "Settings could not be saved. Check the connection and try again."
      }));
    }
  }

  const form = state.form;
  const camera = state.health?.components.camera;
  const storage = state.health?.components.storage;
  const database = state.health?.components.database;

  return (
    <ScreenFrame
      title="Settings"
      description="Review system health and safely update capture, camera, retention, and hosting preferences."
    >
      <div className="settings-layout">
        <aside className="status-panel">
          <div className="dashboard-toolbar">
            <span>{state.isLoading ? "Loading settings..." : "Settings status loaded"}</span>
            <button type="button" className="secondary-button" onClick={() => void loadSettings()}>
              Refresh
            </button>
          </div>
          <div className="stat-grid">
            {state.isLoading ? (
              <LoadingSkeleton label="Loading system status" compact />
            ) : (
              <>
                <StatCard eyebrow="App" value={state.health?.status ?? "Unknown"} detail={state.health?.checkedAtUtc ? `Checked ${formatDateTime(state.health.checkedAtUtc)}` : "Health check has not loaded."} />
                <StatCard eyebrow="Camera" value={camera?.status ?? "Unknown"} detail={camera?.detail ?? "Camera status unavailable."} />
                <StatCard eyebrow="Storage" value={storage?.status ?? "Unknown"} detail={storage?.detail ?? "Storage status unavailable."} />
                <StatCard eyebrow="Database" value={database?.status ?? "Unknown"} detail={database?.detail ?? "Database status unavailable."} />
              </>
            )}
          </div>
        </aside>

        {form ? (
          <form className="settings-form" onSubmit={(event) => void saveSettings(event)}>
            <div className="settings-form-header">
              <div>
                <p className="eyebrow">Editable Settings</p>
                <h3>{state.isDirty ? "Unsaved changes" : "Current configuration"}</h3>
              </div>
              <button type="submit" className="primary-button" disabled={state.isSaving || !state.isDirty}>
                {state.isSaving ? "Saving..." : "Save settings"}
              </button>
            </div>

            <fieldset>
              <legend>Capture Schedule</legend>
              <label>
                <span>Capture enabled</span>
                <input
                  type="checkbox"
                  checked={form.capture.enabled}
                  onChange={(event) =>
                    updateForm((current) => ({
                      ...current,
                      capture: { ...current.capture, enabled: event.target.checked }
                    }))
                  }
                />
              </label>
              <label>
                <span>Interval minutes</span>
                <input
                  type="number"
                  min="1"
                  max="1440"
                  value={form.capture.intervalMinutes}
                  onChange={(event) =>
                    updateForm((current) => ({
                      ...current,
                      capture: { ...current.capture, intervalMinutes: toInteger(event.target.value) }
                    }))
                  }
                />
                <FieldError message={state.errors["capture.intervalMinutes"]} />
              </label>
              <div className="settings-grid">
                <label>
                  <span>Active start UTC</span>
                  <input
                    type="number"
                    min="0"
                    max="23"
                    value={form.capture.activeStartHourUtc}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        capture: { ...current.capture, activeStartHourUtc: toInteger(event.target.value) }
                      }))
                    }
                  />
                  <FieldError message={state.errors["capture.activeStartHourUtc"]} />
                </label>
                <label>
                  <span>Active end UTC</span>
                  <input
                    type="number"
                    min="1"
                    max="24"
                    value={form.capture.activeEndHourUtc}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        capture: { ...current.capture, activeEndHourUtc: toInteger(event.target.value) }
                      }))
                    }
                  />
                  <FieldError message={state.errors["capture.activeEndHourUtc"]} />
                </label>
              </div>
            </fieldset>

            <fieldset>
              <legend>Camera And Storage</legend>
              <label>
                <span>Camera source</span>
                <input
                  value={form.camera.source}
                  onChange={(event) =>
                    updateForm((current) => ({
                      ...current,
                      camera: { ...current.camera, source: event.target.value }
                    }))
                  }
                />
                <FieldError message={state.errors["camera.source"]} />
              </label>
              <div className="settings-grid">
                <label>
                  <span>Platform</span>
                  <select
                    value={form.camera.platform}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        camera: { ...current.camera, platform: event.target.value }
                      }))
                    }
                  >
                    <option value="Windows">Windows</option>
                    <option value="Linux">Linux</option>
                  </select>
                </label>
                <label>
                  <span>Retention days</span>
                  <input
                    type="number"
                    min="1"
                    max="3650"
                    value={form.storage.retentionDays}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        storage: { ...current.storage, retentionDays: toInteger(event.target.value) }
                      }))
                    }
                  />
                  <FieldError message={state.errors["storage.retentionDays"]} />
                </label>
              </div>
            </fieldset>

            <fieldset>
              <legend>Motion And Hosting</legend>
              <label>
                <span>Motion highlights</span>
                <input
                  type="checkbox"
                  checked={form.capture.motionHighlightsEnabled}
                  onChange={(event) =>
                    updateForm((current) => ({
                      ...current,
                      capture: { ...current.capture, motionHighlightsEnabled: event.target.checked }
                    }))
                  }
                />
              </label>
              <div className="settings-grid">
                <label>
                  <span>Motion threshold</span>
                  <input
                    type="number"
                    min="0.01"
                    max="1"
                    step="0.01"
                    value={form.capture.motionThreshold}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        capture: { ...current.capture, motionThreshold: Number(event.target.value) }
                      }))
                    }
                  />
                  <FieldError message={state.errors["capture.motionThreshold"]} />
                </label>
                <label>
                  <span>Host mode</span>
                  <input
                    value={form.security.hostMode}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        security: { ...current.security, hostMode: event.target.value }
                      }))
                    }
                  />
                </label>
              </div>
            </fieldset>

            {state.message ? (
              <p className="live-message" role={Object.keys(state.errors).length > 0 ? "alert" : "status"} aria-live="polite">
                {state.message}
              </p>
            ) : null}
          </form>
        ) : (
          <div className="empty-state">
            <strong>Settings unavailable</strong>
            <span>{state.message ?? "Sign in as an administrator to edit Shrimp Cam settings."}</span>
          </div>
        )}
      </div>
    </ScreenFrame>
  );
}

function ScreenFrame({ title, description, children }: ScreenFrameProps) {
  return (
    <section className="screen">
      <div className="screen-header">
        <div>
          <p className="eyebrow">Route</p>
          <h2>{title}</h2>
        </div>
        <p className="screen-copy">{description}</p>
      </div>
      {children}
    </section>
  );
}

function StatCard({ eyebrow, value, detail }: StatCardProps) {
  return (
    <article className="stat-card">
      <p className="eyebrow">{eyebrow}</p>
      <strong>{value}</strong>
      <span>{detail}</span>
    </article>
  );
}

function LoadingSkeleton({ label, compact = false }: { label: string; compact?: boolean }) {
  return (
    <div className={compact ? "loading-skeleton compact" : "loading-skeleton"} role="status" aria-label={label}>
      <span />
      <span />
      <span />
    </div>
  );
}

function FieldError({ message }: { message?: string }) {
  return message ? (
    <span className="field-error" role="alert">
      {message}
    </span>
  ) : null;
}

function validateSettings(settings: SettingsResponse) {
  const errors: Record<string, string> = {};

  if (!settings.camera.source.trim()) {
    errors["camera.source"] = "Camera source is required.";
  }

  if (settings.capture.intervalMinutes < 1 || settings.capture.intervalMinutes > 1440) {
    errors["capture.intervalMinutes"] = "Interval must be between 1 and 1440 minutes.";
  }

  if (settings.capture.activeStartHourUtc < 0 || settings.capture.activeStartHourUtc > 23) {
    errors["capture.activeStartHourUtc"] = "Start hour must be between 0 and 23.";
  }

  if (settings.capture.activeEndHourUtc < 1 || settings.capture.activeEndHourUtc > 24) {
    errors["capture.activeEndHourUtc"] = "End hour must be between 1 and 24.";
  }

  if (settings.capture.motionThreshold < 0.01 || settings.capture.motionThreshold > 1) {
    errors["capture.motionThreshold"] = "Motion threshold must be between 0.01 and 1.";
  }

  if (settings.storage.retentionDays < 1 || settings.storage.retentionDays > 3650) {
    errors["storage.retentionDays"] = "Retention must be between 1 and 3650 days.";
  }

  return errors;
}

function flattenValidationErrors(errors?: Record<string, string[]>) {
  if (!errors) {
    return {};
  }

  return Object.fromEntries(Object.entries(errors).map(([key, messages]) => [key, messages[0] ?? "Validation failed."]));
}

function toInteger(value: string) {
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? 0 : parsed;
}

function isStandaloneDisplay() {
  const navigatorWithStandalone = window.navigator as Navigator & { standalone?: boolean };
  return window.matchMedia("(display-mode: standalone)").matches || navigatorWithStandalone.standalone === true;
}

function getManualInstallGuidance() {
  const isAppleTouchBrowser = /iphone|ipad|ipod/i.test(window.navigator.userAgent);
  return isAppleTouchBrowser
    ? "Use Share, then Add to Home Screen to install Shrimp Cam on this device."
    : "Use your browser menu and choose Install app or Add to Home screen when supported.";
}

function readStoredSession(): Session | null {
  const stored = window.localStorage.getItem(sessionStorageKey);
  if (!stored) {
    return null;
  }

  try {
    const session = JSON.parse(stored) as Session;
    if (!session.token || Date.parse(session.expiresAtUtc) <= Date.now()) {
      clearStoredSession();
      return null;
    }

    return session;
  } catch {
    clearStoredSession();
    return null;
  }
}

function expireSession(setSession: (session: Session | null) => void, navigate: NavigateFunction) {
  clearStoredSession();
  setSession(null);
  navigate("/sign-in", {
    replace: true,
    state: { message: "Your session expired. Sign in again to continue.", returnTo: "/dashboard" }
  });
}

function clearStoredSession() {
  window.localStorage.removeItem(sessionStorageKey);
}

function readCachedShellMetadata(): CachedShellMetadata | null {
  const stored = window.localStorage.getItem(shellMetadataStorageKey);
  if (!stored) {
    return null;
  }

  try {
    const metadata = JSON.parse(stored) as CachedShellMetadata;
    return metadata.cachedAtUtc ? metadata : null;
  } catch {
    window.localStorage.removeItem(shellMetadataStorageKey);
    return null;
  }
}

function updateCachedShellMetadata(update: Partial<Omit<CachedShellMetadata, "cachedAtUtc">>) {
  const next: CachedShellMetadata = {
    ...(readCachedShellMetadata() ?? { cachedAtUtc: new Date().toISOString() }),
    ...update,
    cachedAtUtc: new Date().toISOString()
  };

  window.localStorage.setItem(shellMetadataStorageKey, JSON.stringify(next));
  return next;
}

function getNextCaptureEstimate() {
  const now = new Date();
  const next = new Date(now);
  const minutes = next.getMinutes();
  const remainder = minutes % 5;
  const minutesToAdd = remainder === 0 ? 5 : 5 - remainder;
  next.setMinutes(minutes + minutesToAdd, 0, 0);

  return {
    value: formatDateTime(next.toISOString()),
    detail: "Estimated from the default five-minute interval until schedule settings are wired into the dashboard."
  };
}

function buildCaptureQuery(dateFilter: string) {
  const query = new URLSearchParams({ page: "1", pageSize: "25" });
  if (!dateFilter) {
    return query;
  }

  const fromUtc = new Date(`${dateFilter}T00:00:00`);
  const toUtc = new Date(`${dateFilter}T23:59:59.999`);
  query.set("fromUtc", fromUtc.toISOString());
  query.set("toUtc", toUtc.toISOString());
  return query;
}

function formatFilterDate(value: string) {
  const date = new Date(`${value}T00:00:00`);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric"
  }).format(date);
}

function formatRelativeTime(value: string) {
  const timestamp = Date.parse(value);
  if (Number.isNaN(timestamp)) {
    return "Unknown";
  }

  const minutesAgo = Math.max(0, Math.round((Date.now() - timestamp) / 60_000));
  if (minutesAgo < 1) {
    return "Just now";
  }

  if (minutesAgo < 60) {
    return `${minutesAgo} min ago`;
  }

  const hoursAgo = Math.round(minutesAgo / 60);
  return `${hoursAgo} hr ago`;
}

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Unknown";
  }

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(date);
}

export default App;

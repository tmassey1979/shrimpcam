import { useEffect, useMemo, useState } from "react";
import type { CSSProperties, FormEvent, ReactNode } from "react";
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
  latestImageUrl: string | null;
  totalCaptures: number | null;
  healthError: string | null;
  capturesError: string | null;
  imageFailed: boolean;
  isImageLoading: boolean;
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
  selectedImageUrl: string | null;
  totalItems: number;
  hasNextPage: boolean;
  isLoading: boolean;
  isImageLoading: boolean;
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
    alwaysOnStreamEnabled: boolean;
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

type CameraDiscoveryResponse = {
  platform: string;
  cameras: CameraOption[];
};

type CameraOption = {
  displayName: string;
  devicePath: string;
  platform: string;
};

type SettingsState = {
  form: SettingsResponse | null;
  health: HealthResponse | null;
  cameras: CameraOption[];
  isLoading: boolean;
  isCameraDiscoveryLoading: boolean;
  isSaving: boolean;
  isDirty: boolean;
  message: string | null;
  cameraDiscoveryMessage: string | null;
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
  browserName: string;
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
  { to: "/dashboard", label: "Dashboard", icon: "Home" },
  { to: "/live", label: "Live", icon: "Cam" },
  { to: "/gallery", label: "Gallery", icon: "Shot" },
  { to: "/settings", label: "Settings", icon: "Tune" }
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
    <div className={auth.isAuthenticated ? "app-shell app-shell-authenticated" : "app-shell"}>
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>
      <header className="app-header">
        <div className="brand-lockup">
          <span className="brand-mark" aria-hidden="true">
            <span className="shrimp-mark-body" />
            <span className="shrimp-mark-tail" />
          </span>
          <div className="brand-copy">
            <p className="eyebrow">Your tank. Always in view.</p>
            <h1>Shrimp Cam</h1>
            <span className="brand-subtitle">Timelapse, live view, and tank status</span>
          </div>
        </div>
        <div className="topbar-actions" aria-label="Connection and session status">
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

        {!auth.isAuthenticated ? (
          <section className="shell-status-card" aria-label="Session and install status">
            <div>
              <p className="eyebrow">Sign-in required</p>
              <p>{shellMessage}</p>
            </div>
            {!installPrompt.isInstalled ? <InstallPromptPanel installPrompt={installPrompt} /> : null}
          </section>
        ) : null}

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
      : getManualInstallGuidance()
  );

  useEffect(() => {
    const standaloneQuery = window.matchMedia("(display-mode: standalone)");
    const handleDisplayModeChange = () => {
      const installed = isStandaloneDisplay();
      setIsInstalled(installed);
      if (!installed) {
        setMessage(getManualInstallGuidance());
      }
    };

    const handleBeforeInstallPrompt = (event: Event) => {
      event.preventDefault();
      setDeferredPrompt(event as BeforeInstallPromptEvent);
      setIsInstalled(false);
      setMessage("Install Shrimp Cam for home-screen launch and standalone display.");
    };

    const handleAppInstalled = () => {
      setDeferredPrompt(null);
      setIsInstalled(true);
      setMessage("Shrimp Cam was installed. Reopen it from your app launcher or home screen.");
    };

    standaloneQuery.addEventListener("change", handleDisplayModeChange);
    window.addEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
    window.addEventListener("appinstalled", handleAppInstalled);
    window.addEventListener("focus", handleDisplayModeChange);
    window.addEventListener("pageshow", handleDisplayModeChange);
    document.addEventListener("visibilitychange", handleDisplayModeChange);

    return () => {
      standaloneQuery.removeEventListener("change", handleDisplayModeChange);
      window.removeEventListener("beforeinstallprompt", handleBeforeInstallPrompt);
      window.removeEventListener("appinstalled", handleAppInstalled);
      window.removeEventListener("focus", handleDisplayModeChange);
      window.removeEventListener("pageshow", handleDisplayModeChange);
      document.removeEventListener("visibilitychange", handleDisplayModeChange);
    };
  }, []);

  return {
    canPrompt: deferredPrompt !== null,
    isInstalled,
    message,
    browserName: getBrowserName(),
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
  if (installPrompt.isInstalled) {
    return null;
  }

  return (
    <div className="install-panel">
      <div>
        <p className="eyebrow">{installPrompt.browserName} install</p>
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
    latestImageUrl: null,
    totalCaptures: null,
    healthError: null,
    capturesError: null,
    imageFailed: false,
    isImageLoading: false,
    isLoading: true
  });

  async function loadDashboard() {
    setDashboard((current) => ({
      ...current,
      isLoading: true,
      healthError: null,
      capturesError: null,
      imageFailed: false
    }));

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

    setDashboard((current) => ({
      ...current,
      health,
      latestCapture,
      totalCaptures,
      healthError,
      capturesError,
      isLoading: false
    }));
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

  useEffect(() => {
    if (!dashboard.latestCapture) {
      setDashboard((current) => ({
        ...current,
        latestImageUrl: null,
        isImageLoading: false,
        imageFailed: false
      }));
      return;
    }

    let revoked = false;
    let objectUrl: string | null = null;
    setDashboard((current) => ({
      ...current,
      latestImageUrl: null,
      isImageLoading: true,
      imageFailed: false
    }));

    async function loadLatestImage(capture: CaptureSummary) {
      try {
        const response = await auth.authenticatedFetch(capture.imageUrl);
        if (!response.ok) {
          throw new Error("Latest capture image request failed.");
        }

        const blob = await response.blob();
        objectUrl = URL.createObjectURL(blob);
        if (revoked) {
          URL.revokeObjectURL(objectUrl);
          return;
        }

        setDashboard((current) => ({
          ...current,
          latestImageUrl: objectUrl,
          isImageLoading: false,
          imageFailed: false
        }));
      } catch {
        if (!revoked) {
          setDashboard((current) => ({
            ...current,
            latestImageUrl: null,
            isImageLoading: false,
            imageFailed: true
          }));
        }
      }
    }

    void loadLatestImage(dashboard.latestCapture);

    return () => {
      revoked = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [auth, dashboard.latestCapture?.id]);

  const camera = dashboard.health?.components.camera;
  const storage = dashboard.health?.components.storage;
  const nextCapture = getNextCaptureEstimate();
  const storageUsage = getDashboardStorageUsage(dashboard.totalCaptures);

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
        <div className="dashboard-overview" aria-label="Dashboard overview cards">
          <StatCard
            eyebrow="Camera Status"
            value={camera?.status ?? "Unavailable"}
            detail={camera?.detail ?? "Current camera component status."}
          />
          <StatCard eyebrow="Next Timelapse" value={nextCapture.value} detail={nextCapture.detail} />
          <article className="storage-usage-card" aria-label="Storage usage">
            <div
              className="storage-ring"
              style={{ "--storage-progress": `${storageUsage.percent}%` } as CSSProperties}
              aria-label={`${storageUsage.percent}% capture index usage`}
            >
              <strong>{storageUsage.percent}%</strong>
              <span>Used</span>
            </div>
            <div className="storage-copy">
              <p className="eyebrow">Storage Usage</p>
              <h3>{storage?.status ?? "Unavailable"}</h3>
              <p>{storage?.detail ?? `${dashboard.totalCaptures ?? 0} captures currently indexed.`}</p>
              <div className="storage-meter" aria-hidden="true">
                <span style={{ width: `${storageUsage.percent}%` }} />
              </div>
              <small>{storageUsage.detail}</small>
            </div>
          </article>
        </div>
      )}

      <div className="dashboard-grid">
        <article className="snapshot-card">
          <p className="eyebrow">Latest Snapshot</p>
          {dashboard.latestCapture ? (
            <>
              <div className="snapshot-preview" aria-label="Latest snapshot preview">
                {dashboard.isImageLoading ? <span role="status">Loading protected snapshot...</span> : null}
                {dashboard.imageFailed ? <span role="alert">Protected snapshot image could not be displayed.</span> : null}
                {dashboard.latestImageUrl ? (
                  <img
                    src={dashboard.latestImageUrl}
                    alt={`Latest shrimp tank snapshot from ${formatDateTime(dashboard.latestCapture.capturedAtUtc)}`}
                    onError={() => setDashboard((current) => ({ ...current, imageFailed: true }))}
                  />
                ) : null}
                {!dashboard.latestImageUrl && !dashboard.isImageLoading && !dashboard.imageFailed ? (
                  <span>{dashboard.latestCapture.fileName}</span>
                ) : null}
              </div>
              <p className="snapshot-file-name">{dashboard.latestCapture.fileName}</p>
              <p>Captured {formatDateTime(dashboard.latestCapture.capturedAtUtc)}.</p>
              <NavLink className="primary-button inline-link" to={`/gallery?capture=${dashboard.latestCapture.id}`}>
                Open snapshot
              </NavLink>
            </>
          ) : (
            <p>No snapshot is available yet. Take a manual capture from Live View or wait for the next scheduled run.</p>
          )}
        </article>

        <article className="quick-actions-card">
          <div className="quick-actions-header">
            <p className="eyebrow">Quick Actions</p>
            <span>Reach the core tank workflows fast.</span>
          </div>
          <div className="quick-action-grid">
            <NavLink className="quick-action-tile live" to="/live">
              <span aria-hidden="true">Cam</span>
              <strong>Live View</strong>
              <small>Watch your tank in real time</small>
            </NavLink>
            <NavLink className="quick-action-tile gallery" to="/gallery">
              <span aria-hidden="true">Shot</span>
              <strong>Gallery</strong>
              <small>Browse snapshots and timelapses</small>
            </NavLink>
            <NavLink className="quick-action-tile capture" to="/live">
              <span aria-hidden="true">Snap</span>
              <strong>Capture Now</strong>
              <small>Take a photo from Live View</small>
            </NavLink>
          </div>
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
  const [searchText, setSearchText] = useState("");
  const [sourceFilter, setSourceFilter] = useState("All");
  const [timeFilter, setTimeFilter] = useState("All");
  const [gallery, setGallery] = useState<GalleryState>({
    captures: [],
    selectedCapture: null,
    selectedImageUrl: null,
    totalItems: 0,
    hasNextPage: false,
    isLoading: true,
    isImageLoading: false,
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
        selectedImageUrl: null,
        totalItems: payload.paging.totalItems,
        hasNextPage: payload.paging.hasNextPage,
        isLoading: false,
        isImageLoading: false,
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

  useEffect(() => {
    if (!gallery.selectedCapture) {
      setGallery((current) => ({ ...current, selectedImageUrl: null, isImageLoading: false }));
      return;
    }

    let revoked = false;
    let objectUrl: string | null = null;
    setGallery((current) => ({ ...current, selectedImageUrl: null, isImageLoading: true, imageFailed: false }));

    async function loadSelectedImage(capture: CaptureSummary) {
      try {
        const response = await auth.authenticatedFetch(capture.imageUrl);
        if (!response.ok) {
          throw new Error("Capture image request failed.");
        }

        const blob = await response.blob();
        objectUrl = URL.createObjectURL(blob);
        if (revoked) {
          URL.revokeObjectURL(objectUrl);
          return;
        }

        setGallery((current) => ({ ...current, selectedImageUrl: objectUrl, isImageLoading: false, imageFailed: false }));
      } catch {
        if (!revoked) {
          setGallery((current) => ({ ...current, selectedImageUrl: null, isImageLoading: false, imageFailed: true }));
        }
      }
    }

    void loadSelectedImage(gallery.selectedCapture);

    return () => {
      revoked = true;
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [auth, gallery.selectedCapture?.id]);

  function selectCapture(capture: CaptureSummary) {
    setGallery((current) => ({ ...current, selectedCapture: capture, selectedImageUrl: null, imageFailed: false }));
  }

  function clearFilter() {
    setDateFilter("");
    setSearchText("");
    setSourceFilter("All");
    setTimeFilter("All");
  }

  const visibleCaptures = gallery.captures.filter((capture) => {
    const normalizedSearch = searchText.trim().toLowerCase();
    const matchesSearch = normalizedSearch.length === 0
      || capture.fileName.toLowerCase().includes(normalizedSearch)
      || capture.sourceType.toLowerCase().includes(normalizedSearch);
    const matchesSource = sourceFilter === "All" || capture.sourceType === sourceFilter;
    const matchesTime = timeFilter === "All" || getCaptureTimeBucket(capture.capturedAtUtc) === timeFilter;

    return matchesSearch && matchesSource && matchesTime;
  });
  const selectedVisibleCapture = gallery.selectedCapture && visibleCaptures.some((capture) => capture.id === gallery.selectedCapture?.id)
    ? gallery.selectedCapture
    : visibleCaptures[0] ?? null;
  const emptyState = !gallery.isLoading && !gallery.error && gallery.captures.length === 0;
  const emptyFilteredState = !gallery.isLoading && !gallery.error && gallery.captures.length > 0 && visibleCaptures.length === 0;
  const hasGalleryFilters = Boolean(dateFilter || searchText.trim() || sourceFilter !== "All" || timeFilter !== "All");
  const timelineDays = Array.from(
    new Map(
      gallery.captures.map((capture) => [
        new Date(capture.capturedAtUtc).toISOString().slice(0, 10),
        formatFilterDate(new Date(capture.capturedAtUtc).toISOString().slice(0, 10))
      ])
    ).entries()
  );
  const captureSources = Array.from(new Set(gallery.captures.map((capture) => capture.sourceType)));
  const timeBuckets = Array.from(new Set(gallery.captures.map((capture) => getCaptureTimeBucket(capture.capturedAtUtc))));

  useEffect(() => {
    if (gallery.isLoading || visibleCaptures.length === 0) {
      return;
    }

    if (!gallery.selectedCapture || !visibleCaptures.some((capture) => capture.id === gallery.selectedCapture?.id)) {
      setGallery((current) => ({
        ...current,
        selectedCapture: visibleCaptures[0],
        selectedImageUrl: null,
        imageFailed: false
      }));
    }
  }, [gallery.isLoading, gallery.selectedCapture?.id, searchText, sourceFilter, timeFilter, gallery.captures.length]);

  return (
    <ScreenFrame
      title="Gallery"
      description="Reverse-chronological capture browsing with date filters and a focused mobile viewer."
    >
      <div className="gallery-layout">
        <section className="gallery-browser" aria-label="Capture browser">
          <div className="gallery-timeline-header">
            <div>
              <p className="eyebrow">Timeline</p>
              <h3>Recent shrimp moments</h3>
            </div>
            <span>{gallery.totalItems} indexed</span>
          </div>

          <div className="timeline-chip-row" aria-label="Capture timeline days">
            {timelineDays.length > 0 ? (
              timelineDays.map(([day, label]) => (
                <button
                  key={day}
                  type="button"
                  className={dateFilter === day ? "timeline-chip active" : "timeline-chip"}
                  onClick={() => setDateFilter(day)}
                >
                  {label}
                </button>
              ))
            ) : (
              <span className="timeline-chip muted">No timeline days loaded</span>
            )}
          </div>

          <div className="source-chip-row" aria-label="Capture source filters">
            <button
              type="button"
              className={sourceFilter === "All" ? "source-chip active" : "source-chip"}
              onClick={() => setSourceFilter("All")}
            >
              All sources
            </button>
            {captureSources.length > 0 ? (
              captureSources.map((source) => (
                <button
                  key={source}
                  type="button"
                  className={sourceFilter === source ? "source-chip active" : "source-chip"}
                  onClick={() => setSourceFilter(source)}
                >
                  {source}
                </button>
              ))
            ) : (
              <span className="source-chip muted">Waiting for captures</span>
            )}
          </div>

          <div className="source-chip-row" aria-label="Capture time filters">
            <button
              type="button"
              className={timeFilter === "All" ? "source-chip active" : "source-chip"}
              onClick={() => setTimeFilter("All")}
            >
              All times
            </button>
            {timeBuckets.length > 0 ? (
              timeBuckets.map((bucket) => (
                <button
                  key={bucket}
                  type="button"
                  className={timeFilter === bucket ? "source-chip active" : "source-chip"}
                  onClick={() => setTimeFilter(bucket)}
                >
                  {bucket}
                </button>
              ))
            ) : (
              <span className="source-chip muted">Waiting for times</span>
            )}
          </div>

          <div className="gallery-toolbar">
            <label>
              <span>Search captures</span>
              <input
                type="search"
                value={searchText}
                onChange={(event) => setSearchText(event.target.value)}
                aria-label="Search captures"
                placeholder="Filename or source"
              />
            </label>
            <label>
              <span>Filter by day</span>
              <input
                type="date"
                value={dateFilter}
                onChange={(event) => setDateFilter(event.target.value)}
                aria-label="Filter captures by day"
              />
            </label>
            <button type="button" className="secondary-button" disabled={!hasGalleryFilters} onClick={clearFilter}>
              Clear filter
            </button>
          </div>

          <div className="gallery-summary" role="status" aria-live="polite">
            {gallery.isLoading
              ? "Loading captures..."
              : `${visibleCaptures.length} of ${gallery.totalItems} capture${gallery.totalItems === 1 ? "" : "s"} shown${
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
              <button type="button" className="secondary-button" disabled={!hasGalleryFilters} onClick={clearFilter}>
                Clear date filter
              </button>
            </div>
          ) : null}

          {emptyFilteredState ? (
            <div className="empty-state">
              <strong>No matching captures</strong>
              <span>Adjust search, source, or time filters to widen the timeline.</span>
              <button type="button" className="secondary-button" onClick={clearFilter}>
                Clear gallery filters
              </button>
            </div>
          ) : null}

          <div className="capture-list" aria-label="Capture thumbnail timeline">
            {!gallery.isLoading ? visibleCaptures.map((capture) => (
              <button
                type="button"
                key={capture.id}
                className={selectedVisibleCapture?.id === capture.id ? "capture-list-item active" : "capture-list-item"}
                onClick={() => selectCapture(capture)}
                aria-pressed={selectedVisibleCapture?.id === capture.id}
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
          {selectedVisibleCapture ? (
            <>
              <div className="viewer-hero-header">
                <div>
                  <p className="eyebrow">Featured Capture</p>
                  <h3>{formatDateTime(selectedVisibleCapture.capturedAtUtc)}</h3>
                </div>
                <span className="source-chip">{selectedVisibleCapture.sourceType}</span>
              </div>
              <div className="viewer-frame">
                {gallery.isImageLoading ? (
                  <div className="stream-fallback" role="status">
                    <strong>Loading image</strong>
                    <span>Fetching the protected capture with your signed-in session.</span>
                  </div>
                ) : null}
                {gallery.imageFailed ? (
                  <div className="stream-fallback" role="alert">
                    <strong>Image unavailable</strong>
                    <span>The capture metadata loaded, but the protected image file could not be displayed.</span>
                  </div>
                ) : null}
                {gallery.selectedImageUrl ? (
                  <img
                    alt={`Shrimp tank capture from ${formatDateTime(selectedVisibleCapture.capturedAtUtc)}`}
                    onError={() => setGallery((current) => ({ ...current, imageFailed: true }))}
                    src={gallery.selectedImageUrl}
                  />
                ) : null}
              </div>
              <div className="viewer-details">
                <p className="eyebrow">Focused Viewer</p>
                <h3>{selectedVisibleCapture.fileName}</h3>
                <p>Captured {formatDateTime(selectedVisibleCapture.capturedAtUtc)}.</p>
                <div className="gallery-action-bar" aria-label="Gallery capture actions">
                  {gallery.selectedImageUrl ? (
                    <a className="primary-button inline-link" href={gallery.selectedImageUrl} target="_blank" rel="noreferrer">
                      Open image
                    </a>
                  ) : null}
                  <a className="secondary-button inline-link" href={selectedVisibleCapture.metadataUrl} target="_blank" rel="noreferrer">
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
  const liveStreamUrl = auth.session?.token
    ? `/stream/live?view=${streamVersion}&access_token=${encodeURIComponent(auth.session.token)}`
    : `/stream/live?view=${streamVersion}`;

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
      <article className={`live-stage ${streamUnavailable ? "offline" : ""}`} aria-label="Immersive live camera stage">
        <div className="live-stage-top">
          <div>
            <p className="eyebrow">Camera Feed</p>
            <strong>{streamStatus === "online" ? "Live stream online" : "Waiting for stream"}</strong>
          </div>
          <span className={`status-pill ${streamStatus}`}>{streamStatus}</span>
        </div>

        <div className="stream-frame">
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
            src={liveStreamUrl}
          />
        </div>

        <div className="live-control-tray" aria-label="Live camera controls">
          <button type="button" className="secondary-button" onClick={retryStream}>
            Retry stream
          </button>
          <button
            type="button"
            className="primary-button"
            disabled={isCapturing || streamUnavailable}
            onClick={() => void captureSnapshot()}
          >
            {isCapturing ? "Capturing..." : "Capture snapshot"}
          </button>
        </div>

        <div className="live-status-panel">
          <div>
            <p className="eyebrow">Manual Snapshot</p>
            <h3>Capture the moment</h3>
            <p>Take a still image from the current camera source without leaving the live tank view.</p>
          </div>
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
        </div>
      </article>
    </ScreenFrame>
  );
}

function SettingsScreen({ auth }: { auth: AuthContext }) {
  const [state, setState] = useState<SettingsState>({
    form: null,
    health: null,
    cameras: [],
    isLoading: true,
    isCameraDiscoveryLoading: false,
    isSaving: false,
    isDirty: false,
    message: null,
    cameraDiscoveryMessage: null,
    errors: {}
  });

  async function loadSettings() {
    setState((current) => ({ ...current, isLoading: true, message: null, cameraDiscoveryMessage: null, errors: {} }));

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
      cameras: [],
      isLoading: false,
      isCameraDiscoveryLoading: false,
      isSaving: false,
      isDirty: false,
      message,
      cameraDiscoveryMessage: null,
      errors: {}
    });
  }

  useEffect(() => {
    void loadSettings();
  }, []);

  useEffect(() => {
    if (!state.form) {
      return;
    }

    void loadCameraOptions(state.form.camera.platform, state.form.camera.source);
  }, [state.form?.camera.platform]);

  async function loadCameraOptions(platform: string, currentSource: string) {
    setState((current) => ({
      ...current,
      isCameraDiscoveryLoading: true,
      cameraDiscoveryMessage: `Looking for ${platform} cameras...`
    }));

    try {
      const response = await auth.authenticatedFetch(`/cameras?platform=${encodeURIComponent(platform)}`);
      if (!response.ok) {
        throw new Error("Camera discovery is unavailable right now.");
      }

      const payload = (await response.json()) as CameraDiscoveryResponse;
      setState((current) => ({
        ...current,
        cameras: ensureSelectedCameraOption(payload.cameras, platform, currentSource),
        isCameraDiscoveryLoading: false,
        cameraDiscoveryMessage: payload.cameras.length > 0
          ? `Found ${payload.cameras.length} camera${payload.cameras.length === 1 ? "" : "s"} for ${platform}.`
          : `No ${platform} cameras were detected. You can keep or enter a custom source.`
      }));
    } catch {
      setState((current) => ({
        ...current,
        cameras: ensureSelectedCameraOption([], platform, currentSource),
        isCameraDiscoveryLoading: false,
        cameraDiscoveryMessage: "Camera discovery failed. You can keep the saved source or enter a custom source."
      }));
    }
  }

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
  const isAllDaySchedule = form ? isCaptureWindowAllDay(form.capture) : false;

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

            <div className="settings-summary-strip" aria-label="Settings summary">
              <article>
                <span>Camera</span>
                <strong>{form.camera.platform}</strong>
                <small>{form.camera.source || "No source selected"}</small>
              </article>
              <article>
                <span>Timelapse</span>
                <strong>{form.capture.enabled ? `${form.capture.intervalMinutes} min` : "Paused"}</strong>
                <small>{describeCaptureWindow(form.capture)}</small>
              </article>
              <article>
                <span>Stream</span>
                <strong>
                  {form.camera.streamWidth}x{form.camera.streamHeight}
                </strong>
                <small>{form.camera.streamFramesPerSecond} FPS live feed</small>
              </article>
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
              <div className="schedule-window-card">
                <div>
                  <strong>Timelapse active window</strong>
                  <span>
                    Scheduled captures only run inside this UTC window. Use all day when you want captures to keep running overnight or right now.
                  </span>
                </div>
                <label className="toggle-row">
                  <span>Active all day</span>
                  <input
                    type="checkbox"
                    checked={isAllDaySchedule}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        capture: {
                          ...current.capture,
                          activeStartHourUtc: event.target.checked ? 0 : 6,
                          activeEndHourUtc: event.target.checked ? 24 : 22
                        }
                      }))
                    }
                  />
                </label>
                <p className="schedule-window-hint">{describeCaptureWindow(form.capture)}</p>
              </div>
              <div className="settings-grid">
                <label>
                  <span>Start hour UTC</span>
                  <input
                    type="number"
                    min="0"
                    max="23"
                    disabled={isAllDaySchedule}
                    value={form.capture.activeStartHourUtc}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        capture: { ...current.capture, activeStartHourUtc: toInteger(event.target.value) }
                      }))
                    }
                  />
                  <small>0-23. Disabled when active all day is on.</small>
                  <FieldError message={state.errors["capture.activeStartHourUtc"]} />
                </label>
                <label>
                  <span>End hour UTC</span>
                  <input
                    type="number"
                    min="1"
                    max="24"
                    disabled={isAllDaySchedule}
                    value={form.capture.activeEndHourUtc}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        capture: { ...current.capture, activeEndHourUtc: toInteger(event.target.value) }
                      }))
                    }
                  />
                  <small>1-24. Use 24 for midnight at the end of the day.</small>
                  <FieldError message={state.errors["capture.activeEndHourUtc"]} />
                </label>
              </div>
            </fieldset>

            <fieldset>
              <legend>Camera And Storage</legend>
              <label>
                <span>Camera source</span>
                <select
                  aria-label="Camera source selector"
                  value={getCameraSelectValue(state.cameras, form.camera.source)}
                  onChange={(event) => {
                    if (event.target.value === "__custom") {
                      return;
                    }

                    updateForm((current) => ({
                      ...current,
                      camera: { ...current.camera, source: event.target.value }
                    }));
                  }}
                >
                  {state.cameras.map((cameraOption) => (
                    <option key={`${cameraOption.platform}:${cameraOption.devicePath}`} value={cameraOption.devicePath}>
                      {formatCameraOption(cameraOption)}
                    </option>
                  ))}
                  <option value="__custom">Custom camera source...</option>
                </select>
                <input
                  aria-label="Selected camera source"
                  value={form.camera.source}
                  placeholder={form.camera.platform === "Linux" ? "/dev/video0" : "Camera display name or DirectShow device path"}
                  onChange={(event) =>
                    updateForm((current) => ({
                      ...current,
                      camera: { ...current.camera, source: event.target.value }
                    }))
                  }
                />
                <small>{state.isCameraDiscoveryLoading ? "Refreshing camera list..." : state.cameraDiscoveryMessage}</small>
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
                        camera: { ...current.camera, platform: event.target.value, source: "" }
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
              <div className="settings-grid resolution-grid" aria-label="Camera resolution controls">
                <label>
                  <span>Capture width</span>
                  <input
                    type="number"
                    min="320"
                    max="7680"
                    value={form.camera.captureWidth}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        camera: { ...current.camera, captureWidth: toInteger(event.target.value) }
                      }))
                    }
                  />
                </label>
                <label>
                  <span>Capture height</span>
                  <input
                    type="number"
                    min="240"
                    max="4320"
                    value={form.camera.captureHeight}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        camera: { ...current.camera, captureHeight: toInteger(event.target.value) }
                      }))
                    }
                  />
                </label>
                <label>
                  <span>Stream width</span>
                  <input
                    type="number"
                    min="320"
                    max="3840"
                    value={form.camera.streamWidth}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        camera: { ...current.camera, streamWidth: toInteger(event.target.value) }
                      }))
                    }
                  />
                </label>
                <label>
                  <span>Stream height</span>
                  <input
                    type="number"
                    min="240"
                    max="2160"
                    value={form.camera.streamHeight}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        camera: { ...current.camera, streamHeight: toInteger(event.target.value) }
                      }))
                    }
                  />
                </label>
                <label>
                  <span>Stream FPS</span>
                  <input
                    type="number"
                    min="1"
                    max="60"
                    value={form.camera.streamFramesPerSecond}
                    onChange={(event) =>
                      updateForm((current) => ({
                        ...current,
                        camera: { ...current.camera, streamFramesPerSecond: toInteger(event.target.value) }
                      }))
                    }
                  />
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
  const screenClassName = `screen screen-${title.toLowerCase().replace(/\s+/g, "-")}`;
  const subtitle = getReferenceSubtitle(title, description);

  return (
    <section className={screenClassName}>
      <div className="screen-header reference-screen-header">
        <span className="reference-shrimp-mark" aria-hidden="true" />
        <div className="reference-brand-copy">
          <h2>Shrimp Cam</h2>
          <p>{subtitle}</p>
        </div>
        <span className={`reference-header-action ${title.toLowerCase()}`} aria-hidden="true">
          {title === "Gallery" ? "1,247" : title === "Live" || title === "Settings" || title === "Dashboard" ? "⚙" : ""}
        </span>
      </div>
      {children}
    </section>
  );
}

function getReferenceSubtitle(title: string, fallback: string) {
  switch (title) {
    case "Dashboard":
      return "Your tank. Always in view.";
    case "Live":
      return "Living Room Tank";
    case "Gallery":
      return "Your tank. Every moment.";
    case "Settings":
      return "Settings & System Status";
    default:
      return fallback;
  }
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

function ensureSelectedCameraOption(cameras: CameraOption[], platform: string, currentSource: string) {
  if (!currentSource.trim() || cameras.some((camera) => camera.devicePath === currentSource)) {
    return cameras;
  }

  return [
    ...cameras,
    {
      displayName: "Saved camera source",
      devicePath: currentSource,
      platform
    }
  ];
}

function getCameraSelectValue(cameras: CameraOption[], currentSource: string) {
  return cameras.some((camera) => camera.devicePath === currentSource) ? currentSource : "__custom";
}

function formatCameraOption(camera: CameraOption) {
  return camera.displayName === camera.devicePath
    ? camera.displayName
    : `${camera.displayName} (${camera.devicePath})`;
}

function isCaptureWindowAllDay(capture: SettingsResponse["capture"]) {
  return capture.activeStartHourUtc === 0 && capture.activeEndHourUtc === 24;
}

function describeCaptureWindow(capture: SettingsResponse["capture"]) {
  if (isCaptureWindowAllDay(capture)) {
    return "Active all day";
  }

  return `Active UTC ${formatUtcHour(capture.activeStartHourUtc)}-${formatUtcHour(capture.activeEndHourUtc)}`;
}

function formatUtcHour(hour: number) {
  const normalized = hour === 24 ? 0 : hour;
  return `${normalized.toString().padStart(2, "0")}:00`;
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
  return (
    window.matchMedia("(display-mode: standalone)").matches
    || window.matchMedia("(display-mode: fullscreen)").matches
    || navigatorWithStandalone.standalone === true
  );
}

function getManualInstallGuidance() {
  const userAgent = window.navigator.userAgent.toLowerCase();
  const isAndroid = userAgent.includes("android");
  const isAppleTouchBrowser = /iphone|ipad|ipod/i.test(window.navigator.userAgent);

  if (isAndroid && userAgent.includes("samsungbrowser")) {
    return "On Samsung Internet, tap the menu button, then Add page to, then Home screen. If prompted, choose Install.";
  }

  if (isAndroid && userAgent.includes("firefox")) {
    return "On Firefox for Android, tap the menu button, then Install. If Install is not shown, choose Add to Home screen.";
  }

  if (isAndroid && userAgent.includes("edg/")) {
    return "On Edge for Android, tap the menu button, then Add to phone or Install app.";
  }

  if (isAndroid && (userAgent.includes("chrome") || userAgent.includes("crios"))) {
    return "On Chrome for Android, tap the three-dot menu, then Install app. If that is not shown, choose Add to Home screen.";
  }

  if (isAppleTouchBrowser) {
    return "On iPhone or iPad, tap Share, then Add to Home Screen.";
  }

  return "Open your browser menu and choose Install app or Add to Home screen when supported.";
}

function getBrowserName() {
  const userAgent = window.navigator.userAgent.toLowerCase();

  if (userAgent.includes("samsungbrowser")) {
    return "Samsung Internet";
  }

  if (userAgent.includes("firefox")) {
    return "Firefox";
  }

  if (userAgent.includes("edg/")) {
    return "Edge";
  }

  if (userAgent.includes("chrome") || userAgent.includes("crios")) {
    return "Chrome";
  }

  if (/iphone|ipad|ipod/i.test(window.navigator.userAgent) || userAgent.includes("safari")) {
    return "Safari";
  }

  return "Browser";
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

function getDashboardStorageUsage(totalCaptures: number | null) {
  const captures = Math.max(0, totalCaptures ?? 0);
  const percent = Math.min(92, Math.max(8, captures * 4));
  return {
    percent,
    detail: captures === 1 ? "1 capture indexed for review." : `${captures} captures indexed for review.`
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

function getCaptureTimeBucket(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "Unknown time";
  }

  const hour = date.getHours();
  if (hour < 6) {
    return "Night";
  }

  if (hour < 12) {
    return "Morning";
  }

  if (hour < 18) {
    return "Afternoon";
  }

  return "Evening";
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

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
    totalItems: number;
  };
};

type CaptureSummary = {
  id: string;
  fileName: string;
  sourceType: string;
  capturedAtUtc: string;
  imageUrl: string;
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

const navItems: NavItem[] = [
  { to: "/dashboard", label: "Dashboard", icon: "DB" },
  { to: "/live", label: "Live", icon: "LV" },
  { to: "/gallery", label: "Gallery", icon: "GL" },
  { to: "/settings", label: "Settings", icon: "ST" }
];

function App() {
  const auth = useAuthSession();
  const isOnline = useOnlineStatus();
  const statusLabel = isOnline ? "Connected" : "Offline";
  const shellMessage = isOnline
    ? auth.isAuthenticated
      ? `Signed in as ${auth.session?.userName}.`
      : "Sign in to reach your protected camera workspace."
    : "Offline shell active. Cached content may be stale.";

  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Shrimp Cam</p>
          <h1>Tank monitoring, simplified.</h1>
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

      <main className="content">
        {!isOnline ? (
          <div className="banner" role="status">
            You are offline. The app shell remains available while data reconnects.
          </div>
        ) : null}

        <section className="hero-card">
          <p className="eyebrow">Secure Shell</p>
          <h2>{auth.isAuthenticated ? "Session active" : "Sign-in required"}</h2>
          <p>{shellMessage}</p>
        </section>

        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="/sign-in" element={<SignInScreen auth={auth} />} />
          <Route
            path="/dashboard"
            element={
              <ProtectedRoute auth={auth}>
                <DashboardScreen auth={auth} />
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
                <ScreenFrame
                  title="Gallery"
                  description="Recent captures list scaffolded for cards, filters, and a future focused viewer."
                >
                  <div className="list-card">
                    <div className="gallery-item">
                      <strong>Today</strong>
                      <span>Recent captures will appear here in reverse chronological order.</span>
                    </div>
                    <div className="gallery-item">
                      <strong>Viewer-ready</strong>
                      <span>The shell leaves room for full-screen image review and date filters.</span>
                    </div>
                  </div>
                </ScreenFrame>
              </ProtectedRoute>
            }
          />
          <Route
            path="/settings"
            element={
              <ProtectedRoute auth={auth}>
                <SettingsPlaceholder auth={auth} />
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

function DashboardScreen({ auth }: { auth: AuthContext }) {
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
              {dashboard.healthError ? <p>{dashboard.healthError}</p> : null}
              {dashboard.capturesError ? <p>{dashboard.capturesError}</p> : null}
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
            <p className="live-message" role={message.includes("failed") || streamUnavailable ? "alert" : "status"}>
              {message}
            </p>
          ) : null}
        </article>
      </div>
    </ScreenFrame>
  );
}

function SettingsPlaceholder({ auth }: { auth: AuthContext }) {
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [isChecking, setIsChecking] = useState(false);

  async function verifySession() {
    setIsChecking(true);
    setStatusMessage(null);
    try {
      const response = await auth.authenticatedFetch("/settings");
      if (response.ok) {
        setStatusMessage("Session verified. Settings can be loaded safely.");
      } else if (response.status === 403) {
        setStatusMessage("Your session is valid, but this account cannot edit settings.");
      }
    } finally {
      setIsChecking(false);
    }
  }

  return (
    <ScreenFrame title="Settings" description="Combined settings and device status layout stub for safe editing flows.">
      <div className="list-card">
        <div className="gallery-item">
          <strong>Capture cadence</strong>
          <span>Settings forms can be dropped into this shared shell without changing navigation.</span>
        </div>
        <div className="gallery-item">
          <strong>System status</strong>
          <span>Health details, validation, and save feedback can live side by side here.</span>
        </div>
        <div className="gallery-item">
          <strong>Session check</strong>
          <span>Protected requests return users to sign-in if the backend reports an expired session.</span>
          <button type="button" className="secondary-button" disabled={isChecking} onClick={() => void verifySession()}>
            {isChecking ? "Checking..." : "Verify session"}
          </button>
          {statusMessage ? <span>{statusMessage}</span> : null}
        </div>
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

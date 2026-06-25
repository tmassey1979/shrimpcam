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
                <ScreenFrame
                  title="Dashboard"
                  description="Quick system overview with room for health, storage, and latest capture data."
                >
                  <div className="stat-grid">
                    <StatCard eyebrow="Camera" value="Online" detail="Stream and snapshot services ready." />
                    <StatCard eyebrow="Latest Capture" value="2 min ago" detail="Placeholder metadata for first shell slice." />
                    <StatCard eyebrow="Storage" value="68%" detail="Compact summary card sized for phones." />
                  </div>
                </ScreenFrame>
              </ProtectedRoute>
            }
          />
          <Route
            path="/live"
            element={
              <ProtectedRoute auth={auth}>
                <ScreenFrame
                  title="Live"
                  description="Live stream area with space for connection status and primary capture actions."
                >
                  <div className="panel stack-gap">
                    <div className="video-placeholder" aria-label="Live stream placeholder">
                      <span>Live viewport placeholder</span>
                    </div>
                    <div className="action-row">
                      <button type="button" className="primary-button">
                        Capture Snapshot
                      </button>
                      <span className="support-copy">Manual controls will attach here once APIs are available.</span>
                    </div>
                  </div>
                </ScreenFrame>
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

export default App;

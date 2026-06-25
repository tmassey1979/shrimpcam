import { NavLink, Navigate, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import { useOnlineStatus } from "./useOnlineStatus";

type NavItem = {
  to: string;
  label: string;
  icon: string;
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

const navItems: NavItem[] = [
  { to: "/dashboard", label: "Dashboard", icon: "DB" },
  { to: "/live", label: "Live", icon: "LV" },
  { to: "/gallery", label: "Gallery", icon: "GL" },
  { to: "/settings", label: "Settings", icon: "ST" }
];

function App() {
  const isOnline = useOnlineStatus();
  const statusLabel = isOnline ? "Connected" : "Offline";
  const shellMessage = isOnline
    ? "Core shell is ready for API integration."
    : "Offline shell active. Cached content may be stale.";

  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Shrimp Cam</p>
          <h1>Tank monitoring, simplified.</h1>
        </div>
        <span className={`status-pill ${isOnline ? "online" : "offline"}`}>{statusLabel}</span>
      </header>

      <main className="content">
        {!isOnline ? (
          <div className="banner" role="status">
            You are offline. The app shell remains available while data reconnects.
          </div>
        ) : null}

        <section className="hero-card">
          <p className="eyebrow">App Shell</p>
          <h2>Mobile-first baseline</h2>
          <p>{shellMessage}</p>
        </section>

        <Routes>
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route
            path="/dashboard"
            element={
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
            }
          />
          <Route
            path="/live"
            element={
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
            }
          />
          <Route
            path="/gallery"
            element={
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
            }
          />
          <Route
            path="/settings"
            element={
              <ScreenFrame
                title="Settings"
                description="Combined settings and device status layout stub for safe editing flows."
              >
                <div className="list-card">
                  <div className="gallery-item">
                    <strong>Capture cadence</strong>
                    <span>Settings forms can be dropped into this shared shell without changing navigation.</span>
                  </div>
                  <div className="gallery-item">
                    <strong>System status</strong>
                    <span>Health details, validation, and save feedback can live side by side here.</span>
                  </div>
                </div>
              </ScreenFrame>
            }
          />
          <Route
            path="*"
            element={
              <ScreenFrame
                title="Not Found"
                description="This route does not exist yet."
              >
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
    </div>
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

export default App;

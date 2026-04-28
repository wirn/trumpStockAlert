import { NavLink, Route, Routes } from "react-router-dom";
import AdminPage from "./pages/AdminPage";
import DashboardPage from "./pages/DashboardPage";
import "./style/index.scss";

function App() {
  return (
    <div className="terminal-shell">
      <aside className="site-sidebar">
        <div className="brand-block">
          <div className="brand-row">
            <span className="brand-mark">Terminal</span>
            <span className="version-badge">PRO</span>
          </div>
          <p>v2.4.0-stable</p>
        </div>

        <nav className="site-nav" aria-label="Main navigation">
          <NavLink
            to="/"
            className={({ isActive }) =>
              isActive ? "nav-link active" : "nav-link"
            }
            end
          >
            <span aria-hidden="true">DB</span>
            Dashboard
          </NavLink>
          <NavLink
            to="/admin"
            className={({ isActive }) =>
              isActive ? "nav-link active" : "nav-link"
            }
          >
            <span aria-hidden="true">AD</span>
            Admin
          </NavLink>
        </nav>

        <div className="sidebar-footer">
          <a href="#settings" className="nav-link muted-link">
            <span aria-hidden="true">ST</span>
            Settings
          </a>
          <a href="#documentation" className="nav-link muted-link">
            <span aria-hidden="true">DC</span>
            Documentation
          </a>
          <div className="system-status">
            <span aria-hidden="true" />
            System operational
          </div>
        </div>
      </aside>

      <header className="top-bar">
        <div className="top-bar-title">
          <strong>TrumpStockAlert</strong>
          <span />
          <p>Live Terminal</p>
        </div>
        <div className="top-bar-actions">
          <label className="search-field">
            <span aria-hidden="true">/</span>
            <input
              type="search"
              placeholder="Search signals..."
              aria-label="Search signals"
            />
          </label>
          <button
            type="button"
            className="icon-button"
            aria-label="Notifications"
          >
            N
          </button>
          <button type="button" className="icon-button" aria-label="History">
            H
          </button>
          <div className="user-avatar" aria-label="Signed in as EB">
            EB
          </div>
        </div>
      </header>

      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/admin" element={<AdminPage />} />
      </Routes>
    </div>
  );
}

export default App;

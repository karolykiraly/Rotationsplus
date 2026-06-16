import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { NavLink, Outlet } from "react-router-dom";
import { useMe } from "../useMe";
import { SignIn } from "../pages/SignIn";

function initials(name?: string | null, username?: string | null): string {
  const source = name?.trim() || username?.trim() || "?";
  const parts = source.split(/\s+/);
  return (parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : source.slice(0, 2)).toUpperCase();
}

/** The authenticated staff shell: sidebar nav + topbar + routed content. Management nav is gated to
 *  Admins (the API enforces it regardless). Unauthenticated visitors get the sign-in screen. */
export function AppLayout() {
  const { instance } = useMsal();
  const { user } = useMe();

  return (
    <>
      <UnauthenticatedTemplate>
        <SignIn />
      </UnauthenticatedTemplate>

      <AuthenticatedTemplate>
        <div className="shell">
          <nav className="sidebar">
            <div className="brand">
              <span className="brand-dot" />
              Rotations Plus
            </div>

            <NavLink to="/" end className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}>
              Overview
            </NavLink>

            {user?.isAdmin && (
              <>
                <div className="nav-label">Marketplace</div>
                <NavLink
                  to="/admin/specialties"
                  className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
                >
                  Specialties
                </NavLink>
                <NavLink
                  to="/admin/programs"
                  className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
                >
                  Programs
                </NavLink>
                <NavLink
                  to="/admin/preceptors"
                  className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
                >
                  Preceptors
                </NavLink>

                <div className="nav-label">Operations</div>
                <NavLink
                  to="/admin/rotations"
                  className={({ isActive }) => `nav-item${isActive ? " active" : ""}`}
                >
                  Rotations
                </NavLink>
              </>
            )}
          </nav>

          <div className="main">
            <header className="topbar">
              <h1>Staff console</h1>
              <div className="user-chip">
                <span>{user?.name ?? user?.username ?? "…"}</span>
                <span className="avatar">{initials(user?.name, user?.username)}</span>
                <button className="btn btn-ghost" onClick={() => void instance.logoutRedirect()}>
                  Sign out
                </button>
              </div>
            </header>
            <main className="content">
              <Outlet />
            </main>
          </div>
        </div>
      </AuthenticatedTemplate>
    </>
  );
}

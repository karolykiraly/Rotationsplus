import { useEffect, useRef, useState } from "react";
import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { NavLink, Outlet, useLocation } from "react-router-dom";
import { useMe } from "../useMe";
import { SignIn } from "../pages/SignIn";

import logo from "../assets/images/logo.png";
import searchIcon from "../assets/icons/search2.png";
import homeIcon from "../assets/icons/home.png";
import homeSel from "../assets/icons/home-select.png";
import analyticsIcon from "../assets/icons/analyticsRed.svg";
import permissionIcon from "../assets/icons/permission.png";
import rotationsIcon from "../assets/icons/rotations.png";
import rotationsSel from "../assets/icons/rotations-select.png";
import honorariumIcon from "../assets/icons/honorarium.svg";
import contactsIcon from "../assets/icons/achievement.png";
import programIcon from "../assets/icons/program.png";
import programSel from "../assets/icons/program-select.png";
import leadIcon from "../assets/icons/lead.png";
import customerIcon from "../assets/icons/customerservice.svg";
import adminIcon from "../assets/icons/admin.svg";
import dataIcon from "../assets/icons/data.svg";

function initials(name?: string | null, username?: string | null): string {
  const source = name?.trim() || username?.trim() || "?";
  const parts = source.split(/\s+/);
  return (parts.length > 1 ? parts[0][0] + parts[parts.length - 1][0] : source.slice(0, 2)).toUpperCase();
}

/** Sidebar nav, ordered to mirror the live admin app. Items with `to` are wired to a built screen;
 *  the rest reproduce the legacy menu visually but are inert until their screen is cloned. */
const NAV: { label: string; icon: string; iconSel?: string; to?: string }[] = [
  { label: "Dashboard", icon: homeIcon, iconSel: homeSel, to: "/admin/dashboard" },
  { label: "Analytics", icon: analyticsIcon },
  { label: "Permissions", icon: permissionIcon, to: "/admin/permission" },
  { label: "Rotations", icon: rotationsIcon, iconSel: rotationsSel, to: "/admin/rotations" },
  { label: "Honorarium", icon: honorariumIcon, to: "/admin/honorarium" },
  { label: "Contacts", icon: contactsIcon },
  { label: "Programs", icon: programIcon, iconSel: programSel, to: "/admin/programs" },
  { label: "Leads", icon: leadIcon },
  { label: "Customer Service", icon: customerIcon },
  { label: "Admin", icon: adminIcon },
  { label: "Data", icon: dataIcon }
];

/** Page title shown in the header bar, keyed by route (the live app shows the screen name here). */
const TITLES: Record<string, string> = {
  "/": "Overview",
  "/admin/dashboard": "Dashboard",
  "/admin/programs": "Programs",
  "/admin/rotations": "Rotations",
  "/admin/specialties": "Specialties",
  "/admin/preceptors": "Preceptors",
  "/admin/permission": "Preceptor approvals",
  "/admin/honorarium": "Honorarium",
  "/admin/students": "Students"
};

/** The authenticated staff shell: a cloned admin sidebar + header + routed content + footer.
 *  Unauthenticated visitors get the sign-in screen. */
export function AppLayout() {
  const { instance } = useMsal();
  const { user } = useMe();
  const { pathname } = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const userRef = useRef<HTMLDivElement>(null);

  // Close the user dropdown on outside click or Escape (matches the app's modal hygiene).
  useEffect(() => {
    if (!menuOpen) return;
    const onDown = (e: MouseEvent) => {
      if (userRef.current && !userRef.current.contains(e.target as Node)) setMenuOpen(false);
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setMenuOpen(false); };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [menuOpen]);

  const title = TITLES[pathname] ?? "Staff console";

  return (
    <>
      <UnauthenticatedTemplate>
        <SignIn />
      </UnauthenticatedTemplate>

      <AuthenticatedTemplate>
        <div className="admin-shell">
          <aside className="admin-aside">
            <NavLink to="/" aria-label="Rotations Plus home">
              <img className="admin-logo" src={logo} alt="Rotations Plus" />
            </NavLink>

            {user?.isAdmin && (
              <nav className="admin-nav" aria-label="Admin">
                <div className="admin-nav-label">Navigation</div>
                <div className="admin-menu">
                  {NAV.map((item) =>
                    item.to ? (
                      <NavLink
                        key={item.label}
                        to={item.to}
                        className={({ isActive }) => `admin-menu-item${isActive ? " active" : ""}`}
                      >
                        {({ isActive }) => (
                          <>
                            <img src={isActive && item.iconSel ? item.iconSel : item.icon} alt="" />
                            <span>{item.label}</span>
                          </>
                        )}
                      </NavLink>
                    ) : (
                      <div key={item.label} className="admin-menu-item soon" title="Coming soon">
                        <img src={item.icon} alt="" />
                        <span>{item.label}</span>
                      </div>
                    )
                  )}
                </div>
              </nav>
            )}
          </aside>

          <div className="admin-main">
            <header className="admin-headline">
              <h1>{title}</h1>
              <div className="admin-headline-right">
                <img className="admin-search-icon" src={searchIcon} alt="Search" />
                <div className="admin-user" ref={userRef}>
                  <span className="avatar">{initials(user?.name, user?.username)}</span>
                  <button className="admin-user-btn" onClick={() => setMenuOpen((o) => !o)} aria-haspopup="menu" aria-expanded={menuOpen}>
                    {user?.name ?? user?.username ?? "…"} ▾
                  </button>
                  {menuOpen && (
                    <div className="admin-user-menu" role="menu">
                      <button role="menuitem" onClick={() => void instance.logoutRedirect()}>Sign out</button>
                    </div>
                  )}
                </div>
              </div>
            </header>

            <main>
              <Outlet />
            </main>

            <footer className="admin-footer">
              <img className="foot-logo" src={logo} alt="Rotations Plus" />
              <nav>
                <span>Home</span><span>Our Process</span><span>Our Team</span>
                <span>For Preceptors</span><span>Consulting Services</span><span>Blog</span><span>FAQ</span>
              </nav>
              <div className="foot-contact">info@rotationsplus.com · +1 (657) 214-7174</div>
              <div>711 South Figueroa Street Ste 4602, Los Angeles CA 90017</div>
              <div>© 2026 RotationsPlus LLC. All rights reserved.</div>
            </footer>
          </div>
        </div>
      </AuthenticatedTemplate>
    </>
  );
}

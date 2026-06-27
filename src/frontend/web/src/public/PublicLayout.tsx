import { useState } from "react";
import { Link, NavLink, Outlet } from "react-router-dom";
import { Footer } from "../components/Footer";
import logo from "../assets/images/logo.png";

/** Top nav of the public marketing site, mirroring the live site menu. */
const NAV: { label: string; to: string }[] = [
  { label: "Home", to: "/" },
  { label: "Our Process", to: "/our-process" },
  { label: "Our Team", to: "/our-team" },
  { label: "For Preceptors", to: "/for-preceptors" },
  { label: "Consulting Services", to: "/consulting-services" },
  { label: "Blog", to: "/blog" }
];

/** The public (anonymous) shell: marketing nav + routed page + shared footer. Deliberately has NO
 *  MsalProvider — the public tree never touches either MSAL instance. "Login" goes to the staff
 *  entry route; "Sign Up" sends students/preceptors into the customer (CIAM) sign-in/sign-up flow at
 *  /portal (the portal's own shell fires the rplus-susi redirect). */
export function PublicLayout() {
  const [open, setOpen] = useState(false);

  return (
    <div className="public">
      <header className="public-top">
        <Link to="/" className="public-brand" aria-label="Rotations Plus home">
          <img src={logo} alt="Rotations Plus" />
        </Link>

        <button
          className="public-burger"
          aria-label="Toggle menu"
          aria-expanded={open}
          onClick={() => setOpen((o) => !o)}
        >
          ☰
        </button>

        <nav className={`public-nav${open ? " open" : ""}`} aria-label="Primary">
          {NAV.map((item) => (
            <NavLink
              key={item.label}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) => `public-link${isActive ? " active" : ""}`}
              onClick={() => setOpen(false)}
            >
              {item.label}
            </NavLink>
          ))}
          <div className="public-cta">
            <Link to="/rotationsplusadmin" className="btn btn-ghost">Login</Link>
            <Link to="/portal" className="btn btn-primary">Sign Up</Link>
          </div>
        </nav>
      </header>

      <main className="public-main">
        <Outlet />
      </main>

      <Footer />
    </div>
  );
}

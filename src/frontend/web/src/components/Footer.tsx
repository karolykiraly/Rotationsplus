import { Link } from "react-router-dom";
import logo from "../assets/images/logo.png";

/** The public site footer, shared by the marketing shell, the admin console, and the customer portal.
 *  Mirrors the live site footer (brand + nav + contact + address). Nav items are real links to the
 *  public marketing routes so the footer works from any shell. */
export function Footer() {
  return (
    <footer className="site-footer">
      <img className="foot-logo" src={logo} alt="Rotations Plus" />
      <nav className="foot-nav" aria-label="Footer">
        <Link to="/">Home</Link>
        <Link to="/our-process">Our Process</Link>
        <Link to="/our-team">Our Team</Link>
        <Link to="/for-preceptors">For Preceptors</Link>
        <Link to="/consulting-services">Consulting Services</Link>
        <Link to="/faq">FAQ</Link>
        <Link to="/privacy-policy">Privacy Policy</Link>
        <Link to="/terms">Terms of Service</Link>
      </nav>
      <div className="foot-contact">info@rotationsplus.com · +1 (657) 214-7174</div>
      <div className="foot-addr">711 South Figueroa Street Ste 4602, Los Angeles CA 90017</div>
      <div className="foot-copy">© 2026 RotationsPlus LLC. All rights reserved.</div>
    </footer>
  );
}

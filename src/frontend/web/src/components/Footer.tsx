import { Link } from "react-router-dom";
import logo from "../assets/images/logo.png";
import facebookIcon from "../assets/images/social/facebook.svg";
import instagramIcon from "../assets/images/social/instagram.svg";
import youtubeIcon from "../assets/images/social/youtube.svg";
import redditIcon from "../assets/images/social/reddit.svg";

/** The public site footer, shared by the marketing shell, the admin console, and the customer portal.
 *  Faithful clone of the live site footer (legacy `components/Footer.js`): brand + address/copyright,
 *  marketing nav, legal links, contact, and the four social icons. Nav items are real links so the
 *  footer works from any shell. */
const SOCIALS: { href: string; icon: string; label: string }[] = [
  { href: "https://www.facebook.com/RotationsPlus-103042048784406/", icon: facebookIcon, label: "Facebook" },
  { href: "https://www.instagram.com/rotationsplus/", icon: instagramIcon, label: "Instagram" },
  { href: "https://youtube.com/channel/UC8NQ51NzVpMe_8xvTDWXvUQ", icon: youtubeIcon, label: "YouTube" },
  { href: "https://www.reddit.com/user/rotationsplus", icon: redditIcon, label: "Reddit" }
];

export function Footer() {
  return (
    <footer className="site-footer">
      <div className="foot-brand">
        <img className="foot-logo" src={logo} alt="Rotations Plus" />
        <div className="foot-addr">777 South Figueroa Street Ste 4600 Los Angeles CA 90017</div>
        <div className="foot-copy">2024 ©RotationsPlus LLC All rights reserved.</div>
      </div>

      <div className="foot-links">
        <nav className="foot-nav" aria-label="Footer">
          <Link to="/">Home</Link>
          <Link to="/our-process">Our Process</Link>
          <Link to="/our-team">Our Team</Link>
          <Link to="/for-preceptors">For Preceptors</Link>
          <Link to="/consulting-services">Consulting Services</Link>
          <Link to="/blog">Blog</Link>
          <Link to="/faq">FAQ</Link>
        </nav>
        <div className="foot-legal">
          <Link to="/privacy-policy">Privacy Policy</Link>
          <Link to="/terms">Terms of Service</Link>
        </div>
      </div>

      <div className="foot-contact">
        <a href="mailto:info@rotationsplus.org">info@rotationsplus.org</a>
        <br />
        +1 (657) 214-7174
        <div className="foot-social">
          {SOCIALS.map((s) => (
            <a key={s.label} href={s.href} target="_blank" rel="noreferrer" aria-label={s.label}>
              <img src={s.icon} alt={s.label} />
            </a>
          ))}
        </div>
      </div>
    </footer>
  );
}

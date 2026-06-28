import { Link } from "react-router-dom";
import aboutImg from "../assets/images/marketing/about_rotations.webp";

/** /about — the legacy About route was an empty stub, so this is a clean, on-brand page built from
 *  the company's own established copy (mission + values), consistent with Our Team / the landing page.
 *  Designed fresh (no legacy page to clone). */
export function AboutPage() {
  return (
    <div className="about-page">
      <section className="funnel-hero">
        <div className="funnel-hero-body">
          <h1>About RotationsPlus</h1>
          <p>
            We make setting up clinical rotations a smooth, seamless and stress-free process. Our
            goal is to match students and graduates with the right preceptors quickly and
            efficiently, while providing 100% transparency.
          </p>
          <Link to="/portal" className="btn btn-primary">Search Programs</Link>
        </div>
        <img className="funnel-hero-img" src={aboutImg} alt="" />
      </section>

      <section className="section about-mission">
        <div className="section-indicator" />
        <h2 className="section-title">Our Mission</h2>
        <p className="section-lead">
          With over a decade of experience connecting medical students and graduates with US
          licensed physicians and hospitals, we use deep automation to deliver a frictionless,
          transparent clinical-rotation experience — helping you earn the US clinical experience and
          Letters of Recommendation you need to reach your residency or medical-school goals.
        </p>
        <div className="about-values">
          <article className="about-value">
            <h3>Trust</h3>
            <p>14+ years of placement experience helping 3000+ students, with transparent pricing.</p>
          </article>
          <article className="about-value">
            <h3>Transparency</h3>
            <p>Clear, constantly-updated program details and an active support team at every step.</p>
          </article>
          <article className="about-value">
            <h3>Technology</h3>
            <p>A streamlined dashboard and deep automation for payments, scheduling and documents.</p>
          </article>
        </div>
        <div className="funnel-process-cta">
          <Link to="/our-team" className="btn btn-ghost">Meet Our Team</Link>
        </div>
      </section>
    </div>
  );
}

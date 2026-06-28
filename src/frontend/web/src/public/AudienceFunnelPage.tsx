import { Link } from "react-router-dom";
import { FaqAccordion, type FaqItem } from "./FaqAccordion";

export interface FunnelStep {
  title: string;
  text: string;
}

export interface AudienceFunnelContent {
  /** Hero */
  heroTitle: string;
  heroText: string;
  heroCta: string;
  heroImg: string;
  /** About band */
  aboutTitle: string;
  aboutText: string;
  aboutImg: string;
  /** Benefits band */
  benefitsTitle: string;
  benefitsText: string;
  benefits: string[];
  bannerImg: string;
  /** Process */
  processTitle: string;
  processLead: string;
  steps: FunnelStep[];
  /** FAQ */
  faqs: FaqItem[];
}

/** Shared layout for the two audience funnel pages (For Preceptors, Consulting Services) — they have
 *  identical structure in the legacy site and differ only in copy. The primary CTA sends the visitor
 *  into the customer (CIAM) sign-up at /portal. The legacy "process" steps used app screenshots; we
 *  render clean numbered step cards instead so the public site never shows stale admin UI. */
export function AudienceFunnelPage({ content }: { content: AudienceFunnelContent }) {
  return (
    <div className="funnel">
      {/* Hero */}
      <section className="funnel-hero">
        <div className="funnel-hero-body">
          <h1>{content.heroTitle}</h1>
          <p>{content.heroText}</p>
          <Link to="/portal" className="btn btn-primary">{content.heroCta}</Link>
        </div>
        <img className="funnel-hero-img" src={content.heroImg} alt="" />
      </section>

      {/* About band */}
      <section className="section funnel-about">
        <img className="funnel-about-img" src={content.aboutImg} alt="" />
        <div className="funnel-about-body">
          <div className="section-indicator" />
          <h2 className="section-title">{content.aboutTitle}</h2>
          <p>{content.aboutText}</p>
        </div>
      </section>

      {/* Benefits band */}
      <section className="section funnel-benefits">
        <div className="funnel-benefits-body">
          <div className="section-indicator" />
          <h2 className="section-title">{content.benefitsTitle}</h2>
          <p>{content.benefitsText}</p>
          <Link to="/portal" className="btn btn-primary">{content.heroCta}</Link>
        </div>
        <ul className="funnel-benefit-list">
          {content.benefits.map((b) => (
            <li key={b}>{b}</li>
          ))}
        </ul>
      </section>

      <img className="funnel-banner" src={content.bannerImg} alt="" />

      {/* Process */}
      <section className="section funnel-process">
        <div className="section-indicator" />
        <h2 className="section-title">{content.processTitle}</h2>
        <p className="section-lead">{content.processLead}</p>
        <div className="funnel-steps">
          {content.steps.map((s, i) => (
            <div className="funnel-step" key={s.title}>
              <div className="funnel-step-n">{String(i + 1).padStart(2, "0")}</div>
              <h3 className="funnel-step-title">{s.title}</h3>
              <p className="funnel-step-text">{s.text}</p>
            </div>
          ))}
        </div>
        <div className="funnel-process-cta">
          <Link to="/portal" className="btn btn-primary">Join Us Today</Link>
        </div>
      </section>

      {/* FAQ */}
      <section className="section funnel-faq">
        <div className="section-indicator" />
        <h2 className="section-title">FAQ</h2>
        <p className="section-lead">You have questions? We've got answers.</p>
        <FaqAccordion items={content.faqs} />
      </section>
    </div>
  );
}

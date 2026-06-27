import { Link } from "react-router-dom";

import trustImg from "../assets/images/marketing/trust.webp";
import transparencyImg from "../assets/images/marketing/transparency.webp";
import technologyImg from "../assets/images/marketing/technology.webp";
import home01 from "../assets/images/marketing/home-01.webp";
import home02 from "../assets/images/marketing/home-02.webp";
import home03 from "../assets/images/marketing/home-03.webp";
import preceptor1 from "../assets/images/marketing/preceptor1.webp";
import preceptor2 from "../assets/images/marketing/preceptor2.webp";
import quote from "../assets/images/marketing/quote.png";
import star from "../assets/images/marketing/star.png";
import partner1 from "../assets/images/marketing/our_partner1.webp";
import partner2 from "../assets/images/marketing/our_partner2.webp";
import partner3 from "../assets/images/marketing/our_partner3.webp";
import partner4 from "../assets/images/marketing/our_partner4.webp";
import partner5 from "../assets/images/marketing/our_partner5.webp";

/** Benefit columns — Trust / Transparency / Technology — verbatim from the live Home page. */
const BENEFITS = [
  {
    img: trustImg,
    title: "TRUST",
    points: [
      "14 years of prior placement experience helping 3000+ students",
      "Lower pricing to meet your needs",
      "Positive feedback and reviews by satisfied trainees"
    ]
  },
  {
    img: transparencyImg,
    title: "TRANSPARENCY",
    points: [
      "Program details are clear and constantly updated",
      "Taking time to discuss both the pros and cons of each Program",
      "Active Support team always available to help you find the best rotation"
    ]
  },
  {
    img: technologyImg,
    title: "TECHNOLOGY",
    points: [
      "Streamlined onboarding with user friendly dashboard",
      "Smooth and worry-free application process and document submission system",
      "Deep automation to ensure smooth payments, scheduling, and communication"
    ]
  }
];

/** "How it works" steps, with the matching screenshot image. */
const STEPS = [
  {
    n: "01",
    img: home01,
    title: "Search, Select & Secure Your Program",
    text: "Set your preferences and use our various filters to find the right clinical rotation. Find Programs based on your preferred start date, location, specialty and duration."
  },
  {
    n: "02",
    img: home02,
    title: "Submit Your Required Documents",
    text: "Benefit from our streamlined application submission system and submit documents required by the rotation site to process your application."
  },
  {
    n: "03",
    img: home03,
    title: "Start Your Clinical Rotation & Get Letters of Recommendation (LORs)",
    text: "Once confirmed, you're all set! Start your dream clinical rotation and get those LORs you need for your residency/med school/NP/PA application."
  }
];

/** Trainee testimonials (live Home page). */
const TESTIMONIALS = [
  {
    quote:
      "At RotationsPlus, they treat you like a friend instead of a client, and they always look for your best interest. It is not very frequent you find a company that puts your interest above anything else, but believe me, they do! They are always transparent and truthful. In today's world you need someone to trust, and that is why RotationsPlus will be the best.",
    name: "Fernando O."
  },
  {
    quote:
      "I had the pleasure to work with RotationsPlus for two rotations. They were always genuine and real about each rotation and accommodated me with the information I needed to reduce my stress. The last thing you want is bad customer service and a lack of transparency. RotationsPlus is the way to go.",
    name: "Fawad M."
  },
  {
    quote:
      "RotationsPlus have been an amazing resource in the process of helping IMGs find sub-internships in order to match to a residency program. Their professionalism and amazing customer service skills have been a real blessing. They make it easy by being patient and forthcoming about the quality of the rotation.",
    name: "Dhaarak D."
  }
];

/** Partner sites (link out to the partners, as the live page does). */
const PARTNERS = [
  { img: partner1, name: "ArcherReview", href: "https://www.archerreview.com/" },
  { img: partner4, name: "RotatingRoom", href: "https://rotatingroom.com/?utm_campaign=RotationsPlus" },
  { img: partner2, name: "MedBound", href: "https://www.medbound.com/" },
  { img: partner3, name: "CMEfy", href: "https://about.cmefy.com/" },
  { img: partner5, name: "LM Global", href: "https://www.lmglobaloverseas.com/" }
];

/** Public landing page — a faithful clone of the live www.rotationsplus.org Home: hero, benefits,
 *  how-it-works, testimonials, dual CTA, partners. The legacy hero search opens the auth flow for an
 *  anonymous visitor; here the primary CTA sends them into the customer (CIAM) sign-up at /portal. */
export function LandingPage() {
  return (
    <div className="landing">
      {/* Hero */}
      <section className="hero">
        <h1 className="hero-title">
          Find Your Perfect <span className="hero-accent">Clinical Experience</span> Today
        </h1>
        <p className="hero-sub">
          Gain Valuable Clinical Experience and Earn Letters of Recommendations to Make Your Medical
          Residency, D.O. or Dental Goals a Reality!
        </p>
        <div className="hero-cta">
          <Link to="/portal" className="btn btn-primary">Search Programs</Link>
          <Link to="/for-preceptors" className="btn btn-ghost">For Preceptors</Link>
        </div>
      </section>

      {/* Our Benefits */}
      <section className="section section-benefit">
        <div className="section-indicator" />
        <h2 className="section-title">Our Benefits</h2>
        <div className="benefit-grid">
          {BENEFITS.map((b) => (
            <article className="benefit-card" key={b.title}>
              <img className="benefit-img" src={b.img} alt="" />
              <h3 className="benefit-name">{b.title}</h3>
              <ul className="benefit-points">
                {b.points.map((p) => (
                  <li key={p}>
                    <span className="benefit-check" aria-hidden="true">✓</span>
                    <span>{p}</span>
                  </li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>

      {/* How it works */}
      <section className="section section-howitworks">
        <div className="section-indicator" />
        <h2 className="section-title">How it works</h2>
        <p className="section-lead">
          Applying to clinical rotations has never been so simple. Start gaining valuable USCE today,
          with your dream hands-on elective or observership one click away.
        </p>
        <div className="steps">
          {STEPS.map((s, i) => (
            <div className={`step${i % 2 ? " reverse" : ""}`} key={s.n}>
              <div className="step-img">
                <img src={s.img} alt="" />
              </div>
              <div className="step-body">
                <div className="step-n">{s.n}</div>
                <h3 className="step-title">{s.title}</h3>
                <p className="step-text">{s.text}</p>
                {i === STEPS.length - 1 && (
                  <Link to="/portal" className="btn btn-primary">Search Programs</Link>
                )}
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Testimonials */}
      <section className="section section-testimonials">
        <div className="section-indicator" />
        <h2 className="section-title">Testimonials</h2>
        <p className="section-lead">
          Trust and transparency are at the core of our identity. Here's what our previous trainees
          have to say about their clinical rotation experience with RotationsPlus.
        </p>
        <div className="testimonial-grid">
          {TESTIMONIALS.map((t) => (
            <blockquote className="testimonial" key={t.name}>
              <img className="testimonial-quote" src={quote} alt="" />
              <p className="testimonial-text">{t.quote}</p>
              <footer className="testimonial-by">{t.name}</footer>
              <div className="testimonial-stars" aria-label="5 out of 5 stars">
                {[0, 1, 2, 3, 4].map((n) => (
                  <img key={n} src={star} alt="" />
                ))}
              </div>
            </blockquote>
          ))}
        </div>
      </section>

      {/* Dual CTA — find a rotation / onboard as preceptor */}
      <section className="section section-learnmore">
        <div className="learnmore-row">
          <img className="learnmore-img" src={preceptor1} alt="" />
          <div className="learnmore-body">
            <div className="section-indicator" />
            <h2 className="section-title">Find Your Perfect Clinical Rotation Today</h2>
            <p>
              Start your USCE journey with RotationsPlus today. Let us help you make your Residency or
              medical school dream a reality.
            </p>
            <Link to="/portal" className="btn btn-primary">Search Programs</Link>
          </div>
        </div>
        <div className="learnmore-row reverse">
          <img className="learnmore-img" src={preceptor2} alt="" />
          <div className="learnmore-body">
            <div className="section-indicator" />
            <h2 className="section-title">Onboard Today to Join Our Preceptor Network</h2>
            <p>
              Want to join our team of highly respected preceptors? Find out more about the
              RotationsPlus Preceptor Experience and benefits.
            </p>
            <Link to="/for-preceptors" className="btn btn-ghost">Learn More</Link>
          </div>
        </div>
      </section>

      {/* Partners */}
      <section className="section section-partners">
        <div className="section-indicator" />
        <h2 className="section-title">Our Partners</h2>
        <p className="section-lead">
          Learn from medical pioneers by rotating at any of our partner sites, including some of the
          most prestigious clinical institutions in the country.
        </p>
        <div className="partner-grid">
          {PARTNERS.map((p) => (
            <a
              className="partner"
              key={p.name}
              href={p.href}
              target="_blank"
              rel="noopener noreferrer"
            >
              <img src={p.img} alt="" />
              <span>{p.name}</span>
            </a>
          ))}
        </div>
      </section>
    </div>
  );
}

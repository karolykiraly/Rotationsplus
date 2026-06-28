import { Link } from "react-router-dom";
import aboutImg from "../assets/images/marketing/about.webp";
import doctorImg from "../assets/images/marketing/doctor.webp";
import team1 from "../assets/images/marketing/team1.webp";
import team2 from "../assets/images/marketing/team2.webp";
import team3 from "../assets/images/marketing/team3.webp";
import team4 from "../assets/images/marketing/team4.webp";

const ABOUT_TEXT =
  "Whether you are an international medical student/graduate (IMG) or a US pre-med student, finding the right clinical rotation can be difficult. If you do manage to find something suitable, high costs, lack of transparency and inconvenient application processes and requirements often make it even more challenging to apply. We noticed those challenges faced by students/applicants when looking for and applying to clinical rotations and realized that a reform was much needed.";

/** Team members, content verbatim from the legacy Our Team page (order preserved). */
const TEAM: { name: string; credentials?: string; role: string; img: string; bio: string[] }[] = [
  {
    name: "Omer Malik",
    role: "CEO",
    img: team1,
    bio: [
      "Graduate of the University of Southern California Marshall School of Business.",
      "14 Years of Clinical Placement experience helping over 3000 pre-med students, medical students and IMGs. Extensively worked on and experience contracting with physicians, teaching hospitals & medical institutions.",
      "Hobbies include basketball & football, being the best dad to Hanna (5 years old) and Liam (3 years old), travelling with his wife Laura, and always wanting to learn more about everything (especially food!)."
    ]
  },
  {
    name: "Howard A. Shaw",
    credentials: "MD, MBA, FACOG, CPE, FACHE, FAAPL",
    role: "MD Advisor",
    img: team3,
    bio: [
      "Graduate of the University of Kansas School of Medicine (MD).",
      "Graduate of the University of Massachusetts Amherst Isenberg School of Management (MBA).",
      "30 years experience in Medical Student and Resident Education as Residency Program Director and Medical Student Rotation Director.",
      "Former Faculty of Yale School of Medicine, University of Connecticut School of Medicine and University of Oklahoma, Tulsa.",
      "Continuous Maintenance of Certification by the American Board of Obstetrics & Gynecology.",
      "Most recently Served as Assistant Designated Official for the HCA/Medical City Healthcare UNT-TCU Graduate Medical Education Consortium in Dallas, TX."
    ]
  },
  {
    name: "Charles Kiraly",
    role: "CTO",
    img: team2,
    bio: [
      "Graduated with a Bachelor's degree in Computer Science from Gabor Denes Foiskola, Budapest, Hungary.",
      "Worked for Siemens for over 20 years in Hungary, Germany and the USA as a Senior Software Engineer and later as a Senior Software Engineering Manager.",
      "5 years of experience in Healthcare, 14 years in Automation and 3 years in Finance.",
      "Hobbies include spending time with his two daughters & wife, tennis, chess and travelling."
    ]
  },
  {
    name: "Jawad Qureshi",
    role: "Tech Advisor",
    img: team4,
    bio: [
      "Graduated with a Bachelor's Degree in Computer Science from California State University - Hayward.",
      "Extensive experience identifying early-stage business trends including the award-winning publisher AfterShock Comics and AI analytics platform, Deep North.",
      "Connected to a global network of like-minded investors in North America, Asia, the Middle East, and Europe.",
      "Enjoys traveling internationally, films & entertainment, and is an avid sports fan of basketball, football and cricket."
    ]
  }
];

/** /our-team — clone of the legacy Our Team page (About Us intro + team + CTA). */
export function OurTeamPage() {
  return (
    <div className="our-team-page">
      <section className="funnel-hero">
        <div className="funnel-hero-body">
          <h1>About Us</h1>
          <p>{ABOUT_TEXT}</p>
        </div>
        <img className="funnel-hero-img" src={aboutImg} alt="" />
      </section>

      <section className="section funnel-about">
        <img className="funnel-about-img" src={doctorImg} alt="" />
        <div className="funnel-about-body">
          <div className="section-indicator" />
          <h2 className="section-title">Revolutionizing the clinical rotation experience for you</h2>
          <p>
            RotationsPlus was established to address those very challenges. With over 12 years of
            experience of connecting medical students and graduates with US licensed physicians &
            hospitals, our founders understand the importance of creating a frictionless experience
            for all parties. Utilizing deep automation, our goal is to match our clients with
            preceptors quickly and efficiently, while providing 100% transparency.
          </p>
        </div>
      </section>

      <section className="section team-section">
        <div className="section-indicator" />
        <h2 className="section-title">Meet Our Team</h2>
        <div className="team-grid">
          {TEAM.map((m) => (
            <article className="team-card" key={m.name}>
              <img className="team-avatar" src={m.img} alt={m.name} />
              <h3 className="team-name">{m.name}</h3>
              {m.credentials && <div className="team-cred">{m.credentials}</div>}
              <div className="team-role">{m.role}</div>
              <ul className="team-bio">
                {m.bio.map((b) => (
                  <li key={b}>{b}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>

      <section className="section team-cta">
        <div className="section-indicator" />
        <h2 className="section-title">Take your RotationsPlus Experience today</h2>
        <p className="section-lead">
          Whether you are a preceptor or a student looking for clinical experience, a world of
          opportunities awaits with RotationsPlus.
        </p>
        <Link to="/portal" className="btn btn-primary">Sign Up</Link>
      </section>
    </div>
  );
}

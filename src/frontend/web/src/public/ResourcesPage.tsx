import { useState } from "react";
import resource1 from "../assets/images/marketing/resource1.webp";
import resource2 from "../assets/images/marketing/resource2.webp";
import resource3 from "../assets/images/marketing/resource3.webp";
import resource4 from "../assets/images/marketing/resource4.webp";
import resource5 from "../assets/images/marketing/resource5.webp";

/** Resource articles, content verbatim from the legacy Resources page. The legacy placeholder 6th
 *  "we don't have a 6th article yet" entry is dropped (a production defect — flagged to the owner).
 *  The legacy "Learn more" buttons and content-type filter buttons are presentational (no handlers
 *  in production either), so they are reproduced as static controls to match the live site exactly. */
const RESOURCES: { title: string; content: string; img: string }[] = [
  {
    title: "How Clinical Rotations Help with Med School Admission",
    content:
      "Learn more about the important role clinical rotations play in assisting with medical school admissions & other aspects of healthcare education. Sign up today!",
    img: resource1
  },
  {
    title: "Clinical Rotation Matching for Preceptors",
    content:
      "RotationsPlus partners with clinical preceptors searching for students to fill any vacancies in rotations. Sign up today or contact our team to get started!",
    img: resource2
  },
  {
    title: "Clerkships, Observerships, & Externships: The Differences",
    content:
      "Learn the differences between medical clerkships, observerships, & externships from the RotationsPlus team. Sign up today to find your perfect rotation!",
    img: resource3
  },
  {
    title: "U.S. Clinical Rotations for International Medical Graduates (IMG)",
    content:
      "We offer U.S. clinical rotations for international medical graduates (IMG) seeking clinical experience & Letters of Recommendation for medical residency. Sign up today!",
    img: resource4
  },
  {
    title: "Help Me Choose My Medical Specialty",
    content:
      "Still need to figure out the medical residency specialty to pursue? Learn about each specialty here & make a more informed decision with RotationsPlus. Sign up today!",
    img: resource5
  }
];

/** Content-type filter chips, exactly as the live site renders them — "Webinar" active, the rest
 *  outlined. Presentational in production (no filtering wired), reproduced faithfully. */
const CONTENT_TYPES = ["Webinar", "Articles", "Video", "Guide", "Case Study", "Report"];

/** /resources — clone of the legacy Resources page (content-type row + article list + Show more). */
export function ResourcesPage() {
  // Matches the legacy paging step: start at 10, +10 per "Show more". With the current article count
  // the button stays hidden (length <= showCount) — same as production.
  const [showCount, setShowCount] = useState(10);

  return (
    <div className="resources-page">
      <section className="public-page-head">
        <h1>Resources</h1>
        <p>
          All the information you need to support your medical journey — all in one place.
        </p>
      </section>

      <section className="section resources-body">
        <div className="resource-content-types">
          <span className="resource-content-label">Content types</span>
          {CONTENT_TYPES.map((type, i) => (
            <button key={type} type="button" className={`btn ${i === 0 ? "btn-primary" : "btn-outline"} resource-chip`}>
              {type}
            </button>
          ))}
        </div>

        <div className="resources-list">
          {RESOURCES.slice(0, showCount).map((r) => (
            <article className="resource-card" key={r.title}>
              <img className="resource-img" src={r.img} alt="" />
              <div className="resource-body">
                <h2 className="resource-title">{r.title}</h2>
                <p className="resource-text">{r.content}</p>
                <button type="button" className="btn btn-outline resource-learn-more">
                  Learn more
                </button>
              </div>
            </article>
          ))}
        </div>

        {RESOURCES.length > showCount && (
          <div className="resources-show-more">
            <button type="button" className="btn btn-outline" onClick={() => setShowCount((c) => c + 10)}>
              Show more
            </button>
          </div>
        )}
      </section>
    </div>
  );
}

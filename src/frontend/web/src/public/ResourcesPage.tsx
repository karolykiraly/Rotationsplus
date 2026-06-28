import resource1 from "../assets/images/marketing/resource1.webp";
import resource2 from "../assets/images/marketing/resource2.webp";
import resource3 from "../assets/images/marketing/resource3.webp";
import resource4 from "../assets/images/marketing/resource4.webp";
import resource5 from "../assets/images/marketing/resource5.webp";

/** Resource articles, content verbatim from the legacy Resources page (the legacy placeholder 6th
 *  "we don't have a 6th article yet" entry is intentionally dropped). The legacy "Learn more" buttons
 *  linked nowhere, so the cards are presentational until a real articles backend exists (deferred with
 *  Blog). */
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

/** /resources — clone of the legacy Resources page. */
export function ResourcesPage() {
  return (
    <div className="resources-page">
      <section className="public-page-head">
        <h1>Resources</h1>
        <p>All the information you need to support your medical journey — all in one place.</p>
      </section>

      <section className="section resources-list">
        {RESOURCES.map((r) => (
          <article className="resource-card" key={r.title}>
            <img className="resource-img" src={r.img} alt="" />
            <div className="resource-body">
              <h2 className="resource-title">{r.title}</h2>
              <p className="resource-text">{r.content}</p>
            </div>
          </article>
        ))}
      </section>
    </div>
  );
}

import { Link } from "react-router-dom";
import heroImg from "../assets/images/marketing/ourprocess_title.webp";

/** The 6-step student journey, content verbatim from the legacy Our Process page. */
const STEPS: { title: string; points: string[] }[] = [
  {
    title: "Create Your Login",
    points: [
      "You've got GOALS — let's make them happen!",
      "Create an Account with RotationsPlus to open up a new world of opportunities.",
      "Add details to your profile to help us find the most suitable clinical rotations for you."
    ]
  },
  {
    title: "Search & Select Your Rotation",
    points: [
      "Refine your search based on what's most important to you.",
      "Set your preferences and narrow down your options using various filters.",
      "Whether it's one rotation or a whole year's worth, plan ahead and have confidence in your decision."
    ]
  },
  {
    title: "Unlock Preceptor Profile",
    points: [
      "Pay a fee to view your physician's name & address before you commit to the entire amount.",
      "Add Favorites & Compare.",
      "Once you have reviewed your choice and are satisfied, proceed to rotation approval."
    ]
  },
  {
    title: "Obtain An Approval & Make Payment",
    points: [
      "Either make a refundable deposit, wait for the preceptor's approval and then pay the balance, or...",
      "Get approved instantly and process full payment to limit any schedule changes later on."
    ]
  },
  {
    title: "Complete Your Registration",
    points: [
      "User friendly dashboard to easily upload all requirements.",
      "Seamlessly upload required documents and provide necessary information through a safe and secure system."
    ]
  },
  {
    title: "It's Rotation Time",
    points: [
      "Your dream rotation is confirmed, and you're all set! Make the most of this clinical experience.",
      "Learn about the US health system, boost your application by earning a valuable LOR (letter of recommendation) and get a clinical evaluation.",
      "Upon successful completion, evaluate your Preceptor and refer others."
    ]
  }
];

/** /our-process — clone of the legacy Our Process page. CTA → CIAM sign-up. */
export function OurProcessPage() {
  return (
    <div className="our-process">
      <section className="funnel-hero">
        <div className="funnel-hero-body">
          <h1>Our Process</h1>
          <p>
            Getting USCE shouldn't be nerve-wracking. We make it smooth, streamlined and stress-free
            with our user-friendly application process and use of the latest technology.
          </p>
          <Link to="/portal" className="btn btn-primary">Search Programs</Link>
        </div>
        <img className="funnel-hero-img" src={heroImg} alt="" />
      </section>

      <section className="section process-steps">
        <ol className="proc-list">
          {STEPS.map((s, i) => (
            <li className="proc-item" key={s.title}>
              <div className="proc-n">{String(i + 1).padStart(2, "0")}</div>
              <div className="proc-body">
                <h2 className="proc-title">{s.title}</h2>
                <ul className="proc-points">
                  {s.points.map((p) => (
                    <li key={p}>
                      <span className="benefit-check" aria-hidden="true">✓</span>
                      <span>{p}</span>
                    </li>
                  ))}
                </ul>
              </div>
            </li>
          ))}
        </ol>
        <div className="funnel-process-cta">
          <Link to="/portal" className="btn btn-primary">Search Programs</Link>
        </div>
      </section>
    </div>
  );
}

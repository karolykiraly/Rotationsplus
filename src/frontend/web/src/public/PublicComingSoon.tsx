import { Link } from "react-router-dom";

/** Placeholder for public marketing routes whose full page lands in a later PR (LP-2…LP-6). Keeps the
 *  nav/footer links live (no dead routes) while the page is built. Replaced per page as they ship. */
export function PublicComingSoon({ title }: { title: string }) {
  return (
    <section className="public-soon">
      <h1>{title}</h1>
      <p>This page is coming soon.</p>
      <Link to="/" className="btn btn-ghost">Back to home</Link>
    </section>
  );
}

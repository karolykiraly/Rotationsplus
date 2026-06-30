import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getPublicPrograms, type PublicProgram } from "./publicApi";

// "Clinical Needs" program tags, verbatim from the live hero (NewHome.js): "Most Popular" first,
// the rest alphabetical.
const CLINICAL_NEEDS = [
  "Most Popular", "100% Inpatient Hospitalist", "Academic Affiliation", "Core", "Discount Available",
  "Elective", "Faculty", "Hands On", "Hospital Invitation Letter", "Hospital Letterhead LOR",
  "Housing available", "IMG Friendly", "Inpatient", "Instant Approval", "Publication", "Research",
  "Residency Audition", "Some Inpatient", "UPike"
];
const RATING_OPTIONS = ["5", "4", "3", "2", "1"];
const PRICE_BANDS = ["$1001 - $2000", "$2001 - $3000", "$3001 - $4000", "> $5001"];

/** The landing hero — a faithful clone of the live www.rotationsplus.org home hero (Home.js `<Hero/>`
 *  + the embedded NewHome search): the doctor hero illustration as a full-width background, the
 *  headline + subtitle, then the search bar and the eight filter dropdowns. As on production, an
 *  anonymous visitor gets the search UI but NOT the results/map — running a search routes them into
 *  the customer (CIAM) sign-in at /portal. The dropdown options are populated from the anonymous
 *  public catalog feed so they show real specialties/cities/states/durations. */
export function HeroSearch() {
  const navigate = useNavigate();
  const [programs, setPrograms] = useState<PublicProgram[]>([]);

  useEffect(() => {
    const ac = new AbortController();
    void getPublicPrograms(ac.signal).then(setPrograms);
    return () => ac.abort();
  }, []);

  const specialties = useMemo(() => [...new Set(programs.map((p) => p.specialtyName))].sort(), [programs]);
  const cities = useMemo(
    () => [...new Set(programs.map((p) => p.city).filter((c): c is string => !!c))].sort(),
    [programs]
  );
  const states = useMemo(
    () => [...new Set(programs.map((p) => p.state).filter((s): s is string => !!s))].sort(),
    [programs]
  );
  const durations = useMemo(
    () => [...new Set(programs.map((p) => p.minWeeksPerRotation))].sort((a, b) => a - b),
    [programs]
  );

  /** Anonymous visitors must sign in to run a search and see results — matches the live site, which
   *  gates the search results (and map) behind login. */
  const requireLogin = () => navigate("/portal");

  return (
    <section className="hero">
      <div className="hero-title">Find Your Perfect</div>
      <div className="hero-title">
        <span className="hero-accent">Clinical Experience</span>&nbsp;Today
      </div>
      <p className="hero-text">
        Gain Valuable Clinical Experience and Earn Letters of Recommendations to Make Your Medical
        Residency, D.O. or Dental Goals a Reality!
      </p>

      <div className="new-search-wrapper">
        <form
          className="search-row"
          onSubmit={(e) => {
            e.preventDefault();
            requireLogin();
          }}
        >
          <input className="search-input" placeholder="What are you searching for?" aria-label="Search programs" />
          <button type="submit" className="search-button">Search</button>
        </form>

        <div className="filter-row">
          <select className="filter-select" aria-label="Specialties" defaultValue="">
            <option value="">Specialties</option>
            {specialties.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select className="filter-select" aria-label="Program Type" defaultValue="">
            <option value="">Program Type</option>
            <option value="InPerson">InPerson</option>
            <option value="TeleRotation">TeleRotation</option>
          </select>
          <select className="filter-select" aria-label="Clinical Needs" defaultValue="">
            <option value="">Clinical Needs</option>
            {CLINICAL_NEEDS.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
          <select className="filter-select" aria-label="Ratings" defaultValue="">
            <option value="">Ratings</option>
            {RATING_OPTIONS.map((r) => <option key={r} value={r}>{r}</option>)}
          </select>
          <select className="filter-select" aria-label="City" defaultValue="">
            <option value="">City</option>
            {cities.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
          <select className="filter-select" aria-label="State" defaultValue="">
            <option value="">State</option>
            {states.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
          <select className="filter-select" aria-label="Duration" defaultValue="">
            <option value="">Duration</option>
            {durations.map((d) => <option key={d} value={String(d)}>{d} weeks</option>)}
          </select>
          <select className="filter-select" aria-label="Pricing" defaultValue="">
            <option value="">Pricing</option>
            {PRICE_BANDS.map((b) => <option key={b} value={b}>{b}</option>)}
          </select>
        </div>
      </div>
    </section>
  );
}

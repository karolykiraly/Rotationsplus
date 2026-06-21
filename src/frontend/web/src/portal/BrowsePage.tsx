import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useCustomerPrograms, usePortalSpecialties } from "./usePortalCatalog";
import { ProgramImage } from "./ProgramImage";
import { PROGRAM_TYPES, programCode, programTypeLabel } from "../programs/programTypes";
import type { Program } from "../api";

/** Whole-dollar amounts show no decimals; fractional amounts keep cents (avoids misstating a price). */
const money = (n: number) =>
  n.toLocaleString(undefined, n % 1 === 0 ? {} : { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Sub-consultations are priced hourly, so the live card shows the per-week amount as-is; every other
 *  type prices the whole minimum stay (per-week retail × the minimum rotation length). */
const isHourly = (p: Program) => p.programType === "ConsultationSub";
const cardTotal = (p: Program) =>
  isHourly(p) ? p.retailAmountPerWeek : p.retailAmountPerWeek * p.minWeeksPerRotation;

type Sort = "default" | "low" | "high";

/** A static 5-star strip (rating is a PHASE-2 field the catalog API doesn't expose yet). */
function Stars() {
  return (
    <span className="rcard-stars" aria-hidden>
      {[0, 1, 2, 3, 4].map((i) => (
        <svg key={i} width="16" height="16" viewBox="0 0 20 20" fill="none">
          <path
            d="M10 1.5l2.6 5.3 5.9.9-4.3 4.1 1 5.8L10 15l-5.2 2.6 1-5.8L1.5 7.7l5.9-.9L10 1.5z"
            fill="#DBDFE3"
          />
        </svg>
      ))}
    </span>
  );
}

/** One catalog result, cloned to the live "rotation-item" card: hospital image + favorite heart,
 *  two tag pills (program code + type), serif title, location row, total price + rating, the
 *  minimum-weeks line, and the specialty pill. The title is a link whose hit area is stretched over
 *  the whole card (CSS ::after), so the entire card is clickable and keyboard-accessible while the
 *  heading stays a real heading. */
function RotationCard({ program }: { program: Program }) {
  return (
    <article className="rcard">
      <div className="rcard-img">
        {/* Decorative: the card is labelled by its specialty heading. Falls back to the gray placeholder. */}
        <ProgramImage url={program.imageUrl} className="rcard-photo" alt="" />
        <span className="rcard-fav" aria-hidden />
      </div>
      <div className="rcard-body">
        <div className="rcard-tags">
          <span className="tag-pill">Program {programCode(program.programType, program.programNumber)}</span>
          <span className="tag-pill">{programTypeLabel(program.programType)}</span>
          {program.isOpen && <span className="tag-pill">Instant Approval</span>}
        </div>
        <h3 className="rcard-title">
          <Link className="rcard-link" to={`/portal/programs/${program.id}`}>{program.specialtyName}</Link>
        </h3>
        {program.preceptorName && <div className="rcard-pre">with {program.preceptorName}</div>}
        <div className="rcard-loc">
          <svg className="rcard-pin" width="12" height="18" viewBox="0 0 12 18" fill="none" aria-hidden>
            <path
              d="M6 0C2.7 0 0 2.7 0 6c0 4.5 6 12 6 12s6-7.5 6-12c0-3.3-2.7-6-6-6zm0 8.2A2.2 2.2 0 1 1 6 3.8a2.2 2.2 0 0 1 0 4.4z"
              fill="#5AA6FF"
            />
          </svg>
          <span>{[program.city, program.state].filter(Boolean).join(", ") || "—"}</span>
        </div>
        <div className="rcard-pricerow">
          <div className="rcard-price">${money(cardTotal(program))}</div>
          <div className="rcard-rating">
            <span className="rcard-reviews">(0)</span>
            <Stars />
          </div>
        </div>
        <div className="rcard-dur">{isHourly(program) ? "Hourly" : `${program.minWeeksPerRotation} weeks minimum`}</div>
        <div className="rcard-spec">{program.specialtyName}</div>
      </div>
    </article>
  );
}

/** Student-facing catalog browse, cloned to the live "Search Results" screen: a hero band with the
 *  frosted search bar, a sort control, and a 3-up grid of rotation cards with progressive reveal. */
export function BrowsePage() {
  const [specialtyId, setSpecialtyId] = useState("");
  const [programType, setProgramType] = useState("");
  const [q, setQ] = useState("");
  const [maxRetail, setMaxRetail] = useState("");
  const [sort, setSort] = useState<Sort>("default");
  const [showCount, setShowCount] = useState(9);

  const queryString = useMemo(() => {
    const params = new URLSearchParams();
    if (specialtyId) params.set("specialtyId", specialtyId);
    if (programType) params.set("programType", programType);
    if (q.trim()) params.set("q", q.trim());
    if (maxRetail) params.set("maxRetailPerWeek", maxRetail);
    const s = params.toString();
    return s ? `?${s}` : "";
  }, [specialtyId, programType, q, maxRetail]);

  // A new filter set starts the progressive reveal over at the first page.
  useEffect(() => setShowCount(9), [queryString]);

  const programs = useCustomerPrograms(queryString);
  const specialties = usePortalSpecialties();

  const rows = useMemo(() => {
    const list = [...(programs.data ?? [])];
    if (sort === "low") list.sort((a, b) => cardTotal(a) - cardTotal(b));
    if (sort === "high") list.sort((a, b) => cardTotal(b) - cardTotal(a));
    return list;
  }, [programs.data, sort]);

  const shown = rows.slice(0, showCount);
  const remaining = rows.length - shown.length;

  return (
    <div className="browse">
      <section className="browse-hero">
        <h1>Search Results</h1>
        <form className="search-bar" onSubmit={(e) => e.preventDefault()}>
          <label className="search-pill">
            <input
              aria-label="Search"
              placeholder="Search specialty or keyword…"
              value={q}
              onChange={(e) => setQ(e.target.value)}
            />
          </label>
          <label className="search-pill">
            <select aria-label="Specialty" value={specialtyId} onChange={(e) => setSpecialtyId(e.target.value)}>
              <option value="">All Specialties</option>
              {(specialties.data ?? []).map((s) => (
                <option key={s.id} value={s.id}>{s.name}</option>
              ))}
            </select>
          </label>
          <label className="search-pill">
            <select aria-label="Type" value={programType} onChange={(e) => setProgramType(e.target.value)}>
              <option value="">All Programs</option>
              {PROGRAM_TYPES.map((t) => (
                <option key={t.value} value={t.value}>{t.label}</option>
              ))}
            </select>
          </label>
          <label className="search-pill">
            <input
              aria-label="Max price per week"
              type="number"
              min={0}
              step={1}
              placeholder="Max $/wk"
              value={maxRetail}
              onChange={(e) => setMaxRetail(e.target.value)}
            />
          </label>
          <button type="submit" className="search-go">SEARCH</button>
        </form>
      </section>

      <section className="browse-body">
        {programs.isLoading && <div className="card state">Loading programs…</div>}
        {programs.isError && (
          <div className="card state">Couldn’t load programs: {(programs.error as Error).message}</div>
        )}
        {!programs.isLoading && !programs.isError && rows.length === 0 && (
          <div className="card state">No programs match your filters.</div>
        )}

        {!programs.isError && rows.length > 0 && (
          <>
            <div className="browse-sort">
              <span className="browse-sort-label">Sort by:</span>
              <select aria-label="Sort by" value={sort} onChange={(e) => setSort(e.target.value as Sort)}>
                <option value="default">Featured</option>
                <option value="low">Price low to high</option>
                <option value="high">Price high to low</option>
              </select>
            </div>

            <div className="browse-grid">
              {shown.map((p) => <RotationCard key={p.id} program={p} />)}
            </div>

            {remaining > 0 && (
              <div className="browse-more">
                {/* Match the live site: one click reveals all remaining results, then the button hides. */}
                <button type="button" className="btn show-more" onClick={() => setShowCount(rows.length)}>
                  Show more ({remaining})
                </button>
              </div>
            )}
          </>
        )}
      </section>
    </div>
  );
}

import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useCustomerPrograms, usePortalSpecialties } from "./usePortalCatalog";
import { PROGRAM_TYPES, programTypeLabel } from "../programs/programTypes";

const money = (n: number) =>
  n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/** Student-facing catalog browse: filter by specialty/type/price/text, results as program cards. */
export function BrowsePage() {
  const [specialtyId, setSpecialtyId] = useState("");
  const [programType, setProgramType] = useState("");
  const [q, setQ] = useState("");
  const [maxRetail, setMaxRetail] = useState("");

  const queryString = useMemo(() => {
    const params = new URLSearchParams();
    if (specialtyId) params.set("specialtyId", specialtyId);
    if (programType) params.set("programType", programType);
    if (q.trim()) params.set("q", q.trim());
    if (maxRetail) params.set("maxRetailPerWeek", maxRetail);
    const s = params.toString();
    return s ? `?${s}` : "";
  }, [specialtyId, programType, q, maxRetail]);

  const programs = useCustomerPrograms(queryString);
  const specialties = usePortalSpecialties();
  const rows = programs.data ?? [];

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Find a rotation</h2>
          <p>Browse clinical-rotation programs and open one for details.</p>
        </div>
      </div>

      <div className="card filters">
        <input aria-label="Search" placeholder="Search specialty or keyword…" value={q} onChange={(e) => setQ(e.target.value)} />
        <select aria-label="Specialty" value={specialtyId} onChange={(e) => setSpecialtyId(e.target.value)}>
          <option value="">All specialties</option>
          {(specialties.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        <select aria-label="Type" value={programType} onChange={(e) => setProgramType(e.target.value)}>
          <option value="">All types</option>
          {PROGRAM_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
        </select>
        <input aria-label="Max price per week" type="number" min={0} step={0.01} placeholder="Max $/wk"
          value={maxRetail} onChange={(e) => setMaxRetail(e.target.value)} />
      </div>

      {programs.isLoading && <div className="card state">Loading programs…</div>}
      {programs.isError && <div className="card state">Couldn’t load programs: {(programs.error as Error).message}</div>}
      {!programs.isLoading && !programs.isError && rows.length === 0 && (
        <div className="card state">No programs match your filters.</div>
      )}

      {rows.length > 0 && (
        <div className="program-grid">
          {rows.map((p) => (
            <Link key={p.id} to={`/portal/programs/${p.id}`} className="program-card">
              <div className="pc-specialty">{p.specialtyName}</div>
              <div className="pc-type">{programTypeLabel(p.programType)}</div>
              <div className="pc-meta">{p.minWeeksPerRotation}+ wks · up to {p.maxStudentsPerRotation} students</div>
              <div className="pc-price">${money(p.retailAmountPerWeek)}/wk</div>
              {p.preceptorName && <div className="pc-preceptor">with {p.preceptorName}</div>}
            </Link>
          ))}
        </div>
      )}
    </>
  );
}

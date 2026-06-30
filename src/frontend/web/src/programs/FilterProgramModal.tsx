import { useState } from "react";
import { Modal } from "../components/Modal";
import { RangeSlider } from "../components/RangeSlider";
import type { ProgramFilter } from "../api";

/** The legacy FilterProgram "Clinical Needs" vocabulary (modal/FilterProgram.js): the base list sorted,
 *  then "Most Popular" prepended. Each entry's `id` is the stored program-tag value; the "100% Inpatient
 *  Hospitalist" label maps to the stored tag id "Hospitalist", exactly as legacy does. */
const CLINICAL_NEEDS: { id: string; title: string }[] = [
  { id: "Most Popular", title: "Most Popular" },
  { id: "Hospitalist", title: "100% Inpatient Hospitalist" },
  { id: "Academic Affiliation", title: "Academic Affiliation" },
  { id: "Core", title: "Core" },
  { id: "Discount Available", title: "Discount Available" },
  { id: "Elective", title: "Elective" },
  { id: "Faculty", title: "Faculty" },
  { id: "Hands On", title: "Hands On" },
  { id: "Hospital Invitation Letter", title: "Hospital Invitation Letter" },
  { id: "Hospital Letterhead LOR", title: "Hospital Letterhead LOR" },
  { id: "Housing available", title: "Housing available" },
  { id: "IMG Friendly", title: "IMG Friendly" },
  { id: "Inpatient", title: "Inpatient" },
  { id: "Instant Approval", title: "Instant Approval" },
  { id: "Publication", title: "Publication" },
  { id: "Research", title: "Research" },
  { id: "Residency Audition", title: "Residency Audition" },
  { id: "Some Inpatient", title: "Some Inpatient" },
  { id: "UPike", title: "UPike" }
];

const AMOUNT_MIN = 0;
const AMOUNT_MAX = 15000;

interface Props {
  initial: ProgramFilter;
  specialties: { id: string; name: string }[];
  /** Distinct "City, State" options for the location dropdown. */
  cities: string[];
  /** The legacy modal only shows the location filter on the InPerson / InPersonResearch tabs. */
  showLocation: boolean;
  onApply: (filter: ProgramFilter) => void;
  onClear: () => void;
  onClose: () => void;
}

/** The FilterProgram modal — a production-faithful clone of the legacy admin Programs filter
 *  (components/modal/FilterProgram.js): Program ID, a Location city dropdown (InPerson tabs only),
 *  a single-select Specialty checkbox list with "All", an Approval Type checkbox group, a Retail
 *  Amount dual-range slider (0–15,000) with min/max inputs, and a "Clinical Needs" tag checkbox grid
 *  with "All". Apply replaces the active filter; Clear resets it. The amount range maps to the
 *  honorarium min/max the API filters on (the "Retail Amount" column is the weekly honorarium). */
export function FilterProgramModal({ initial, specialties, cities, showLocation, onApply, onClear, onClose }: Props) {
  const [programNumber, setProgramNumber] = useState(initial.programNumber?.toString() ?? "");
  const [city, setCity] = useState(initial.city ?? "");
  const [specialtyId, setSpecialtyId] = useState(initial.specialtyId ?? "");
  const [approvalType, setApprovalType] = useState<"both" | "yes" | "no">(
    initial.instantApproval === true ? "yes" : initial.instantApproval === false ? "no" : "both"
  );
  const [amount, setAmount] = useState<[number, number]>([
    initial.honorariumMin ?? AMOUNT_MIN,
    initial.honorariumMax ?? AMOUNT_MAX
  ]);
  const [tags, setTags] = useState<string[]>(initial.tags ?? []);

  const toggleTag = (id: string) =>
    setTags((cur) => (cur.includes(id) ? cur.filter((t) => t !== id) : [...cur, id]));

  const setAmountAt = (index: 0 | 1, raw: string) =>
    setAmount((cur) => {
      const fallback = index === 0 ? AMOUNT_MIN : AMOUNT_MAX;
      const parsed = raw.trim() === "" ? fallback : Number(raw);
      // Coerce non-numeric input and clamp into [0, 15000] so the slider/fill never get a NaN or
      // out-of-range value (which would render a broken thumb and desync from the number input).
      const value = Number.isFinite(parsed) ? Math.min(Math.max(parsed, AMOUNT_MIN), AMOUNT_MAX) : fallback;
      const next: [number, number] = [...cur];
      next[index] = value;
      return next;
    });

  const apply = () => {
    const [lo, hi] = amount;
    onApply({
      programNumber: programNumber.trim() === "" || Number(programNumber) <= 0 ? undefined : Number(programNumber),
      city: city.trim() || undefined,
      specialtyId: specialtyId || undefined,
      instantApproval: approvalType === "both" ? undefined : approvalType === "yes",
      // Send a bound only when it narrows the full 0–15,000 range (legacy treats the full range as "no filter").
      honorariumMin: lo > AMOUNT_MIN ? lo : undefined,
      honorariumMax: hi < AMOUNT_MAX ? hi : undefined,
      tags: tags.length ? tags : undefined
    });
  };

  return (
    <Modal title="Filters" onClose={onClose}>
      <div className="modal-body filter-modal-body">
        <div className="filter-panel">
          <label className="filter-label" htmlFor="pf-number">Program ID:</label>
          <input id="pf-number" type="number" min={0} placeholder="Enter Program ID"
            value={programNumber} onChange={(e) => setProgramNumber(e.target.value)} />
        </div>

        {showLocation && (
          <div className="filter-panel">
            <label className="filter-label" htmlFor="pf-city">Location: City/State</label>
            <select id="pf-city" value={city} onChange={(e) => setCity(e.target.value)}>
              <option value="">All Locations</option>
              {cities.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
        )}

        <div className="filter-panel">
          <div className="filter-label">Specialty:</div>
          <div className="filter-checks">
            <label className="filter-check">
              <input type="checkbox" checked={specialtyId === ""} onChange={() => setSpecialtyId("")} />
              All
            </label>
            {specialties.map((s) => (
              <label key={s.id} className="filter-check">
                <input type="checkbox" checked={specialtyId === s.id}
                  onChange={() => setSpecialtyId(specialtyId === s.id ? "" : s.id)} />
                {s.name}
              </label>
            ))}
          </div>
        </div>

        <div className="filter-panel">
          <div className="filter-label">Approval Type:</div>
          <div className="filter-checks">
            <label className="filter-check">
              <input type="checkbox" checked={approvalType === "both"} onChange={() => setApprovalType("both")} />
              Both
            </label>
            <label className="filter-check">
              <input type="checkbox" checked={approvalType === "yes"} onChange={() => setApprovalType("yes")} />
              Yes (Instant Approval)
            </label>
            <label className="filter-check">
              <input type="checkbox" checked={approvalType === "no"} onChange={() => setApprovalType("no")} />
              No (Not an Instant Approval)
            </label>
          </div>
        </div>

        <div className="filter-panel">
          <div className="filter-label">Retail Amount:</div>
          <div className="amount-range-inputs">
            <input type="number" min={AMOUNT_MIN} max={AMOUNT_MAX} aria-label="Retail amount minimum" placeholder="$1,000"
              value={amount[0]} onChange={(e) => setAmountAt(0, e.target.value)} />
            <span className="filter-label">-</span>
            <input type="number" min={AMOUNT_MIN} max={AMOUNT_MAX} aria-label="Retail amount maximum" placeholder="$15,000"
              value={amount[1]} onChange={(e) => setAmountAt(1, e.target.value)} />
          </div>
          <RangeSlider
            min={AMOUNT_MIN} max={AMOUNT_MAX} value={amount} onChange={setAmount}
            minLabel="Retail amount range minimum" maxLabel="Retail amount range maximum"
          />
        </div>

        <div className="filter-panel">
          <div className="filter-label">Clinical Needs</div>
          <div className="filter-checks two-col">
            <label className="filter-check">
              <input type="checkbox" checked={tags.length === 0} onChange={() => setTags([])} />
              All
            </label>
            {CLINICAL_NEEDS.map((t) => (
              <label key={t.id} className="filter-check">
                <input type="checkbox" checked={tags.includes(t.id)} onChange={() => toggleTag(t.id)} />
                {t.title}
              </label>
            ))}
          </div>
        </div>
      </div>
      <div className="modal-foot">
        <button className="btn btn-ghost" type="button" onClick={onClear}>Clear filters</button>
        <button className="btn btn-primary" type="button" onClick={apply}>Apply filters</button>
      </div>
    </Modal>
  );
}

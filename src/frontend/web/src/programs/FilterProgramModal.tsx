import { useState } from "react";
import { Modal } from "../components/Modal";
import type { ProgramFilter } from "../api";

/** The legacy FilterProgram tag vocabulary (the multi-select chips). */
const PROGRAM_TAGS = [
  "Academic Affiliation", "Core", "Discount Available", "Elective", "Faculty", "Hands On",
  "Hospital Invitation Letter", "Hospital Letterhead LOR", "Hospitalist", "Housing available",
  "IMG Friendly", "Inpatient", "Instant Approval", "Most Popular", "Publication", "Research",
  "Residency Audition", "Some Inpatient", "UPike"
];

interface Props {
  initial: ProgramFilter;
  specialties: { id: string; name: string }[];
  onApply: (filter: ProgramFilter) => void;
  onClear: () => void;
  onClose: () => void;
}

/** The FilterProgram modal (legacy parity): narrows the program list by program number, location,
 *  specialty, instant-approval, honorarium range, and tags. Apply replaces the active filter; Clear
 *  resets it. (The legacy honorarium dual-range slider is rendered as min/max inputs; location is a
 *  free-text "City, State" box rather than a derived dropdown.) */
export function FilterProgramModal({ initial, specialties, onApply, onClear, onClose }: Props) {
  const [programNumber, setProgramNumber] = useState(initial.programNumber?.toString() ?? "");
  const [city, setCity] = useState(initial.city ?? "");
  const [specialtyId, setSpecialtyId] = useState(initial.specialtyId ?? "");
  const [instantApproval, setInstantApproval] = useState<"both" | "yes" | "no">(
    initial.instantApproval === true ? "yes" : initial.instantApproval === false ? "no" : "both"
  );
  const [honorariumMin, setHonorariumMin] = useState(initial.honorariumMin?.toString() ?? "");
  const [honorariumMax, setHonorariumMax] = useState(initial.honorariumMax?.toString() ?? "");
  const [tags, setTags] = useState<string[]>(initial.tags ?? []);

  const toggleTag = (tag: string) =>
    setTags((cur) => (cur.includes(tag) ? cur.filter((t) => t !== tag) : [...cur, tag]));

  const apply = () => {
    const num = (s: string) => (s.trim() === "" ? undefined : Number(s));
    onApply({
      programNumber: num(programNumber),
      city: city.trim() || undefined,
      specialtyId: specialtyId || undefined,
      instantApproval: instantApproval === "both" ? undefined : instantApproval === "yes",
      honorariumMin: num(honorariumMin),
      honorariumMax: num(honorariumMax),
      tags: tags.length ? tags : undefined
    });
  };

  return (
    <Modal title="Filters" onClose={onClose}>
      <div className="modal-body filter-modal-body">
        <div className="filter-panel">
          <label className="filter-label" htmlFor="pf-number">Program ID</label>
          <input id="pf-number" type="number" min={0} placeholder="Enter Program Number"
            value={programNumber} onChange={(e) => setProgramNumber(e.target.value)} />
        </div>

        <div className="filter-panel">
          <label className="filter-label" htmlFor="pf-city">Location</label>
          <input id="pf-city" type="text" placeholder="City, State"
            value={city} onChange={(e) => setCity(e.target.value)} />
        </div>

        <div className="filter-panel">
          <label className="filter-label" htmlFor="pf-specialty">Specialty</label>
          <select id="pf-specialty" value={specialtyId} onChange={(e) => setSpecialtyId(e.target.value)}>
            <option value="">All</option>
            {specialties.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </div>

        <div className="filter-panel">
          <label className="filter-label" htmlFor="pf-approval">Instant Approval</label>
          <select id="pf-approval" value={instantApproval}
            onChange={(e) => setInstantApproval(e.target.value as "both" | "yes" | "no")}>
            <option value="both">Both</option>
            <option value="yes">Yes</option>
            <option value="no">No</option>
          </select>
        </div>

        <div className="filter-panel">
          <div className="filter-label">Amount</div>
          <div className="date-range-edit">
            <input type="number" min={0} aria-label="Honorarium min" placeholder="$0"
              value={honorariumMin} onChange={(e) => setHonorariumMin(e.target.value)} />
            <span className="filter-label">–</span>
            <input type="number" min={0} aria-label="Honorarium max" placeholder="$15,000"
              value={honorariumMax} onChange={(e) => setHonorariumMax(e.target.value)} />
          </div>
        </div>

        <div className="filter-panel">
          <div className="filter-label">Tags</div>
          <div className="filter-tags">
            {PROGRAM_TAGS.map((tag) => (
              <label key={tag} className="filter-check">
                <input type="checkbox" checked={tags.includes(tag)} onChange={() => toggleTag(tag)} />
                {tag}
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

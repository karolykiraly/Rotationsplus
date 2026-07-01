import { useState, useMemo, useRef, useEffect } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import {
  getSpecialties,
  updateStudentNeeds,
  type Specialty,
  type StudentDetail,
  type StudentNeedsInput
} from "../api";
import { INTERESTS_MEDICAL, INTERESTS_DENTAL, SPECIALTY_LOCATIONS, IMPORTANTS } from "./needsOptions";

/** Toggle a value in/out of a string array (selection helper). */
const toggle = (list: string[], value: string): string[] =>
  list.includes(value) ? list.filter((v) => v !== value) : [...list, value];

/** A collapsed multi-select (button showing "label(N)" → checkbox panel), matching production's
 *  specialty-locations control. Dependency-free. */
function MultiSelect({
  label,
  options,
  selected,
  onToggle
}: {
  label: string;
  options: string[];
  selected: string[];
  onToggle: (value: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const onDown = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false); };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);
  return (
    <div className="needs-multiselect" ref={ref}>
      <button type="button" className="needs-multiselect-field" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        {label}({selected.length}) <span aria-hidden="true">▾</span>
      </button>
      {open && (
        <div className="needs-multiselect-panel" role="listbox">
          {options.map((opt) => (
            <label key={opt} className="needs-multiselect-opt">
              <input type="checkbox" checked={selected.includes(opt)} onChange={() => onToggle(opt)} /> {opt}
            </label>
          ))}
        </div>
      )}
    </div>
  );
}

/** The student profile's Needs tab (legacy StudentProfile.js tab 1 / onSaveProfile2): the interests
 *  toggle grid, an "add from the list" specialty picker, preferred locations (+ "Other" free text), and
 *  the "what's most important" priorities. Dental students see the dental interest set and no priorities. */
export function StudentNeedsTab({
  student,
  onSaved
}: {
  student: StudentDetail;
  onSaved: (updated: StudentDetail) => void;
}) {
  const isDental = student.academicStatus === "DentalStudent";
  const interestOptions = isDental ? INTERESTS_DENTAL : INTERESTS_MEDICAL;

  const [interests, setInterests] = useState<string[]>(student.interests ?? []);
  const [preferredSpecialty, setPreferredSpecialty] = useState<string>(student.preferredSpecialty ?? "");
  const [locations, setLocations] = useState<string[]>(student.specialtyLocations ?? []);
  const [customLocation, setCustomLocation] = useState<string>(student.customSpecialtyLocation ?? "");
  const [importants, setImportants] = useState<string[]>(student.importants ?? []);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // "Or add from the list": the full specialty catalog (medical) or the dental set.
  const specialties = useQuery<Specialty[]>({ queryKey: ["specialties"], queryFn: getSpecialties, enabled: !isDental });
  const addFromListOptions = useMemo(
    () => (isDental ? INTERESTS_DENTAL : (specialties.data ?? []).map((s) => s.name)),
    [isDental, specialties.data]
  );

  const otherSelected = locations.includes("Other");

  const save = useMutation({
    mutationFn: (input: StudentNeedsInput) => updateStudentNeeds(student.id, input),
    onSuccess: (updated) => { setBanner({ type: "ok", text: "Needs saved." }); onSaved(updated); },
    onError: (e) => setBanner({ type: "error", text: (e as Error).message })
  });

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (otherSelected && !customLocation.trim()) {
      setBanner({ type: "error", text: "Enter the specialty location for 'Other'." });
      return;
    }
    save.mutate({
      interests: interests.length ? interests : null,
      preferredSpecialty: preferredSpecialty.trim() || null,
      specialtyLocations: locations.length ? locations : null,
      customSpecialtyLocation: otherSelected ? customLocation.trim() || null : null,
      // Priorities don't apply to the dental track.
      importants: isDental ? null : (importants.length ? importants : null)
    });
  };

  return (
    <form onSubmit={submit} className="profile-form">
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="needs-interests-grid" role="group" aria-label="Interests">
        {interestOptions.map((opt) => (
          <button
            key={opt}
            type="button"
            className={`interest-btn${interests.includes(opt) ? " selected" : ""}`}
            aria-pressed={interests.includes(opt)}
            onClick={() => setInterests((cur) => toggle(cur, opt))}
          >
            {opt}
          </button>
        ))}
      </div>

      <div className="field">
        <label htmlFor="needs-add">Or add from the list</label>
        <select id="needs-add" value={preferredSpecialty} onChange={(e) => setPreferredSpecialty(e.target.value)}>
          <option value="">Choose specialties</option>
          {addFromListOptions.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
        </select>
      </div>

      <div className="field">
        <label>Specialty Location(s)</label>
        <MultiSelect
          label="Specialty locations"
          options={SPECIALTY_LOCATIONS}
          selected={locations}
          onToggle={(v) => setLocations((cur) => toggle(cur, v))}
        />
        {otherSelected && (
          <input
            className="needs-other"
            type="text"
            placeholder="Enter Specialty Location"
            value={customLocation}
            onChange={(e) => setCustomLocation(e.target.value)}
            aria-label="Enter Specialty Location"
          />
        )}
      </div>

      {!isDental && (
        <fieldset className="field needs-importants">
          <legend>What are most important to you when finding a clinical rotation?</legend>
          {IMPORTANTS.map((opt) => (
            <label key={opt} className="checkbox">
              <input
                type="checkbox"
                checked={importants.includes(opt)}
                onChange={() => setImportants((cur) => toggle(cur, opt))}
              /> {opt}
            </label>
          ))}
        </fieldset>
      )}

      <div className="profile-form-foot">
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>
          {save.isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}

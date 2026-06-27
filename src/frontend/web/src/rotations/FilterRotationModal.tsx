import { useState } from "react";
import { Modal } from "../components/Modal";
import type { RotationFilter, RotationStatus } from "../api";
import { ROTATION_STATUSES } from "./rotationStatuses";

interface Props {
  initial: RotationFilter;
  onApply: (filter: RotationFilter) => void;
  onClear: () => void;
  onClose: () => void;
}

/** The FilterRotation modal (legacy parity): narrows BOTH rotation sections by date range, retail-amount
 *  range, a single status, the needs-visa flag, and an exact rotation number. Applying replaces the active
 *  filter; Clear resets it. (The legacy dual-range slider is rendered as min/max inputs.) */
export function FilterRotationModal({ initial, onApply, onClear, onClose }: Props) {
  const [startFrom, setStartFrom] = useState(initial.startFrom ?? "");
  const [endTo, setEndTo] = useState(initial.endTo ?? "");
  const [retailMin, setRetailMin] = useState(initial.retailMin?.toString() ?? "");
  const [retailMax, setRetailMax] = useState(initial.retailMax?.toString() ?? "");
  const [needsVisa, setNeedsVisa] = useState(initial.needsVisa ?? false);
  const [status, setStatus] = useState<RotationStatus | "">(initial.status ?? "");
  const [rotationNumber, setRotationNumber] = useState(initial.rotationNumber?.toString() ?? "");

  const apply = () => {
    const num = (s: string) => (s.trim() === "" ? undefined : Number(s));
    onApply({
      startFrom: startFrom || undefined,
      endTo: endTo || undefined,
      retailMin: num(retailMin),
      retailMax: num(retailMax),
      needsVisa: needsVisa || undefined,
      status: status || undefined,
      rotationNumber: num(rotationNumber)
    });
  };

  return (
    <Modal title="Filters" onClose={onClose}>
      <div className="modal-body filter-modal-body">
        <div className="filter-panel">
          <div className="filter-label">Start and End Date</div>
          <div className="date-range-edit">
            <input type="date" aria-label="Start from" value={startFrom} onChange={(e) => setStartFrom(e.target.value)} />
            <span className="filter-label">–</span>
            <input type="date" aria-label="End to" value={endTo} onChange={(e) => setEndTo(e.target.value)} />
          </div>
        </div>

        <div className="filter-panel">
          <div className="filter-label">Amount</div>
          <div className="date-range-edit">
            <input type="number" min={0} aria-label="Amount min" placeholder="$0" value={retailMin} onChange={(e) => setRetailMin(e.target.value)} />
            <span className="filter-label">–</span>
            <input type="number" min={0} aria-label="Amount max" placeholder="$10,000" value={retailMax} onChange={(e) => setRetailMax(e.target.value)} />
          </div>
        </div>

        <div className="filter-panel">
          <label className="filter-check">
            <input type="checkbox" checked={needsVisa} onChange={(e) => setNeedsVisa(e.target.checked)} />
            Needs Visa
          </label>
        </div>

        <div className="filter-panel">
          <label className="filter-label" htmlFor="filter-status">Status</label>
          <select id="filter-status" value={status} onChange={(e) => setStatus(e.target.value as RotationStatus | "")}>
            <option value="">All</option>
            {ROTATION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
        </div>

        <div className="filter-panel">
          <label className="filter-label" htmlFor="filter-rotnum">Rotation Number</label>
          <input
            id="filter-rotnum"
            type="number"
            min={0}
            placeholder="Enter Rotation Number"
            value={rotationNumber}
            onChange={(e) => setRotationNumber(e.target.value)}
          />
        </div>
      </div>
      <div className="modal-foot">
        <button className="btn btn-ghost" type="button" onClick={onClear}>Clear filters</button>
        <button className="btn btn-primary" type="button" onClick={apply}>Apply filters</button>
      </div>
    </Modal>
  );
}

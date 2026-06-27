import { useState } from "react";
import type { Program, RotationDetail, RotationInput } from "../api";
import { programCode, programDisplayName } from "../programs/programTypes";
import { ROTATION_STATUSES } from "./rotationStatuses";

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

const money = (amount: number) =>
  `$${amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

interface Props {
  detail: RotationDetail;
  programs: Program[];
  pending: boolean;
  serverError: string | null;
  onSave: (input: RotationInput) => void;
  onClose: () => void;
}

/** The "Selected Rotation" detail panel shown when an admin clicks View on a rotation row (legacy parity).
 *  It surfaces the booking's identity + money and the three production edit affordances — Replace (pick a
 *  different program), Change (the date range), and the status dropdown — saved together via the single
 *  reliable update endpoint. Refunded is intentionally not offered (refunding is a money action). */
export function SelectedRotationPanel({ detail, programs, pending, serverError, onSave, onClose }: Props) {
  const [programId, setProgramId] = useState(detail.programId);
  const [startDate, setStartDate] = useState(detail.startDate);
  const [endDate, setEndDate] = useState(detail.endDate);
  const [status, setStatus] = useState(detail.status);
  const [replacing, setReplacing] = useState(false);
  const [changingDate, setChangingDate] = useState(false);

  // Offer the current status plus the server's allowed transitions, minus Refunded (a money action done
  // via the refund flow, not a plain status edit) — same rule the old edit modal used.
  const statusValues = [detail.status, ...detail.allowedNextStatuses.filter((s) => s !== "Refunded")];
  const statusOptions = ROTATION_STATUSES.filter((s) => statusValues.includes(s.value));

  const selectedProgram = programs.find((p) => p.id === programId);
  const programLabel = selectedProgram
    ? `${programDisplayName(selectedProgram.specialtyName)} · ${programCode(selectedProgram.programType, selectedProgram.programNumber)}`
    : `${programDisplayName(detail.specialtyName)} · ${programCode(detail.programType, detail.programNumber)}`;

  const save = () => {
    if (!detail.studentId) return; // a legacy row with no directory student can't be re-saved here
    onSave({ programId, studentId: detail.studentId, startDate, endDate, status });
  };

  return (
    <div className="lead-page rotation-detail" aria-label="Selected rotation">
      <div className="list-head">
        <h2 className="heading-xxs">Selected Rotation</h2>
      </div>

      {serverError && <div className="banner error" role="alert">{serverError}</div>}

      <div className="rotation-detail-grid">
        {/* Left: program identity + Replace */}
        <dl className="detail-col">
          <div className="detail-row">
            <dt className="place-holder">Rotation Number</dt>
            <dd className="heading-xxxs">R{detail.rotationNumber}</dd>
          </div>
          <div className="detail-row">
            <dt className="place-holder">Preceptor Name</dt>
            <dd className="heading-xxxs-normal">{detail.preceptorName ?? "—"}</dd>
          </div>
          {replacing ? (
            <div className="detail-row">
              <dt className="place-holder">Program</dt>
              <dd>
                <select
                  aria-label="Replace program"
                  value={programId}
                  onChange={(e) => setProgramId(e.target.value)}
                >
                  {programs.map((p) => (
                    <option key={p.id} value={p.id}>
                      {programDisplayName(p.specialtyName)} · {programCode(p.programType, p.programNumber)}
                    </option>
                  ))}
                </select>
              </dd>
            </div>
          ) : (
            <>
              <div className="detail-row">
                <dt className="place-holder">Program ID</dt>
                <dd className="heading-xxxs-normal">{programCode(detail.programType, detail.programNumber)}</dd>
              </div>
              <div className="detail-row">
                <dt className="place-holder">Program Name</dt>
                <dd className="heading-xxxs-normal">{programLabel}</dd>
              </div>
            </>
          )}
          <button className="btn btn-outline button-sm" type="button" onClick={() => setReplacing((v) => !v)}>
            {replacing ? "Done" : "Replace"}
          </button>
        </dl>

        {/* Right: student + money + dates + status */}
        <dl className="detail-col">
          <div className="detail-row">
            <dt className="place-holder">Student Name</dt>
            <dd className="heading-xxxs-normal">{detail.studentName}</dd>
          </div>
          <div className="detail-row">
            <dt className="place-holder">Paid Amount</dt>
            <dd className="heading-xxxs-normal">{money(detail.paidAmount)}</dd>
          </div>
          <div className="detail-row">
            <dt className="place-holder">Rotation Cost</dt>
            <dd className="heading-xxxs-normal">{money(detail.retailAmount)}</dd>
          </div>
          <div className="detail-row">
            <dt className="place-holder">Start and End Date</dt>
            <dd>
              {changingDate ? (
                <span className="date-range-edit">
                  <input type="date" aria-label="Start date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
                  <input type="date" aria-label="End date" value={endDate} onChange={(e) => setEndDate(e.target.value)} />
                </span>
              ) : (
                <span className="heading-xxxs-normal">{formatDate(startDate)} – {formatDate(endDate)}</span>
              )}
            </dd>
          </div>
          <button className="btn btn-outline button-sm" type="button" onClick={() => setChangingDate((v) => !v)}>
            {changingDate ? "Done" : "Change"}
          </button>
          <div className="detail-row">
            <dt className="place-holder"><label htmlFor="rot-status">Current status</label></dt>
            <dd>
              <select id="rot-status" value={status} onChange={(e) => setStatus(e.target.value as typeof status)}>
                {statusOptions.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </dd>
          </div>
        </dl>
      </div>

      <div className="modal-foot">
        <button className="btn btn-ghost" type="button" onClick={onClose} disabled={pending}>Cancel</button>
        <button className="btn btn-primary" type="button" onClick={save} disabled={pending || !detail.studentId}>
          {pending ? "Saving…" : "Save"}
        </button>
      </div>
    </div>
  );
}

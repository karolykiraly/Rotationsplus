import { useState } from "react";
import { useCustomerRotations } from "./useCustomerRotations";
import { PaymentModal } from "./PaymentModal";
import { programTypeLabel } from "../programs/programTypes";
import { rotationStatusLabel } from "../rotations/rotationStatuses";

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/** The signed-in student's rotation tracker (GET /api/customer/rotations). */
export function MyRotationsPage() {
  const rotations = useCustomerRotations();
  const rows = rotations.data ?? [];
  // The rotation whose deposit dialog is open, if any.
  const [payingRotationId, setPayingRotationId] = useState<string | null>(null);
  // A rotation whose deposit just succeeded, so we can show a brief confirmation.
  const [paidRotationId, setPaidRotationId] = useState<string | null>(null);

  return (
    <>
      <div className="page-head">
        <div>
          <h2>My rotations</h2>
          <p>The clinical rotations booked under your account.</p>
        </div>
      </div>

      {rotations.isLoading && <div className="card state">Loading your rotations…</div>}
      {rotations.isError && (
        <div className="card state">Couldn’t load your rotations: {(rotations.error as Error).message}</div>
      )}
      {!rotations.isLoading && !rotations.isError && rows.length === 0 && (
        <div className="card state">You don’t have any rotations yet.</div>
      )}

      {rows.length > 0 && (
        <div className="program-grid">
          {rows.map((r) => (
            <div key={r.id} className="program-card" aria-label={`${r.specialtyName} rotation`}>
              <div className="pc-specialty">{r.specialtyName}</div>
              <div className="pc-type">{programTypeLabel(r.programType)}</div>
              <div className="pc-meta">{formatDate(r.startDate)} – {formatDate(r.endDate)} · {r.weeks} wks</div>
              {r.preceptorName && <div className="pc-preceptor">with {r.preceptorName}</div>}
              <div className="pc-status"><span className="badge">{rotationStatusLabel(r.status)}</span></div>
              {r.status === "Pending" && (
                <div className="pc-action">
                  <button type="button" className="btn btn-primary" onClick={() => setPayingRotationId(r.id)}>
                    Pay deposit
                  </button>
                </div>
              )}
              {paidRotationId === r.id && (
                <div className="pc-paid" role="status">Deposit paid — your rotation is approved.</div>
              )}
            </div>
          ))}
        </div>
      )}

      {payingRotationId && (
        <PaymentModal
          rotationId={payingRotationId}
          onClose={() => setPayingRotationId(null)}
          onPaid={() => {
            setPaidRotationId(payingRotationId);
            setPayingRotationId(null);
          }}
        />
      )}
    </>
  );
}

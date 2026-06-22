import { useState } from "react";
import { useCustomerRotations } from "./useCustomerRotations";
import { PaymentModal } from "./PaymentModal";
import { DocumentsModal } from "./DocumentsModal";
import type { CustomerRotation } from "./customerApi";
import { programTypeLabel } from "../programs/programTypes";
import { rotationStatusLabel } from "../rotations/rotationStatuses";

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/** One labelled cell in a tracker row (header label above the value), cloned from the live table. */
function Cell({
  header,
  value,
  valueClass = "responsive-col-value",
  flex = 2
}: {
  header: string;
  value: React.ReactNode;
  valueClass?: string;
  flex?: number;
}) {
  return (
    <div className="responsive-col" style={{ flex }}>
      <div className="responsive-col-header">{header}</div>
      <div className={valueClass}>{value}</div>
    </div>
  );
}

/** The signed-in student's rotation tracker (GET /api/customer/rotations), cloned to the live
 *  "Rotations Tracker" table: labelled responsive-row cards + progressive reveal. Keeps our
 *  Pay-deposit money path on Pending rotations. The "Documents" column reflects the real required-docs
 *  status and opens the per-rotation document checklist; Program ID stays a placeholder. */
export function MyRotationsPage() {
  const rotations = useCustomerRotations();
  const rows = rotations.data ?? [];
  const [showCount, setShowCount] = useState(5);
  // The rotation whose deposit dialog is open, if any.
  const [payingRotationId, setPayingRotationId] = useState<string | null>(null);
  // A rotation whose deposit just succeeded, so we can show a brief confirmation.
  const [paidRotationId, setPaidRotationId] = useState<string | null>(null);
  // The rotation whose documents checklist is open, if any.
  const [docsRotation, setDocsRotation] = useState<CustomerRotation | null>(null);

  const shown = rows.slice(0, showCount);
  const remaining = rows.length - shown.length;

  return (
    <div className="tracker-page">
      <div className="tracker">
        <h2 className="tracker-title">Rotations Tracker</h2>

        {rotations.isLoading && <div className="card state">Loading your rotations…</div>}
        {rotations.isError && (
          <div className="card state">Couldn’t load your rotations: {(rotations.error as Error).message}</div>
        )}
        {!rotations.isLoading && !rotations.isError && rows.length === 0 && (
          <div className="card state">You don’t have any rotations yet.</div>
        )}

        {rows.length > 0 && (
          <div className="div-table">
            {shown.map((r) => (
              <div key={r.id} className="responsive-row" aria-label={`${r.specialtyName} rotation`}>
                {/* Program ID stays a placeholder until the customer rotation DTO carries the program code. */}
                <Cell header="Program ID" value="—" valueClass="responsive-col-strong" />
                <Cell header="Specialty" value={r.specialtyName} valueClass="responsive-col-value pc-specialty" />
                <Cell header="Type" value={programTypeLabel(r.programType)} />
                <Cell header="Preceptor" value={r.preceptorName ?? "—"} />
                <Cell header="Rotation Number" value={r.rotationNumber ? `R${r.rotationNumber}` : "—"} valueClass="responsive-col-strong" />
                <Cell header="Start Date" value={formatDate(r.startDate)} />
                <Cell header="End Date" value={formatDate(r.endDate)} />
                <Cell header="Weeks" value={r.weeks} />
                <Cell
                  header="Documents"
                  value={
                    r.documentsState === "NotRequired" ? (
                      "—"
                    ) : (
                      <button type="button" className="doc-link" onClick={() => setDocsRotation(r)}>
                        {r.documentsState === "Missing" ? "Documents Missing" : "All Documents Uploaded"}
                      </button>
                    )
                  }
                  valueClass={r.documentsState === "Missing" ? "responsive-col-value doc-missing" : "responsive-col-value"}
                />
                <Cell header="Status" value={<span className="badge">{rotationStatusLabel(r.status)}</span>} />
                {(r.status === "Pending" || paidRotationId === r.id) && (
                  <div className="responsive-col responsive-col-action" style={{ flex: 2 }}>
                    {r.status === "Pending" && (
                      <button type="button" className="btn btn-primary button-sm" onClick={() => setPayingRotationId(r.id)}>
                        Pay deposit
                      </button>
                    )}
                    {paidRotationId === r.id && (
                      <div className="pc-paid" role="status">Deposit paid — your rotation is approved.</div>
                    )}
                  </div>
                )}
              </div>
            ))}

            {remaining > 0 && (
              <div className="tracker-more">
                <button type="button" className="btn show-more button-sm" onClick={() => setShowCount((c) => c + 5)}>
                  Show more ({remaining})
                </button>
              </div>
            )}
          </div>
        )}
      </div>

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

      {docsRotation && (
        <DocumentsModal
          rotationId={docsRotation.id}
          rotationLabel={docsRotation.rotationNumber ? `R${docsRotation.rotationNumber}` : docsRotation.specialtyName}
          onClose={() => setDocsRotation(null)}
        />
      )}
    </div>
  );
}

import { useMemo, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import {
  clearDocumentFile,
  getStudentDocuments,
  setDocumentStatus,
  uploadDocumentFile,
  type AdminRotationDocument,
  type DocumentStatus
} from "../api";

interface Props {
  studentId: string;
  /** Shown in the dialog title so the admin knows whose documents they're reviewing. */
  studentName: string;
  onClose: () => void;
}

const STATUSES: DocumentStatus[] = ["UploadNeeded", "Submitted", "Approved", "Rejected", "Expired"];

const STATUS_LABEL: Record<DocumentStatus, string> = {
  UploadNeeded: "Upload needed",
  Submitted: "Submitted",
  Approved: "Approved",
  Rejected: "Rejected",
  Expired: "Expired"
};

const STATUS_CLASS: Record<DocumentStatus, string> = {
  UploadNeeded: "badge badge-warn",
  Submitted: "badge",
  Approved: "badge badge-ok",
  Rejected: "badge badge-danger",
  Expired: "badge badge-danger"
};

// The legacy admin accepts the full document set; the server is the authoritative magic-byte validator.
const ACCEPT = "application/pdf,image/jpeg,image/png,image/bmp,.doc,.docx";

function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/** One review row: type + rotation + due date, a status dropdown (with a rejection-reason input when
 *  Rejected), the uploaded-file link, and upload/replace + remove-on-behalf controls. */
function ReviewRow({ doc, onChanged }: { doc: AdminRotationDocument; onChanged: () => void }) {
  const fileInput = useRef<HTMLInputElement>(null);
  // While the admin is choosing "Rejected", hold the pending value + reason locally until they apply.
  const [pendingReject, setPendingReject] = useState(false);
  const [reason, setReason] = useState(doc.rejectionReason ?? "");

  const status = useMutation({
    mutationFn: (v: { status: DocumentStatus; reason: string | null }) =>
      setDocumentStatus(doc.id, v.status, v.reason),
    onSuccess: () => { setPendingReject(false); onChanged(); }
  });

  const upload = useMutation({
    mutationFn: (file: File) => uploadDocumentFile(doc.id, file),
    onSuccess: onChanged
  });

  const clear = useMutation({
    mutationFn: () => clearDocumentFile(doc.id),
    onSuccess: onChanged
  });

  const busy = status.isPending || upload.isPending || clear.isPending;

  const onStatusPick = (next: DocumentStatus) => {
    if (next === "Rejected") {
      setReason(doc.rejectionReason ?? "");
      setPendingReject(true); // reveal the reason input; apply on Save
    } else {
      setPendingReject(false);
      status.mutate({ status: next, reason: null });
    }
  };

  const onPick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) upload.mutate(file);
    e.target.value = ""; // allow re-selecting the same filename
  };

  const selectValue = pendingReject ? "Rejected" : doc.status;
  const err = (status.error ?? upload.error ?? clear.error) as Error | undefined;
  // A student can require the same document type in two rotations, so the type name alone is not
  // unique — qualify every label with the rotation number for distinct accessible names.
  const label = `${doc.documentTypeName} (R${doc.rotationNumber})`;
  // Show the rejection-reason editor while choosing Rejected OR for an already-rejected doc (so the
  // admin can edit + re-save the reason without first bouncing to another status).
  const showReason = pendingReject || doc.status === "Rejected";

  return (
    <tr aria-label={label}>
      <td>
        <div className="heading-xxxs">{doc.documentTypeName}</div>
        <div className="doc-due">R{doc.rotationNumber} · Due {formatDate(doc.dueDate)}</div>
        {err && <div className="doc-reject" role="alert">{err.message}</div>}
      </td>
      <td>
        {doc.fileUrl ? (
          <a className="doc-file" href={doc.fileUrl} target="_blank" rel="noopener noreferrer">
            {doc.fileName ?? "View file"}
          </a>
        ) : (
          <span className="doc-due">No file</span>
        )}
      </td>
      <td>
        <span className={STATUS_CLASS[doc.status]}>{STATUS_LABEL[doc.status]}</span>
      </td>
      <td className="doc-review-actions">
        <select
          aria-label={`Set status for ${label}`}
          value={selectValue}
          onChange={(e) => onStatusPick(e.target.value as DocumentStatus)}
          disabled={busy}
        >
          {STATUSES.map((s) => <option key={s} value={s}>{STATUS_LABEL[s]}</option>)}
        </select>
        {showReason && (
          <div className="doc-add">
            <input
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Reason for rejection"
              aria-label={`Rejection reason for ${label}`}
            />
            <button
              type="button"
              className="btn btn-primary button-sm"
              onClick={() => status.mutate({ status: "Rejected", reason: reason.trim() })}
              disabled={!reason.trim() || busy}
            >
              Save
            </button>
          </div>
        )}

        <input
          ref={fileInput}
          type="file"
          accept={ACCEPT}
          className="doc-file-input"
          onChange={onPick}
          aria-label={`Upload ${label} on behalf`}
        />
        <button type="button" className="btn-link" onClick={() => fileInput.current?.click()} disabled={busy}>
          {upload.isPending ? "Uploading…" : doc.fileName ? "Replace" : "Upload"}
        </button>
        {doc.fileName && (
          <button type="button" className="btn-link danger" onClick={() => clear.mutate()} disabled={busy}>
            Remove
          </button>
        )}
      </td>
    </tr>
  );
}

/** Admin per-student document review (GET /api/students/{id}/documents): every document across the
 *  student's rotations, with a status dropdown, on-behalf upload/replace/remove, and a rotation filter.
 *  The rewrite of the legacy Student-Profile "Documents" tab (PHASE 2g-3c). */
export function StudentDocumentsModal({ studentId, studentName, onClose }: Props) {
  const [rotationFilter, setRotationFilter] = useState<string>(""); // "" = all rotations

  const docs = useQuery({
    queryKey: ["student-documents", studentId],
    queryFn: () => getStudentDocuments(studentId)
  });

  const all = docs.data ?? [];
  // Distinct rotation numbers for the filter (sorted ascending).
  const rotations = useMemo(
    () => [...new Set(all.map((d) => d.rotationNumber))].sort((a, b) => a - b),
    [all]
  );
  const rows = rotationFilter ? all.filter((d) => String(d.rotationNumber) === rotationFilter) : all;

  return (
    <Modal title={`Documents — ${studentName}`} onClose={onClose} wide>
      <div className="modal-body">
        {docs.isLoading && <div className="state">Loading documents…</div>}
        {docs.isError && (
          <div className="banner error" role="alert">
            Couldn’t load documents: {(docs.error as Error).message}
          </div>
        )}
        {!docs.isLoading && !docs.isError && all.length === 0 && (
          <div className="state">This student has no rotation documents.</div>
        )}

        {all.length > 0 && (
          <>
            {rotations.length > 1 && (
              <div className="doc-config-row">
                <label htmlFor="doc-rot-filter">Filter by rotation</label>
                <select
                  id="doc-rot-filter"
                  value={rotationFilter}
                  onChange={(e) => setRotationFilter(e.target.value)}
                  className="doc-due-input"
                >
                  <option value="">All rotations</option>
                  {rotations.map((n) => <option key={n} value={String(n)}>R{n}</option>)}
                </select>
              </div>
            )}
            <table className="program-table doc-review-table">
              <thead>
                <tr>
                  <th>Document</th>
                  <th>File</th>
                  <th>Status</th>
                  <th>Review</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((d) => (
                  <ReviewRow key={d.id} doc={d} onChanged={() => void docs.refetch()} />
                ))}
              </tbody>
            </table>
          </>
        )}
      </div>

      <div className="modal-foot">
        <button type="button" className="btn btn-ghost" onClick={onClose}>Close</button>
      </div>
    </Modal>
  );
}

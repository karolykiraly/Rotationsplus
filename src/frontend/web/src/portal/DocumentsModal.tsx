import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { getRotationDocuments, uploadRotationDocument, type DocumentStatus, type RotationDocument } from "./customerApi";

interface DocumentsModalProps {
  rotationId: string;
  /** Shown in the dialog title so the student knows which rotation's documents they're viewing. */
  rotationLabel: string;
  onClose: () => void;
}

const STATUS_LABEL: Record<DocumentStatus, string> = {
  UploadNeeded: "Upload needed",
  Submitted: "Submitted — in review",
  Approved: "Approved",
  Rejected: "Rejected",
  Expired: "Expired"
};

/** Pink/blue badge tone per status — reuses the global badge classes. */
const STATUS_CLASS: Record<DocumentStatus, string> = {
  UploadNeeded: "badge badge-warn",
  Submitted: "badge",
  Approved: "badge badge-ok",
  Rejected: "badge badge-danger",
  Expired: "badge badge-danger"
};

function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

/** One document row: type + status + due date, the uploaded-file link (if any), a rejection reason
 *  (if rejected), and an Upload/Replace control. Approved documents are locked (no re-upload). */
function DocumentRow({
  doc,
  rotationId,
  onUploadStart,
  onUploaded
}: {
  doc: RotationDocument;
  rotationId: string;
  onUploadStart: () => void;
  onUploaded: () => void;
}) {
  const fileInput = useRef<HTMLInputElement>(null);

  const upload = useMutation({
    mutationFn: (file: File) => uploadRotationDocument(rotationId, doc.id, file),
    onSuccess: onUploaded
  });

  const onPick = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      onUploadStart(); // clear any prior success banner before this attempt
      upload.mutate(file);
    }
    e.target.value = ""; // allow re-selecting the same filename
  };

  const locked = doc.status === "Approved";

  return (
    <div className="doc-row" aria-label={doc.documentTypeName}>
      <div className="doc-main">
        <div className="doc-name">{doc.documentTypeName}</div>
        <div className="doc-meta">
          <span className={STATUS_CLASS[doc.status]}>{STATUS_LABEL[doc.status]}</span>
          <span className="doc-due">Due {formatDate(doc.dueDate)}</span>
          {doc.fileUrl && (
            <a className="doc-file" href={doc.fileUrl} target="_blank" rel="noopener noreferrer">
              {doc.fileName ?? "View file"}
            </a>
          )}
        </div>
        {doc.status === "Rejected" && doc.rejectionReason && (
          <div className="doc-reject">Rejected: {doc.rejectionReason}</div>
        )}
        {upload.isError && (
          <div className="doc-reject" role="alert">Upload failed: {(upload.error as Error).message}</div>
        )}
      </div>

      <div className="doc-action">
        {locked ? (
          <span className="doc-locked">Approved</span>
        ) : (
          <>
            <input
              ref={fileInput}
              type="file"
              accept="application/pdf,image/jpeg,image/png"
              className="doc-file-input"
              onChange={onPick}
              aria-label={`Upload ${doc.documentTypeName}`}
            />
            <button
              type="button"
              className="btn btn-primary button-sm"
              onClick={() => fileInput.current?.click()}
              disabled={upload.isPending}
            >
              {upload.isPending ? "Uploading…" : doc.fileName ? "Replace" : "Upload"}
            </button>
          </>
        )}
      </div>
    </div>
  );
}

/** The student's per-rotation document checklist (GET .../documents) with upload. On any successful
 *  upload, refetches the checklist and invalidates the rotations tracker so the "Documents" column
 *  updates. PDF/JPEG/PNG, 10 MB — the server is the authoritative validator. */
export function DocumentsModal({ rotationId, rotationLabel, onClose }: DocumentsModalProps) {
  const queryClient = useQueryClient();
  const [justUploaded, setJustUploaded] = useState(false);

  const docs = useQuery({
    queryKey: ["rotation-documents", rotationId],
    queryFn: () => getRotationDocuments(rotationId)
  });

  const onUploaded = () => {
    setJustUploaded(true);
    void docs.refetch();
    void queryClient.invalidateQueries({ queryKey: ["customer-rotations"] });
  };

  const rows = docs.data ?? [];

  return (
    <Modal title={`Documents — ${rotationLabel}`} onClose={onClose}>
      <div className="modal-body">
        {docs.isLoading && <div className="state">Loading your documents…</div>}
        {docs.isError && (
          <div className="banner error" role="alert">
            Couldn’t load your documents: {(docs.error as Error).message}
          </div>
        )}
        {!docs.isLoading && !docs.isError && rows.length === 0 && (
          <div className="state">This rotation has no required documents.</div>
        )}

        {rows.length > 0 && (
          <>
            {justUploaded && (
              <div className="banner ok" role="status">
                Uploaded — your document is now in review.
              </div>
            )}
            <div className="doc-list">
              {rows.map((d) => (
                <DocumentRow
                  key={d.id}
                  doc={d}
                  rotationId={rotationId}
                  onUploadStart={() => setJustUploaded(false)}
                  onUploaded={onUploaded}
                />
              ))}
            </div>
          </>
        )}
      </div>

      <div className="modal-foot">
        <button type="button" className="btn btn-ghost" onClick={onClose}>Close</button>
      </div>
    </Modal>
  );
}

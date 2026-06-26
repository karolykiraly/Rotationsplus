import { useEffect, useState } from "react";
import { Modal } from "../components/Modal";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import type { Preceptor } from "../api";
import { usePermissionQueue } from "./usePermissionQueue";

const PAGE_SIZE = 10;

/** The admin "Permissions" screen — the preceptor approval queue. Lists preceptors awaiting approval
 *  (status Pending) and lets an admin Approve (activate) or Reject (with a required reason) each one.
 *  The agreement-PDF / W9 / doc-due-date pieces of the legacy permission screen are later slices. */
export function PermissionPage() {
  const { user } = useMe();
  const [page, setPage] = useState(1);
  const { list, approve, reject } = usePermissionQueue(page, PAGE_SIZE);

  const [rejecting, setRejecting] = useState<Preceptor | null>(null);
  const [reason, setReason] = useState("");
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // Step back if the queue shrinks past the current page (a decision removes a row). Gated on fresh data;
  // declared before the admin guard so hooks stay stable.
  const totalItems = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const settled = !list.isPlaceholderData;
  useEffect(() => {
    if (settled && page > totalPages) setPage(totalPages);
  }, [settled, page, totalPages]);

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to review preceptor approvals.</div>;
  }

  const rows = list.data?.items ?? [];

  const onApprove = (p: Preceptor) => {
    approve.mutate(p.id, {
      onSuccess: () => setBanner({ type: "ok", text: `Approved ${p.fullName}.` }),
      onError: (e) => setBanner({ type: "error", text: (e as Error).message })
    });
  };

  const confirmReject = () => {
    if (!rejecting) return;
    if (reason.trim().length === 0) {
      setFormError("A rejection reason is required.");
      return;
    }
    const name = rejecting.fullName;
    reject.mutate(
      { id: rejecting.id, reason: reason.trim() },
      {
        onSuccess: () => { setRejecting(null); setReason(""); setBanner({ type: "ok", text: `Rejected ${name}.` }); },
        onError: (e) => setFormError((e as Error).message)
      }
    );
  };

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="lead-page">
        {list.isLoading && <div className="state">Loading approvals…</div>}
        {list.isError && <div className="state">Couldn’t load approvals: {(list.error as Error).message}</div>}
        {!list.isLoading && list.isFetching && <div className="state subtle" role="status">Updating…</div>}

        {!list.isLoading && !list.isError && (
          rows.length === 0 ? (
            <div className="state">No preceptors are awaiting approval.</div>
          ) : (
            <table className="program-table" aria-busy={list.isFetching}>
              <tbody>
                {rows.map((p) => (
                  <tr key={p.id} className="rot-row">
                    <td className="first-td">
                      <div className="place-holder">Name</div>
                      <div className="heading-xxxs">{p.fullName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Email</div>
                      <div className="heading-xxxs-normal">{p.email}</div>
                    </td>
                    <td>
                      <div className="place-holder">Specialty</div>
                      <div className="heading-xxxs-normal">{p.primarySpecialtyName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Location</div>
                      <div className="heading-xxxs-normal">{[p.city, p.state].filter(Boolean).join(", ") || "—"}</div>
                    </td>
                    <td className="last-td">
                      <div className="row-actions">
                        <button
                          className="btn btn-primary button-sm"
                          onClick={() => onApprove(p)}
                          disabled={approve.isPending && approve.variables === p.id}
                        >
                          {approve.isPending && approve.variables === p.id ? "Approving…" : "Approve"}
                        </button>
                        <button
                          className="btn-link danger"
                          onClick={() => { setFormError(null); setReason(""); setRejecting(p); }}
                        >
                          Reject
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        <Pagination page={page} pageSize={PAGE_SIZE} totalItems={totalItems} onChange={setPage} />
      </div>

      {rejecting && (
        <Modal title={`Reject ${rejecting.fullName}`} onClose={() => setRejecting(null)}>
          <div className="modal-body">
            <label htmlFor="reject-reason">Reason (shown in the audit record)</label>
            <textarea
              id="reject-reason"
              rows={4}
              value={reason}
              maxLength={1000}
              onChange={(e) => { setReason(e.target.value); if (formError) setFormError(null); }}
              placeholder="Why is this preceptor being rejected?"
            />
            {formError && <div className="err" role="alert">{formError}</div>}
          </div>
          <div className="modal-foot">
            <button className="btn btn-ghost" onClick={() => setRejecting(null)} disabled={reject.isPending}>Cancel</button>
            <button className="btn btn-danger" onClick={confirmReject} disabled={reject.isPending}>
              {reject.isPending ? "Rejecting…" : "Reject"}
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}

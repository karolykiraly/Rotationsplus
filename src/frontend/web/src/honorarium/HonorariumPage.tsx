import { useEffect, useState } from "react";
import { Modal } from "../components/Modal";
import { Pagination } from "../components/Pagination";
import { Tabs } from "../components/Tabs";
import { useMe } from "../useMe";
import type { Honorarium, HonorariumStage } from "../api";
import { useHonorariums } from "./useHonorariums";

const PAGE_SIZE = 10;

/** The three payout-screen tabs, in order. The tab index maps to the stage filter. */
const STAGES: { stage: HonorariumStage; label: string }[] = [
  { stage: "Deposit", label: "Honorarium Deposit" },
  { stage: "Start", label: "Honorarium Start" },
  { stage: "Evaluation", label: "Honorarium Evaluation" }
];

// Legacy display: "$450" (no forced cents), showing cents only when the amount has them.
const money = (amount: number) =>
  `$${amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

const formatDate = (iso: string) => {
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
};

/** The admin "Honorarium" screen — the preceptor payout queue, split into the Deposit / Start / Evaluation
 *  stage tabs. Each stage is a server-paginated list; an admin marks a stage paid (the server enforces that
 *  stages are paid in order) and, on the Deposit tab, toggles the refunded bookkeeping flag. Rows are
 *  generated automatically when a rotation's deposit succeeds. */
export function HonorariumPage() {
  const { user } = useMe();
  const [tab, setTab] = useState(0);
  const [page, setPage] = useState(1);
  const { stage } = STAGES[tab];
  const { list, pay, setRefund, remove } = useHonorariums(stage, page, PAGE_SIZE);

  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);
  const [deleting, setDeleting] = useState<Honorarium | null>(null);

  // Step back if the queue shrinks past the current page. Gated on fresh data; declared before the admin
  // guard so hooks stay stable across renders.
  const totalItems = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const settled = !list.isPlaceholderData;
  useEffect(() => {
    if (settled && page > totalPages) setPage(totalPages);
  }, [settled, page, totalPages]);

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage honorariums.</div>;
  }

  const rows = list.data?.items ?? [];
  const isDepositTab = stage === "Deposit";
  const isEvaluationTab = stage === "Evaluation";

  const switchTab = (index: number) => {
    setTab(index);
    setPage(1);
    setBanner(null);
  };

  const onPay = (h: Honorarium) => {
    pay.mutate(h.id, {
      onSuccess: () => setBanner({ type: "ok", text: `Marked the ${h.stage.toLowerCase()} honorarium for R${h.rotationNumber} paid.` }),
      onError: (e) => setBanner({ type: "error", text: (e as Error).message })
    });
  };

  const onToggleRefund = (h: Honorarium, refunded: boolean) => {
    setRefund.mutate(
      { id: h.id, refunded },
      { onError: (e) => setBanner({ type: "error", text: (e as Error).message }) }
    );
  };

  const confirmDelete = () => {
    if (!deleting) return;
    const label = `R${deleting.rotationNumber}`;
    remove.mutate(deleting.id, {
      onSuccess: () => { setDeleting(null); setBanner({ type: "ok", text: `Deleted the deposit honorarium for ${label}.` }); },
      onError: (e) => setBanner({ type: "error", text: (e as Error).message })
    });
  };

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <Tabs labels={STAGES.map((s) => s.label)} active={tab} onChange={switchTab} />

      <div className="lead-page">
        <div className="list-head">
          <h2 className="heading-xxs">{STAGES[tab].label}</h2>
          <span className="list-count">{totalItems} items</span>
        </div>

        {list.isLoading && <div className="state">Loading honorariums…</div>}
        {list.isError && <div className="state">Couldn’t load honorariums: {(list.error as Error).message}</div>}
        {!list.isLoading && list.isFetching && <div className="state subtle" role="status">Updating…</div>}

        {!list.isLoading && !list.isError && (
          rows.length === 0 ? (
            <div className="state">No honorariums in this stage.</div>
          ) : (
            <table className="program-table" aria-busy={list.isFetching}>
              <tbody>
                {rows.map((h) => {
                  const payPending = pay.isPending && pay.variables === h.id;
                  const refundPending = setRefund.isPending && setRefund.variables?.id === h.id;
                  const deletePending = remove.isPending && remove.variables === h.id;
                  return (
                    <tr key={h.id} className="rot-row">
                      <td className="first-td">
                        <div className="place-holder">Rotation Number</div>
                        <div className="heading-xxxs">R{h.rotationNumber}</div>
                      </td>
                      <td>
                        <div className="place-holder">Preceptor Name</div>
                        <div className="heading-xxxs-normal">{h.preceptorName}</div>
                      </td>
                      <td>
                        <div className="place-holder">Student Name</div>
                        <div className="heading-xxxs-normal">{h.studentName}</div>
                      </td>
                      <td>
                        <div className="place-holder">Honorarium Amount</div>
                        <div className="heading-xxxs-normal">{money(h.amount)}</div>
                      </td>
                      {isEvaluationTab ? (
                        <>
                          <td>
                            <div className="place-holder">Evaluation Upload Status</div>
                            <div className="heading-xxxs-normal">Completed</div>
                          </td>
                          <td>
                            <div className="place-holder">Evaluation Due Date</div>
                            <div className="heading-xxxs-normal">
                              {h.evaluationDueDate ? formatDate(h.evaluationDueDate) : "-"}
                            </div>
                          </td>
                        </>
                      ) : (
                        <td>
                          <div className="place-holder">Rotation Start Date</div>
                          <div className="heading-xxxs-normal">{formatDate(h.rotationStartDate)}</div>
                        </td>
                      )}
                      {isDepositTab && (
                        <td>
                          <div className="place-holder">Refunded</div>
                          <input
                            type="checkbox"
                            aria-label={`Mark R${h.rotationNumber} deposit refunded`}
                            checked={h.refunded}
                            disabled={refundPending}
                            onChange={(e) => onToggleRefund(h, e.target.checked)}
                          />
                        </td>
                      )}
                      <td className={isDepositTab ? "" : "last-td"}>
                        <div className="place-holder">Payment Status</div>
                        {h.status === "Paid" ? (
                          <span className="badge badge-ok">Paid</span>
                        ) : (
                          <button
                            className="btn btn-outline button-sm"
                            onClick={() => onPay(h)}
                            disabled={payPending}
                          >
                            {payPending ? "Paying…" : "Pay"}
                          </button>
                        )}
                      </td>
                      {isDepositTab && (
                        <td className="last-td">
                          <div className="place-holder">&nbsp;</div>
                          <button
                            className="btn btn-danger button-sm"
                            onClick={() => setDeleting(h)}
                            disabled={deletePending}
                          >
                            {deletePending ? "Deleting…" : "Delete"}
                          </button>
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )
        )}

        <Pagination page={page} pageSize={PAGE_SIZE} totalItems={totalItems} onChange={setPage} />
      </div>

      {deleting && (
        <Modal title="Delete honorarium" onClose={() => setDeleting(null)}>
          <div className="modal-body">
            Delete the deposit honorarium for <strong>R{deleting.rotationNumber}</strong> ({money(deleting.amount)},
            {" "}{deleting.preceptorName})? This removes the payout row. A paid honorarium can’t be deleted.
          </div>
          <div className="modal-foot">
            <button className="btn btn-ghost" onClick={() => setDeleting(null)} disabled={remove.isPending}>Cancel</button>
            <button className="btn btn-danger" onClick={confirmDelete} disabled={remove.isPending}>
              {remove.isPending ? "Deleting…" : "Delete"}
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}

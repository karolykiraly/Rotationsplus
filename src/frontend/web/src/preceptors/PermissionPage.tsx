import { useEffect, useState } from "react";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { usePermissionQueue } from "./usePermissionQueue";

const PAGE_SIZE = 10;

/** Per-row checkbox selection: which preceptors are checked to Activate vs Reject. Keyed by id so the
 *  selection survives paging until the admin clicks Save. */
type Selection = Record<string, "activate" | "reject" | undefined>;

/** The admin "Permissions" screen — the preceptor approval queue, matching production: a row per Pending
 *  preceptor (Name · Specialty · Scheduled · Phone · Email) with an **Activated** and a **Reject** checkbox,
 *  and a single **Save** that applies the whole batch (activate the checked, reject the others). Activated
 *  and Reject are mutually exclusive per row so a row can't be sent as both. */
export function PermissionPage() {
  const { user } = useMe();
  const [page, setPage] = useState(1);
  const { list, save } = usePermissionQueue(page, PAGE_SIZE);

  const [selection, setSelection] = useState<Selection>({});
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // Step back if the queue shrinks past the current page (a Save removes rows). Gated on fresh data;
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

  const toggle = (id: string, choice: "activate" | "reject") =>
    setSelection((prev) => ({ ...prev, [id]: prev[id] === choice ? undefined : choice }));

  const onSave = () => {
    const activateIds = Object.keys(selection).filter((id) => selection[id] === "activate");
    const rejectIds = Object.keys(selection).filter((id) => selection[id] === "reject");
    if (activateIds.length === 0 && rejectIds.length === 0) {
      setBanner({ type: "error", text: "Check Activated or Reject on at least one preceptor first." });
      return;
    }
    save.mutate(
      { activateIds, rejectIds },
      {
        onSuccess: (r) => {
          setSelection({});
          setBanner({ type: "ok", text: `Saved — activated ${r.activated}, rejected ${r.rejected}.` });
        },
        onError: (e) => setBanner({ type: "error", text: (e as Error).message })
      }
    );
  };

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="lead-page">
        <div className="list-head">
          <h2 className="heading-xxs">Permissions</h2>
          <span className="list-count">{totalItems} items</span>
        </div>

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
                      <div className="place-holder">Preceptor Name</div>
                      <div className="heading-xxxs">{p.fullName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Specialty</div>
                      <div className="heading-xxxs-normal">{p.primarySpecialtyName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Scheduled</div>
                      <div className={`heading-xxxs-normal ${p.callScheduled ? "ok-text" : "brand-text"}`}>
                        {p.callScheduled ? "Yes" : "No"}
                      </div>
                    </td>
                    <td>
                      <div className="place-holder">Phone Number</div>
                      <div className="heading-xxxs-normal">{p.mobilePhone || "—"}</div>
                    </td>
                    <td>
                      <div className="place-holder">Email</div>
                      <div className="heading-xxxs-normal">{p.email}</div>
                    </td>
                    <td className="text-center">
                      <div className="place-holder">Activated</div>
                      <input
                        type="checkbox"
                        aria-label={`Activate ${p.fullName}`}
                        checked={selection[p.id] === "activate"}
                        disabled={save.isPending}
                        onChange={() => toggle(p.id, "activate")}
                      />
                    </td>
                    <td className="last-td text-center">
                      <div className="place-holder">Reject</div>
                      <input
                        type="checkbox"
                        aria-label={`Reject ${p.fullName}`}
                        checked={selection[p.id] === "reject"}
                        disabled={save.isPending}
                        onChange={() => toggle(p.id, "reject")}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        <div className="list-foot">
          <Pagination page={page} pageSize={PAGE_SIZE} totalItems={totalItems} onChange={setPage} />
          <button className="btn btn-primary" onClick={onSave} disabled={save.isPending || rows.length === 0}>
            {save.isPending ? "Saving…" : "Save"}
          </button>
        </div>
      </div>
    </>
  );
}

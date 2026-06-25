import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { useDebouncedValue } from "../useDebouncedValue";
import { getRotation, type Rotation, type RotationInput, type RotationStatus } from "../api";
import searchIcon from "../assets/icons/search.png";
import { useRotations, useRotationPrograms, useRotationStudents } from "./useRotations";
import { RotationFormModal, type RotationFormInitial } from "./RotationFormModal";
import { ROTATION_STATUSES, rotationStatusLabel } from "./rotationStatuses";
import { programTypeLabel } from "../programs/programTypes";

/** When creating, every status is a valid initial value (the state machine governs only transitions). */
const ALL_STATUS_VALUES = ROTATION_STATUSES.map((s) => s.value);

const DEFAULTS: RotationFormInitial = {
  programId: "",
  studentId: "",
  startDate: "",
  endDate: "",
  status: "Pending"
};

/** "new" = create; a guid = edit that rotation; null = no modal. */
type EditId = string | "new" | null;

const PAGE_SIZE = 10;

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export function RotationsPage() {
  const { user } = useMe();
  const [statusFilter, setStatusFilter] = useState<RotationStatus | "">("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  // Debounce the search so we fire one server query per pause, not per keystroke.
  const debouncedSearch = useDebouncedValue(search.trim());

  const { list, create, update, remove, refund } = useRotations(statusFilter, debouncedSearch, page, PAGE_SIZE);
  const programs = useRotationPrograms();
  const students = useRotationStudents();

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Rotation | null>(null);
  const [refunding, setRefunding] = useState<Rotation | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // Back to the first page whenever the status filter or (debounced) search changes.
  useEffect(() => setPage(1), [statusFilter, debouncedSearch]);

  // Server returns one page + the full filtered count for the pager.
  const totalItems = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  // If the result set shrank past the current page (e.g. deleting the last row on the last page), step back.
  // Gate on fresh (non-placeholder) data so a stale keepPreviousData total can't drive a wrong jump.
  // (Declared before the admin guard below so the hook order is stable.)
  const settled = !list.isPlaceholderData;
  useEffect(() => {
    if (settled && page > totalPages) setPage(totalPages);
  }, [settled, page, totalPages]);

  // For edit, load the full detail (the list row lacks programId/oid).
  const detail = useQuery({
    queryKey: ["rotation", editId],
    queryFn: () => getRotation(editId as string),
    enabled: !!editId && editId !== "new"
  });

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage rotations.</div>;
  }

  const closeForm = () => setEditId(null);

  const submitForm = (input: RotationInput) => {
    setFormError(null);
    const onError = (e: unknown) => setFormError((e as Error).message);
    if (editId === "new") {
      const name = studentOpts.find((s) => s.id === input.studentId)?.fullName ?? "student";
      create.mutate(input, {
        onSuccess: () => { closeForm(); setBanner({ type: "ok", text: `Booked ${name}.` }); },
        onError
      });
    } else if (editId) {
      update.mutate(
        { id: editId, input },
        { onSuccess: () => { closeForm(); setBanner({ type: "ok", text: "Rotation updated." }); }, onError }
      );
    }
  };

  const confirmDelete = () => {
    if (!deleting) return;
    const name = deleting.studentName;
    remove.mutate(deleting.id, {
      onSuccess: () => { setDeleting(null); setBanner({ type: "ok", text: `Deleted ${name}'s rotation.` }); },
      onError: (e) => { setDeleting(null); setBanner({ type: "error", text: (e as Error).message }); }
    });
  };

  const confirmRefund = () => {
    if (!refunding) return;
    const name = refunding.studentName;
    refund.mutate(refunding.id, {
      onSuccess: (res) => {
        setRefunding(null);
        const n = res.paymentsRefunded;
        setBanner({ type: "ok", text: `Refunded ${name}'s deposit (${n} payment${n === 1 ? "" : "s"}).` });
      },
      onError: (e) => { setRefunding(null); setBanner({ type: "error", text: (e as Error).message }); }
    });
  };

  const mapDetail = (d: NonNullable<typeof detail.data>): RotationFormInitial => ({
    programId: d.programId,
    studentId: d.studentId ?? "",
    startDate: d.startDate,
    endDate: d.endDate,
    status: d.status
  });

  const rows = list.data?.items ?? [];
  const programOpts = programs.data ?? [];
  const studentOpts = students.data ?? [];

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="lead-page">
        <div className="program-toolbar">
          <button className="btn btn-primary spacer" onClick={() => { setFormError(null); setEditId("new"); }}>
            Add rotation
          </button>
          <label htmlFor="r-filter">Status</label>
          <select
            id="r-filter"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as RotationStatus | "")}
          >
            <option value="">All statuses</option>
            {ROTATION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
          <div className="search-form2">
            <img src={searchIcon} alt="" />
            <input
              placeholder="Search for Number/Preceptor/Student"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search for rotations"
            />
          </div>
        </div>

        {list.isLoading && <div className="state">Loading rotations…</div>}
        {list.isError && <div className="state">Couldn’t load rotations: {(list.error as Error).message}</div>}
        {/* keepPreviousData keeps the prior page visible while the next loads; show that it's updating
            (isFetching is true on a page/search change, isLoading only on the first load). */}
        {!list.isLoading && list.isFetching && <div className="state subtle" role="status">Updating…</div>}

        {!list.isLoading && !list.isError && (
          rows.length === 0 ? (
            <div className="state">There is no data available.</div>
          ) : (
            <table className="program-table" aria-busy={list.isFetching}>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.id} className="rot-row">
                    <td className="first-td">
                      <div className="place-holder">Rotation Number</div>
                      <div className="heading-xxxs">{r.rotationNumber ? `R${r.rotationNumber}` : "—"}</div>
                    </td>
                    <td>
                      <div className="place-holder">Student Name</div>
                      <div className="heading-xxxs-normal">{r.studentName}</div>
                      <div className="muted">{r.studentEmail}</div>
                    </td>
                    <td>
                      <div className="place-holder">Preceptor Name</div>
                      <div className="heading-xxxs-normal">{r.preceptorName ?? "—"}</div>
                    </td>
                    <td>
                      <div className="place-holder">Specialty</div>
                      <div className="heading-xxxs-normal">{r.specialtyName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Type</div>
                      <div className="heading-xxxs-normal">{programTypeLabel(r.programType)}</div>
                    </td>
                    <td>
                      <div className="place-holder">Start and End Date</div>
                      <div className="heading-xxxs-normal">{formatDate(r.startDate)} – {formatDate(r.endDate)}</div>
                    </td>
                    <td>
                      <div className="place-holder">Weeks</div>
                      <div className="heading-xxxs-normal">{r.weeks}</div>
                    </td>
                    <td>
                      <div className="place-holder">Status</div>
                      <div><span className="badge">{rotationStatusLabel(r.status)}</span></div>
                    </td>
                    <td className="last-td">
                      <div className="row-actions">
                        <button className="btn-link" onClick={() => { setFormError(null); setEditId(r.id); }}>Edit</button>
                        {(r.status === "Cancelled" || r.status === "Completed") && (
                          <button className="btn-link" onClick={() => setRefunding(r)}>Refund</button>
                        )}
                        <button className="btn-link danger" onClick={() => setDeleting(r)}>Delete</button>
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

      {editId === "new" && (
        <RotationFormModal
          title="Add rotation"
          initial={DEFAULTS}
          programs={programOpts}
          students={studentOpts}
          allowedStatuses={ALL_STATUS_VALUES}
          pending={create.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && detail.data && (
        <RotationFormModal
          title="Edit rotation"
          initial={mapDetail(detail.data)}
          programs={programOpts}
          students={studentOpts}
          // The current status (so it stays selectable) plus the server's allowed transitions, minus
          // Refunded — refunding is a money action done via the Refund button, not a plain status edit.
          allowedStatuses={[detail.data.status, ...detail.data.allowedNextStatuses.filter((s) => s !== "Refunded")]}
          pending={update.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && !detail.data && (
        <Modal title="Edit rotation" onClose={closeForm}>
          <div className="modal-body state">
            {detail.isError ? `Couldn’t load rotation: ${(detail.error as Error).message}` : "Loading…"}
          </div>
        </Modal>
      )}

      {refunding && (
        <Modal title="Refund rotation" onClose={() => setRefunding(null)}>
          <div className="modal-body">
            Refund {refunding.studentName}’s deposit for {refunding.specialtyName}? This returns the captured
            payment to the student and marks the rotation Refunded.
          </div>
          <div className="modal-foot">
            <button className="btn btn-ghost" onClick={() => setRefunding(null)} disabled={refund.isPending}>Cancel</button>
            <button className="btn btn-danger" onClick={confirmRefund} disabled={refund.isPending}>
              {refund.isPending ? "Refunding…" : "Refund"}
            </button>
          </div>
        </Modal>
      )}

      {deleting && (
        <Modal title="Delete rotation" onClose={() => setDeleting(null)}>
          <div className="modal-body">
            Delete {deleting.studentName}’s rotation in {deleting.specialtyName}? This can’t be undone.
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

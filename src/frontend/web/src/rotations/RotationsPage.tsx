import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { useDebouncedValue } from "../useDebouncedValue";
import { getRotation, type RotationInput } from "../api";
import searchIcon from "../assets/icons/search.png";
import { useRotationsList, useRotationMutations, useRotationPrograms, useRotationStudents } from "./useRotations";
import { RotationFormModal, type RotationFormInitial } from "./RotationFormModal";
import { SelectedRotationPanel } from "./SelectedRotationPanel";
import { ROTATION_STATUSES, rotationStatusLabel, rotationStatusClass } from "./rotationStatuses";

const DEFAULTS: RotationFormInitial = {
  programId: "",
  studentId: "",
  startDate: "",
  endDate: "",
  status: "Pending"
};

const ALL_STATUS_VALUES = ROTATION_STATUSES.map((s) => s.value);
const PAGE_SIZE = 10;

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

const money = (amount: number) =>
  `$${amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })}`;

/** One rotation table section (Current or Historical) — own search + pager, the production columns, and a
 *  View button that opens the shared Selected Rotation panel. */
function RotationsSection({
  title,
  scope,
  selectedId,
  onView,
  headerExtra
}: {
  title: string;
  scope: "current" | "historical";
  selectedId: string | null;
  onView: (id: string) => void;
  headerExtra?: React.ReactNode;
}) {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search.trim());
  const list = useRotationsList(scope, debouncedSearch, page, PAGE_SIZE);

  useEffect(() => setPage(1), [debouncedSearch]);

  const totalItems = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const settled = !list.isPlaceholderData;
  useEffect(() => {
    if (settled && page > totalPages) setPage(totalPages);
  }, [settled, page, totalPages]);

  const rows = list.data?.items ?? [];

  return (
    <div className="lead-page">
      <div className="list-head">
        <h2 className="heading-xxs">{title}</h2>
        <span className="list-count">{totalItems} items</span>
        <div className="list-head-actions">
          {headerExtra}
          <div className="search-form2">
            <img src={searchIcon} alt="" />
            <input
              placeholder="Search for Number/Preceptor/Student"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label={`Search ${title.toLowerCase()}`}
            />
          </div>
        </div>
      </div>

      {list.isLoading && <div className="state">Loading rotations…</div>}
      {list.isError && <div className="state">Couldn’t load rotations: {(list.error as Error).message}</div>}
      {!list.isLoading && list.isFetching && <div className="state subtle" role="status">Updating…</div>}

      {!list.isLoading && !list.isError && (
        rows.length === 0 ? (
          <div className="state">There is no data available.</div>
        ) : (
          <table className="program-table" aria-busy={list.isFetching}>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className={`rot-row${r.id === selectedId ? " selected" : ""}`}>
                  <td className="first-td">
                    <div className="place-holder">Rotation Number</div>
                    <div className="heading-xxxs">{r.rotationNumber ? `R${r.rotationNumber}` : "—"}</div>
                  </td>
                  <td>
                    <div className="place-holder">Preceptor Name</div>
                    <div className="heading-xxxs-normal">{r.preceptorName ?? "—"}</div>
                  </td>
                  <td>
                    <div className="place-holder">Student Name</div>
                    <div className="heading-xxxs-normal">{r.studentName}</div>
                  </td>
                  <td>
                    <div className="place-holder">Start and End Date</div>
                    <div className="heading-xxxs-normal">{formatDate(r.startDate)} – {formatDate(r.endDate)}</div>
                  </td>
                  <td>
                    <div className="place-holder">Retail Amount</div>
                    <div className="heading-xxxs-normal">{money(r.retailAmount)}</div>
                  </td>
                  <td className="text-center">
                    <div className="place-holder">Needs Visa</div>
                    <input
                      type="checkbox"
                      readOnly
                      checked={r.needsVisa}
                      aria-label={`R${r.rotationNumber} needs visa`}
                    />
                  </td>
                  <td>
                    <div className="place-holder">Status</div>
                    <div className={`heading-xxxs-normal ${rotationStatusClass(r.status)}`}>
                      {rotationStatusLabel(r.status)}
                    </div>
                  </td>
                  <td className="last-td">
                    <button className="btn btn-outline button-sm" onClick={() => onView(r.id)}>View</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )
      )}

      <Pagination page={page} pageSize={PAGE_SIZE} totalItems={totalItems} onChange={setPage} />
    </div>
  );
}

/** The admin Rotations screen — two stacked sections (Current / Historical), each server-paginated with
 *  its own search, plus the Selected Rotation detail panel that opens on View (Replace program / Change
 *  dates / status, saved via the shared update endpoint). Matches the production layout. */
export function RotationsPage() {
  const { user } = useMe();
  const { create, update } = useRotationMutations();
  const programs = useRotationPrograms();
  const students = useRotationStudents();

  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [panelError, setPanelError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  const detail = useQuery({
    queryKey: ["rotation", selectedId],
    queryFn: () => getRotation(selectedId as string),
    enabled: !!selectedId
  });

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage rotations.</div>;
  }

  const programOpts = programs.data ?? [];
  const studentOpts = students.data ?? [];

  const onView = (id: string) => {
    setPanelError(null);
    setSelectedId(id);
  };
  const closePanel = () => { setSelectedId(null); setPanelError(null); };

  const submitAdd = (input: RotationInput) => {
    setFormError(null);
    const name = studentOpts.find((s) => s.id === input.studentId)?.fullName ?? "student";
    create.mutate(input, {
      onSuccess: () => { setAdding(false); setBanner({ type: "ok", text: `Booked ${name}.` }); },
      onError: (e) => setFormError((e as Error).message)
    });
  };

  const savePanel = (input: RotationInput) => {
    if (!selectedId) return;
    setPanelError(null);
    update.mutate({ id: selectedId, input }, {
      onSuccess: () => { closePanel(); setBanner({ type: "ok", text: "Rotation updated." }); },
      onError: (e) => setPanelError((e as Error).message)
    });
  };

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <RotationsSection
        title="Current Rotations"
        scope="current"
        selectedId={selectedId}
        onView={onView}
        headerExtra={
          <button className="btn btn-primary spacer" onClick={() => { setFormError(null); setAdding(true); }}>
            Add New Rotation
          </button>
        }
      />

      {selectedId && detail.data && (
        <SelectedRotationPanel
          detail={detail.data}
          programs={programOpts}
          pending={update.isPending}
          serverError={panelError}
          onSave={savePanel}
          onClose={closePanel}
        />
      )}
      {selectedId && !detail.data && (
        <div className="lead-page state">
          {detail.isError ? `Couldn’t load rotation: ${(detail.error as Error).message}` : "Loading rotation…"}
        </div>
      )}

      <RotationsSection
        title="Historical Rotations"
        scope="historical"
        selectedId={selectedId}
        onView={onView}
      />

      {adding && (
        <RotationFormModal
          title="Add rotation"
          initial={DEFAULTS}
          programs={programOpts}
          students={studentOpts}
          allowedStatuses={ALL_STATUS_VALUES}
          pending={create.isPending}
          serverError={formError}
          onSubmit={submitAdd}
          onClose={() => setAdding(false)}
        />
      )}
    </>
  );
}

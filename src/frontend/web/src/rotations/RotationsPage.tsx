import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { useDebouncedValue } from "../useDebouncedValue";
import { getRotation, type RotationFilter, type RotationInput } from "../api";
import searchIcon from "../assets/icons/search.png";
import filterIcon from "../assets/images/filter.svg";
import { useRotationsList, useRotationMutations, useRotationPrograms, useRotationStudents } from "./useRotations";
import { RotationFormModal, type RotationFormInitial } from "./RotationFormModal";
import { SelectedRotationPanel } from "./SelectedRotationPanel";
import { FilterRotationModal } from "./FilterRotationModal";
import { ROTATION_STATUSES, rotationStatusLabel, rotationStatusClass } from "./rotationStatuses";

/** Number of active (set) keys in a rotation filter — drives the filter-count badge. */
function rotationFilterCount(f: RotationFilter): number {
  return Object.values(f).filter((v) => v !== undefined && v !== "" && v !== false).length;
}

const DEFAULTS: RotationFormInitial = {
  programId: "",
  studentId: "",
  startDate: "",
  endDate: "",
  status: "Pending"
};

const ALL_STATUS_VALUES = ROTATION_STATUSES.map((s) => s.value);
const PAGE_SIZE = 10;

/** Format a YYYY-MM-DD wire date as MM/DD/YY (or MM/DD/YYYY) — the legacy admin rotations format. The
 *  wire parts are already zero-padded, so slice them directly (no Date() → no timezone shift). Legacy
 *  shows a 2-digit year in the Current section and a 4-digit year in the Historical section. */
function formatDate(iso: string, fourDigitYear = false): string {
  const [y, m, d] = iso.split("-");
  if (!y || !m || !d) return "-";
  return `${m}/${d}/${fourDigitYear ? y : y.slice(-2)}`;
}

/** One rotation table section (Current or Historical) — own search + pager, the production columns, and a
 *  View button that opens the shared Selected Rotation panel. */
function RotationsSection({
  title,
  scope,
  selectedId,
  onView,
  filter,
  headerExtra
}: {
  title: string;
  scope: "current" | "historical";
  selectedId: string | null;
  onView: (id: string) => void;
  filter: RotationFilter;
  headerExtra?: React.ReactNode;
}) {
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search.trim());
  const list = useRotationsList(scope, debouncedSearch, page, PAGE_SIZE, filter);

  // Back to page 1 whenever the search or the shared filter changes.
  useEffect(() => setPage(1), [debouncedSearch, filter]);

  const totalItems = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const settled = !list.isPlaceholderData;
  useEffect(() => {
    if (settled && page > totalPages) setPage(totalPages);
  }, [settled, page, totalPages]);

  const rows = list.data?.items ?? [];
  // Legacy shows a 2-digit year in the Current section, a 4-digit year in the Historical section.
  const fourDigitYear = scope === "historical";

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
                    {/* Production renders the name as a bold blue link to the member's profile page; the
                        rewrite styles it to match but defers the navigation until the member-profile
                        (Contacts) pages exist — see RotationsPage note. */}
                    <div className="heading-xxxs rot-name">{r.preceptorName ?? "—"}</div>
                  </td>
                  <td>
                    <div className="place-holder">Student Name</div>
                    <div className="heading-xxxs rot-name">{r.studentName}</div>
                  </td>
                  <td>
                    <div className="place-holder">Start and End Date</div>
                    <div className="body-md">{formatDate(r.startDate, fourDigitYear)} to {formatDate(r.endDate, fourDigitYear)}</div>
                  </td>
                  <td>
                    <div className="place-holder">Retail Amount</div>
                    <div className="body-md">{`$${r.retailAmount}`}</div>
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
                    <div className={`body-md ${rotationStatusClass(r.status)}`}>
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
 *  dates / status, saved via the shared update endpoint). Matches the production layout.
 *
 *  Deferred-parity note: production renders the Preceptor and Student names as links to the member's
 *  profile page (`/admin/preceptors/:id`, `/admin/students/:id`). Those profile routes don't exist in
 *  the rewrite yet (they're part of the upcoming Contacts work), so the names are styled to match
 *  (bold blue) but are not yet clickable — wire them to the profile pages when those land. */
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
  // One filter shared by BOTH sections (legacy applies the FilterRotation modal to current + historical).
  const [filter, setFilter] = useState<RotationFilter>({});
  const [showFilter, setShowFilter] = useState(false);
  const filterCount = rotationFilterCount(filter);

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
        filter={filter}
        headerExtra={
          <>
            <button className="btn btn-primary spacer" onClick={() => { setFormError(null); setAdding(true); }}>
              Add New Rotation
            </button>
            <button className="filter-btn" type="button" aria-label="Filter rotations" title="Filter" onClick={() => setShowFilter(true)}>
              <img src={filterIcon} alt="" />
              {filterCount > 0 && <span className="filter-count">{filterCount}</span>}
            </button>
          </>
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
        filter={filter}
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

      {showFilter && (
        <FilterRotationModal
          initial={filter}
          onApply={(f) => { setFilter(f); setShowFilter(false); }}
          onClear={() => { setFilter({}); setShowFilter(false); }}
          onClose={() => setShowFilter(false)}
        />
      )}
    </>
  );
}

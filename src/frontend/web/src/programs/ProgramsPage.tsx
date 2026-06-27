import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { Tabs } from "../components/Tabs";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { useDebouncedValue } from "../useDebouncedValue";
import { getProgram, type Program, type ProgramFilter, type ProgramInput, type ProgramType } from "../api";
import { usePrograms, useProgramFormOptions } from "./usePrograms";
import { ProgramFormModal, type ProgramFormInitial } from "./ProgramFormModal";
import { FilterProgramModal } from "./FilterProgramModal";
import { ProgramDocumentsModal } from "./ProgramDocumentsModal";
import { programCode, programDisplayName, programTypeLabel } from "./programTypes";
import noImage from "../assets/images/no_image.webp";
import filterIcon from "../assets/images/filter.svg";
import searchIcon from "../assets/icons/search.png";

const DEFAULTS: ProgramFormInitial = {
  specialtyId: "",
  programType: "InPerson",
  maxStudentsPerRotation: 1,
  minWeeksPerRotation: 4,
  retailAmountPerWeek: 0,
  weeklyHonorarium: 0,
  preceptorId: "",
  description: ""
};

/** Program-type tabs, mirroring the live admin app. Each tab maps to one or more ProgramType values
 *  (Consultation also covers the ConsultationSub variant, as in legacy). */
const TAB_LABELS = ["InPerson", "InPersonResearch", "Consultation", "TeleRotation", "TeleResearch"];
const TAB_TYPES: ProgramType[][] = [
  ["InPerson"],
  ["InPersonResearch"],
  ["Consultation", "ConsultationSub"],
  ["TeleRotation"],
  ["TeleResearch"]
];

const PAGE_SIZE = 10;

/** "new" = create; a guid = edit that program; null = no modal. */
type EditId = string | "new" | null;

export function ProgramsPage() {
  const { user } = useMe();
  const [tab, setTab] = useState(0);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebouncedValue(search.trim());
  const [filter, setFilter] = useState<ProgramFilter>({});
  const [showFilter, setShowFilter] = useState(false);
  const filterCount = Object.values(filter).filter(
    (v) => v !== undefined && v !== "" && !(Array.isArray(v) && v.length === 0)
  ).length;

  const { list, create, update, remove } = usePrograms(TAB_TYPES[tab], debouncedSearch, page, PAGE_SIZE, filter);
  const { specialties, preceptors } = useProgramFormOptions();

  const [editId, setEditId] = useState<EditId>(null);
  const [docsProgram, setDocsProgram] = useState<{ id: string; label: string } | null>(null);
  const [deleting, setDeleting] = useState<Program | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // Reset to the first page whenever the tab, (debounced) search, or filter changes (legacy paging behaviour).
  useEffect(() => setPage(1), [tab, debouncedSearch, filter]);

  // Server returns one page + the full filtered count for the pager. If the set shrinks past the current
  // page, step back. Gate on fresh (non-placeholder) data; declared before the admin guard so hooks are stable.
  const totalItems = list.data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  const settled = !list.isPlaceholderData;
  useEffect(() => {
    if (settled && page > totalPages) setPage(totalPages);
  }, [settled, page, totalPages]);

  // For edit, load the full detail (the list row lacks honorarium/description/ids).
  const detail = useQuery({
    queryKey: ["program", editId],
    queryFn: () => getProgram(editId as string),
    enabled: !!editId && editId !== "new"
  });

  if (user && !user.isAdmin) {
    return <div className="lead-page state">You need the Admin role to manage programs.</div>;
  }

  const closeForm = () => setEditId(null);

  const submitForm = (input: ProgramInput) => {
    setFormError(null);
    const onError = (e: unknown) => setFormError((e as Error).message);
    if (editId === "new") {
      create.mutate(input, {
        onSuccess: () => { closeForm(); setBanner({ type: "ok", text: "Program created." }); },
        onError
      });
    } else if (editId) {
      update.mutate(
        { id: editId, input },
        { onSuccess: () => { closeForm(); setBanner({ type: "ok", text: "Program updated." }); }, onError }
      );
    }
  };

  const confirmDelete = () => {
    if (!deleting) return;
    const label = `${deleting.specialtyName} · ${programTypeLabel(deleting.programType)}`;
    remove.mutate(deleting.id, {
      onSuccess: () => { setDeleting(null); setBanner({ type: "ok", text: `Deleted ${label}.` }); },
      onError: (e) => { setDeleting(null); setBanner({ type: "error", text: (e as Error).message }); }
    });
  };

  const mapDetail = (d: NonNullable<typeof detail.data>): ProgramFormInitial => ({
    specialtyId: d.specialtyId,
    programType: d.programType,
    maxStudentsPerRotation: d.maxStudentsPerRotation,
    minWeeksPerRotation: d.minWeeksPerRotation,
    retailAmountPerWeek: d.retailAmountPerWeek,
    weeklyHonorarium: d.weeklyHonorarium ?? 0,
    preceptorId: d.preceptorId ?? "",
    description: d.description ?? ""
  });

  const opts = { specialties: specialties.data ?? [], preceptors: preceptors.data ?? [] };
  const rows = list.data?.items ?? [];

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="lead-page">
        <div className="program-toolbar">
          <button className="btn btn-primary spacer" onClick={() => { setFormError(null); setEditId("new"); }}>
            Add program
          </button>
          <button className="filter-btn" type="button" title="Filter" aria-label="Filter programs" onClick={() => setShowFilter(true)}>
            <img src={filterIcon} alt="" />
            {filterCount > 0 && <span className="filter-count">{filterCount}</span>}
          </button>
          <div className="search-form2">
            <img src={searchIcon} alt="" />
            <input
              placeholder="Search for Programs"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search for programs"
            />
          </div>
        </div>

        <Tabs labels={TAB_LABELS} active={tab} onChange={setTab} />

        {list.isLoading && <div className="state">Loading programs…</div>}
        {list.isError && <div className="state">Couldn’t load programs: {(list.error as Error).message}</div>}
        {!list.isLoading && list.isFetching && <div className="state subtle" role="status">Updating…</div>}

        {!list.isLoading && !list.isError && (
          rows.length === 0 ? (
            <div className="state">There is no data available.</div>
          ) : (
            <table className="program-table" aria-busy={list.isFetching}>
              <tbody>
                {rows.map((p) => {
                  const openEdit = () => { setFormError(null); setEditId(p.id); };
                  return (
                  <tr
                    key={p.id}
                    className="program-row"
                    role="button"
                    tabIndex={0}
                    aria-label={`Edit ${p.specialtyName} program`}
                    onClick={openEdit}
                    onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); openEdit(); } }}
                  >
                    <td className="first-td">
                      <img className="hospital-img" src={noImage} alt="" />
                    </td>
                    <td className="hospital-name">
                      <div className="heading-xxxs">{programDisplayName(p.specialtyName)}</div>
                    </td>
                    <td>
                      <div className="place-holder">Program ID</div>
                      <div className="heading-xxxs-normal">{programCode(p.programType, p.programNumber)}</div>
                    </td>
                    <td>
                      <div className="place-holder">Location</div>
                      <div className="heading-xxxs-normal">{[p.city, p.state].filter(Boolean).join(", ") || "—"}</div>
                    </td>
                    <td>
                      <div className="place-holder">Specialty</div>
                      <div className="heading-xxxs-normal">{p.specialtyName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Instant Approval</div>
                      <div className="heading-xxxs-normal">{p.isOpen ? "Yes" : "No"}</div>
                    </td>
                    <td className="last-td">
                      <div className="place-holder">Retail Amount</div>
                      {/* The legacy admin Programs list shows the weekly honorarium under its "Retail Amount"
                          column (owner-confirmed 2026-06-26 to match production); staff-only, "—" when unset. */}
                      <div className="heading-xxxs-normal">
                        {p.weeklyHonorarium ? `$${p.weeklyHonorarium.toLocaleString()}` : "—"}
                      </div>
                    </td>
                  </tr>
                  );
                })}
              </tbody>
            </table>
          )
        )}

        <Pagination page={page} pageSize={PAGE_SIZE} totalItems={totalItems} onChange={setPage} />
      </div>

      {editId === "new" && (
        <ProgramFormModal
          title="Add program"
          initial={DEFAULTS}
          specialties={opts.specialties}
          preceptors={opts.preceptors}
          pending={create.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && detail.data && (
        <ProgramFormModal
          title="Edit program"
          initial={mapDetail(detail.data)}
          specialties={opts.specialties}
          preceptors={opts.preceptors}
          pending={update.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
          onDelete={() => {
            const prog = rows.find((p) => p.id === editId);
            if (prog) { closeForm(); setDeleting(prog); }
          }}
          onConfigureDocuments={() => {
            const d = detail.data!;
            setDocsProgram({ id: d.id, label: `${d.specialtyName} · ${programCode(d.programType, d.programNumber)}` });
          }}
        />
      )}

      {docsProgram && (
        <ProgramDocumentsModal
          programId={docsProgram.id}
          programLabel={docsProgram.label}
          onClose={() => setDocsProgram(null)}
        />
      )}

      {editId && editId !== "new" && !detail.data && (
        <Modal title="Edit program" onClose={closeForm}>
          <div className="modal-body state">
            {detail.isError ? `Couldn’t load program: ${(detail.error as Error).message}` : "Loading…"}
          </div>
        </Modal>
      )}

      {deleting && (
        <Modal title="Delete program" onClose={() => setDeleting(null)}>
          <div className="modal-body">
            Delete the {programTypeLabel(deleting.programType)} program in {deleting.specialtyName}? This can’t be undone.
          </div>
          <div className="modal-foot">
            <button className="btn btn-ghost" onClick={() => setDeleting(null)} disabled={remove.isPending}>Cancel</button>
            <button className="btn btn-danger" onClick={confirmDelete} disabled={remove.isPending}>
              {remove.isPending ? "Deleting…" : "Delete"}
            </button>
          </div>
        </Modal>
      )}

      {showFilter && (
        <FilterProgramModal
          initial={filter}
          specialties={specialties.data ?? []}
          onApply={(f) => { setFilter(f); setShowFilter(false); }}
          onClear={() => { setFilter({}); setShowFilter(false); }}
          onClose={() => setShowFilter(false)}
        />
      )}
    </>
  );
}

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { useMe } from "../useMe";
import { getProgram, type Program, type ProgramInput } from "../api";
import { usePrograms, useProgramFormOptions } from "./usePrograms";
import { ProgramFormModal, type ProgramFormInitial } from "./ProgramFormModal";
import { programTypeLabel } from "./programTypes";

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

/** "new" = create; a guid = edit that program; null = no modal. */
type EditId = string | "new" | null;

export function ProgramsPage() {
  const { user } = useMe();
  const { list, create, update, remove } = usePrograms();
  const { specialties, preceptors } = useProgramFormOptions();

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Program | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // For edit, load the full detail (the list row lacks honorarium/description/ids).
  const detail = useQuery({
    queryKey: ["program", editId],
    queryFn: () => getProgram(editId as string),
    enabled: !!editId && editId !== "new"
  });

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage programs.</div>;
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

  const programs = list.data ?? [];
  const opts = { specialties: specialties.data ?? [], preceptors: preceptors.data ?? [] };

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Programs</h2>
          <p>Clinical-rotation offerings in the marketplace catalog.</p>
        </div>
        <button className="btn btn-primary" onClick={() => { setFormError(null); setEditId("new"); }}>
          Add program
        </button>
      </div>

      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="card">
        {list.isLoading && <div className="state">Loading programs…</div>}
        {list.isError && <div className="state">Couldn’t load programs: {(list.error as Error).message}</div>}
        {!list.isLoading && !list.isError && programs.length === 0 && (
          <div className="state">No programs yet. Add the first one.</div>
        )}

        {programs.length > 0 && (
          <table className="data">
            <thead>
              <tr>
                <th>Specialty</th>
                <th>Type</th>
                <th>Capacity</th>
                <th>Retail</th>
                <th>Preceptor</th>
                <th style={{ width: 150, textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {programs.map((p) => (
                <tr key={p.id}>
                  <td>{p.specialtyName}</td>
                  <td>{programTypeLabel(p.programType)}</td>
                  <td>{p.maxStudentsPerRotation} · {p.minWeeksPerRotation}+ wks</td>
                  <td>${p.retailAmountPerWeek.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}/wk</td>
                  <td>{p.preceptorName ?? "—"}</td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-link" onClick={() => { setFormError(null); setEditId(p.id); }}>Edit</button>
                      <button className="btn-link danger" onClick={() => setDeleting(p)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
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
    </>
  );
}

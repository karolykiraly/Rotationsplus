import { useState } from "react";
import { Modal } from "../components/Modal";
import { useMe } from "../useMe";
import { type Specialty } from "../api";
import { useSpecialties } from "./useSpecialties";
import { SpecialtyFormModal } from "./SpecialtyFormModal";

type Editing = { kind: "create" } | { kind: "edit"; specialty: Specialty } | null;

/** Admin management of marketplace specialties: list + create/edit/delete against the AdminOnly
 *  API. Non-admins see a forbidden notice (the API enforces it too). */
export function SpecialtiesPage() {
  const { user } = useMe();
  const { list, create, update, remove } = useSpecialties();

  const [editing, setEditing] = useState<Editing>(null);
  const [deleting, setDeleting] = useState<Specialty | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage specialties.</div>;
  }

  const openCreate = () => {
    setFormError(null);
    setEditing({ kind: "create" });
  };
  const openEdit = (specialty: Specialty) => {
    setFormError(null);
    setEditing({ kind: "edit", specialty });
  };
  const closeForm = () => setEditing(null);

  const submitForm = (name: string) => {
    setFormError(null);
    const onError = (e: unknown) => setFormError((e as Error).message);
    if (editing?.kind === "create") {
      create.mutate(name, {
        onSuccess: () => { closeForm(); setBanner({ type: "ok", text: `Added “${name}”.` }); },
        onError
      });
    } else if (editing?.kind === "edit") {
      update.mutate(
        { id: editing.specialty.id, name },
        {
          onSuccess: () => { closeForm(); setBanner({ type: "ok", text: `Renamed to “${name}”.` }); },
          onError
        }
      );
    }
  };

  const confirmDelete = () => {
    if (!deleting) return;
    const name = deleting.name;
    remove.mutate(deleting.id, {
      onSuccess: () => { setDeleting(null); setBanner({ type: "ok", text: `Deleted “${name}”.` }); },
      onError: (e) => { setDeleting(null); setBanner({ type: "error", text: (e as Error).message }); }
    });
  };

  const specialties = list.data ?? [];

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Specialties</h2>
          <p>Clinical specialties available across the marketplace.</p>
        </div>
        <button className="btn btn-primary" onClick={openCreate}>Add specialty</button>
      </div>

      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="card">
        {list.isLoading && <div className="state">Loading specialties…</div>}
        {list.isError && <div className="state">Couldn’t load specialties: {(list.error as Error).message}</div>}
        {!list.isLoading && !list.isError && specialties.length === 0 && (
          <div className="state">No specialties yet. Add the first one.</div>
        )}

        {specialties.length > 0 && (
          <table className="data">
            <thead>
              <tr>
                <th>Name</th>
                <th style={{ width: 160, textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {specialties.map((s) => (
                <tr key={s.id}>
                  <td>{s.name}</td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-link" onClick={() => openEdit(s)}>Edit</button>
                      <button className="btn-link danger" onClick={() => setDeleting(s)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editing && (
        <SpecialtyFormModal
          title={editing.kind === "create" ? "Add specialty" : "Edit specialty"}
          initialName={editing.kind === "edit" ? editing.specialty.name : ""}
          pending={create.isPending || update.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {deleting && (
        <Modal title="Delete specialty" onClose={() => setDeleting(null)}>
          <div className="modal-body">
            Delete “{deleting.name}”? Programs that reference it are unaffected, but it will no longer
            be selectable.
          </div>
          <div className="modal-foot">
            <button className="btn btn-ghost" onClick={() => setDeleting(null)} disabled={remove.isPending}>
              Cancel
            </button>
            <button className="btn btn-danger" onClick={confirmDelete} disabled={remove.isPending}>
              {remove.isPending ? "Deleting…" : "Delete"}
            </button>
          </div>
        </Modal>
      )}
    </>
  );
}

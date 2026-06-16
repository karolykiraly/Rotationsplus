import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { useMe } from "../useMe";
import { getPreceptor, type Preceptor, type PreceptorInput } from "../api";
import { usePreceptors, usePreceptorSpecialties } from "./usePreceptors";
import { PreceptorFormModal, type PreceptorFormInitial } from "./PreceptorFormModal";
import { preceptorStatusLabel } from "./preceptorStatuses";

const DEFAULTS: PreceptorFormInitial = {
  firstName: "",
  lastName: "",
  email: "",
  primarySpecialtyId: "",
  status: "Registered",
  medicalLicenseNumber: "",
  licenseState: "",
  city: "",
  state: "",
  bio: ""
};

type EditId = string | "new" | null;

export function PreceptorsPage() {
  const { user } = useMe();
  const { list, create, update, remove } = usePreceptors();
  const specialties = usePreceptorSpecialties();

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Preceptor | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  const detail = useQuery({
    queryKey: ["preceptor", editId],
    queryFn: () => getPreceptor(editId as string),
    enabled: !!editId && editId !== "new"
  });

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage preceptors.</div>;
  }

  const closeForm = () => setEditId(null);

  const submitForm = (input: PreceptorInput) => {
    setFormError(null);
    const onError = (e: unknown) => setFormError((e as Error).message);
    if (editId === "new") {
      create.mutate(input, {
        onSuccess: () => { closeForm(); setBanner({ type: "ok", text: `Added ${input.firstName} ${input.lastName}.` }); },
        onError
      });
    } else if (editId) {
      update.mutate(
        { id: editId, input },
        { onSuccess: () => { closeForm(); setBanner({ type: "ok", text: "Preceptor updated." }); }, onError }
      );
    }
  };

  const confirmDelete = () => {
    if (!deleting) return;
    const name = deleting.fullName;
    remove.mutate(deleting.id, {
      onSuccess: () => { setDeleting(null); setBanner({ type: "ok", text: `Deleted ${name}.` }); },
      onError: (e) => { setDeleting(null); setBanner({ type: "error", text: (e as Error).message }); }
    });
  };

  const mapDetail = (d: NonNullable<typeof detail.data>): PreceptorFormInitial => ({
    firstName: d.firstName,
    lastName: d.lastName,
    email: d.email,
    primarySpecialtyId: d.primarySpecialtyId,
    status: d.status,
    medicalLicenseNumber: d.medicalLicenseNumber ?? "",
    licenseState: d.licenseState ?? "",
    city: d.city ?? "",
    state: d.state ?? "",
    bio: d.bio ?? ""
  });

  const preceptors = list.data ?? [];
  const opts = specialties.data ?? [];

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Preceptors</h2>
          <p>The directory of supervising preceptors.</p>
        </div>
        <button className="btn btn-primary" onClick={() => { setFormError(null); setEditId("new"); }}>
          Add preceptor
        </button>
      </div>

      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="card">
        {list.isLoading && <div className="state">Loading preceptors…</div>}
        {list.isError && <div className="state">Couldn’t load preceptors: {(list.error as Error).message}</div>}
        {!list.isLoading && !list.isError && preceptors.length === 0 && (
          <div className="state">No preceptors yet. Add the first one.</div>
        )}

        {preceptors.length > 0 && (
          <table className="data">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Specialty</th>
                <th>Location</th>
                <th>Status</th>
                <th style={{ width: 150, textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {preceptors.map((p) => (
                <tr key={p.id}>
                  <td>{p.fullName}</td>
                  <td>{p.email}</td>
                  <td>{p.primarySpecialtyName}</td>
                  <td>{[p.city, p.state].filter(Boolean).join(", ") || "—"}</td>
                  <td><span className="badge">{preceptorStatusLabel(p.status)}</span></td>
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
        <PreceptorFormModal
          title="Add preceptor"
          initial={DEFAULTS}
          specialties={opts}
          pending={create.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && detail.data && (
        <PreceptorFormModal
          title="Edit preceptor"
          initial={mapDetail(detail.data)}
          specialties={opts}
          pending={update.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && !detail.data && (
        <Modal title="Edit preceptor" onClose={closeForm}>
          <div className="modal-body state">
            {detail.isError ? `Couldn’t load preceptor: ${(detail.error as Error).message}` : "Loading…"}
          </div>
        </Modal>
      )}

      {deleting && (
        <Modal title="Delete preceptor" onClose={() => setDeleting(null)}>
          <div className="modal-body">
            Delete {deleting.fullName}? Programs they offer become unassigned. This can’t be undone.
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

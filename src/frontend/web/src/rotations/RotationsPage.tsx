import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { useMe } from "../useMe";
import { getRotation, type Rotation, type RotationInput, type RotationStatus } from "../api";
import { useRotations, useRotationPrograms, useRotationStudents } from "./useRotations";
import { RotationFormModal, type RotationFormInitial } from "./RotationFormModal";
import { ROTATION_STATUSES, rotationStatusLabel } from "./rotationStatuses";
import { programTypeLabel } from "../programs/programTypes";

const DEFAULTS: RotationFormInitial = {
  programId: "",
  studentId: "",
  startDate: "",
  endDate: "",
  status: "Pending"
};

/** "new" = create; a guid = edit that rotation; null = no modal. */
type EditId = string | "new" | null;

/** Format a YYYY-MM-DD wire date for display without a timezone shift (parse the parts directly). */
function formatDate(iso: string): string {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return iso;
  return new Date(y, m - 1, d).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
}

export function RotationsPage() {
  const { user } = useMe();
  const [statusFilter, setStatusFilter] = useState<RotationStatus | "">("");
  const { list, create, update, remove } = useRotations(statusFilter);
  const programs = useRotationPrograms();
  const students = useRotationStudents();

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Rotation | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

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

  const mapDetail = (d: NonNullable<typeof detail.data>): RotationFormInitial => ({
    programId: d.programId,
    studentId: d.studentId ?? "",
    startDate: d.startDate,
    endDate: d.endDate,
    status: d.status
  });

  const rotations = list.data ?? [];
  const programOpts = programs.data ?? [];
  const studentOpts = students.data ?? [];

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Rotations</h2>
          <p>Student bookings into programs, with their lifecycle status.</p>
        </div>
        <button className="btn btn-primary" onClick={() => { setFormError(null); setEditId("new"); }}>
          Add rotation
        </button>
      </div>

      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="toolbar">
        <label htmlFor="r-filter">Status</label>
        <select
          id="r-filter"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as RotationStatus | "")}
        >
          <option value="">All statuses</option>
          {ROTATION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
      </div>

      <div className="card">
        {list.isLoading && <div className="state">Loading rotations…</div>}
        {list.isError && <div className="state">Couldn’t load rotations: {(list.error as Error).message}</div>}
        {!list.isLoading && !list.isError && rotations.length === 0 && (
          <div className="state">No rotations{statusFilter ? " with this status" : " yet"}.</div>
        )}

        {rotations.length > 0 && (
          <table className="data">
            <thead>
              <tr>
                <th>Student</th>
                <th>Specialty</th>
                <th>Type</th>
                <th>Preceptor</th>
                <th>Dates</th>
                <th>Weeks</th>
                <th>Status</th>
                <th style={{ width: 150, textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rotations.map((r) => (
                <tr key={r.id}>
                  <td>
                    <div>{r.studentName}</div>
                    <div className="muted">{r.studentEmail}</div>
                  </td>
                  <td>{r.specialtyName}</td>
                  <td>{programTypeLabel(r.programType)}</td>
                  <td>{r.preceptorName ?? "—"}</td>
                  <td>{formatDate(r.startDate)} – {formatDate(r.endDate)}</td>
                  <td>{r.weeks}</td>
                  <td><span className="badge">{rotationStatusLabel(r.status)}</span></td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-link" onClick={() => { setFormError(null); setEditId(r.id); }}>Edit</button>
                      <button className="btn-link danger" onClick={() => setDeleting(r)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editId === "new" && (
        <RotationFormModal
          title="Add rotation"
          initial={DEFAULTS}
          programs={programOpts}
          students={studentOpts}
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

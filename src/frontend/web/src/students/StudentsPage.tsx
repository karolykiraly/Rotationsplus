import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { useMe } from "../useMe";
import { getStudent, type Student, type StudentInput, type StudentStatus } from "../api";
import { useStudents } from "./useStudents";
import { StudentFormModal, type StudentFormInitial } from "./StudentFormModal";
import { STUDENT_STATUSES, academicStatusLabel, studentStatusLabel, visaStatusLabel } from "./studentStatuses";

const DEFAULTS: StudentFormInitial = {
  firstName: "",
  lastName: "",
  email: "",
  mobilePhone: "",
  academicStatus: "MdStudent",
  visaStatus: "",
  medicalSchool: "",
  medicalSchoolCountry: "",
  city: "",
  state: "",
  status: "Registered",
  studentOid: ""
};

/** "new" = create; a guid = edit that student; null = no modal. */
type EditId = string | "new" | null;

export function StudentsPage() {
  const { user } = useMe();
  const [statusFilter, setStatusFilter] = useState<StudentStatus | "">("");
  const { list, create, update, remove } = useStudents(statusFilter);

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Student | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // For edit, load the full detail (the list row lacks school/country/oid).
  const detail = useQuery({
    queryKey: ["student", editId],
    queryFn: () => getStudent(editId as string),
    enabled: !!editId && editId !== "new"
  });

  if (user && !user.isAdmin) {
    return <div className="card state">You need the Admin role to manage students.</div>;
  }

  const closeForm = () => setEditId(null);

  const submitForm = (input: StudentInput) => {
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
        { onSuccess: () => { closeForm(); setBanner({ type: "ok", text: "Student updated." }); }, onError }
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

  const mapDetail = (d: NonNullable<typeof detail.data>): StudentFormInitial => ({
    firstName: d.firstName,
    lastName: d.lastName,
    email: d.email,
    mobilePhone: d.mobilePhone ?? "",
    academicStatus: d.academicStatus,
    visaStatus: d.visaStatus ?? "",
    medicalSchool: d.medicalSchool ?? "",
    medicalSchoolCountry: d.medicalSchoolCountry ?? "",
    city: d.city ?? "",
    state: d.state ?? "",
    status: d.status,
    studentOid: d.studentOid ?? ""
  });

  const students = list.data ?? [];

  return (
    <>
      <div className="page-head">
        <div>
          <h2>Students</h2>
          <p>The directory of students (the demand side of the marketplace).</p>
        </div>
        <button className="btn btn-primary" onClick={() => { setFormError(null); setEditId("new"); }}>
          Add student
        </button>
      </div>

      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="toolbar">
        <label htmlFor="s-filter">Status</label>
        <select
          id="s-filter"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as StudentStatus | "")}
        >
          <option value="">All statuses</option>
          {STUDENT_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
        </select>
      </div>

      <div className="card">
        {list.isLoading && <div className="state">Loading students…</div>}
        {list.isError && <div className="state">Couldn’t load students: {(list.error as Error).message}</div>}
        {!list.isLoading && !list.isError && students.length === 0 && (
          <div className="state">No students{statusFilter ? " with this status" : " yet"}.</div>
        )}

        {students.length > 0 && (
          <table className="data">
            <thead>
              <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Academic status</th>
                <th>Visa</th>
                <th>Location</th>
                <th>Status</th>
                <th style={{ width: 150, textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {students.map((s) => (
                <tr key={s.id}>
                  <td>{s.fullName}</td>
                  <td>{s.email}</td>
                  <td>{academicStatusLabel(s.academicStatus)}</td>
                  <td>{visaStatusLabel(s.visaStatus)}</td>
                  <td>{[s.city, s.state].filter(Boolean).join(", ") || "—"}</td>
                  <td><span className="badge">{studentStatusLabel(s.status)}</span></td>
                  <td>
                    <div className="row-actions">
                      <button className="btn-link" onClick={() => { setFormError(null); setEditId(s.id); }}>Edit</button>
                      <button className="btn-link danger" onClick={() => setDeleting(s)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {editId === "new" && (
        <StudentFormModal
          title="Add student"
          initial={DEFAULTS}
          pending={create.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && detail.data && (
        <StudentFormModal
          title="Edit student"
          initial={mapDetail(detail.data)}
          pending={update.isPending}
          serverError={formError}
          onSubmit={submitForm}
          onClose={closeForm}
        />
      )}

      {editId && editId !== "new" && !detail.data && (
        <Modal title="Edit student" onClose={closeForm}>
          <div className="modal-body state">
            {detail.isError ? `Couldn’t load student: ${(detail.error as Error).message}` : "Loading…"}
          </div>
        </Modal>
      )}

      {deleting && (
        <Modal title="Delete student" onClose={() => setDeleting(null)}>
          <div className="modal-body">
            Delete {deleting.fullName}? This can’t be undone.
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

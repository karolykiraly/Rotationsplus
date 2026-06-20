import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { getStudent, type Student, type StudentInput, type StudentStatus } from "../api";
import searchIcon from "../assets/icons/search.png";
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

const PAGE_SIZE = 10;

export function StudentsPage() {
  const { user } = useMe();
  const [statusFilter, setStatusFilter] = useState<StudentStatus | "">("");
  const { list, create, update, remove } = useStudents(statusFilter);

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Student | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => setPage(1), [statusFilter, search]);

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

  // Client-side search + pagination over the (server status-filtered) list.
  const q = search.trim().toLowerCase();
  const filtered = students.filter(
    (s) => !q || `${s.fullName} ${s.email} ${[s.city, s.state].filter(Boolean).join(", ")}`.toLowerCase().includes(q)
  );
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const rows = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);

  return (
    <>
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="lead-page">
        <div className="program-toolbar">
          <button className="btn btn-primary spacer" onClick={() => { setFormError(null); setEditId("new"); }}>
            Add student
          </button>
          <label htmlFor="s-filter">Status</label>
          <select
            id="s-filter"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value as StudentStatus | "")}
          >
            <option value="">All statuses</option>
            {STUDENT_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
          <div className="search-form2">
            <img src={searchIcon} alt="" />
            <input
              placeholder="Search for Name/Email/Location"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search for students"
            />
          </div>
        </div>

        {list.isLoading && <div className="state">Loading students…</div>}
        {list.isError && <div className="state">Couldn’t load students: {(list.error as Error).message}</div>}

        {!list.isLoading && !list.isError && (
          rows.length === 0 ? (
            <div className="state">There is no data available.</div>
          ) : (
            <table className="program-table">
              <tbody>
                {rows.map((s) => (
                  <tr key={s.id} className="rot-row">
                    <td className="first-td">
                      <div className="place-holder">Name</div>
                      <div className="heading-xxxs">{s.fullName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Email</div>
                      <div className="heading-xxxs-normal">{s.email}</div>
                    </td>
                    <td>
                      <div className="place-holder">Academic Status</div>
                      <div className="heading-xxxs-normal">{academicStatusLabel(s.academicStatus)}</div>
                    </td>
                    <td>
                      <div className="place-holder">Visa</div>
                      <div className="heading-xxxs-normal">{visaStatusLabel(s.visaStatus)}</div>
                    </td>
                    <td>
                      <div className="place-holder">Location</div>
                      <div className="heading-xxxs-normal">{[s.city, s.state].filter(Boolean).join(", ") || "—"}</div>
                    </td>
                    <td>
                      <div className="place-holder">Status</div>
                      <div><span className="badge">{studentStatusLabel(s.status)}</span></div>
                    </td>
                    <td className="last-td">
                      <div className="row-actions">
                        <button className="btn-link" onClick={() => { setFormError(null); setEditId(s.id); }}>Edit</button>
                        <button className="btn-link danger" onClick={() => setDeleting(s)}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )
        )}

        <Pagination page={safePage} pageSize={PAGE_SIZE} totalItems={filtered.length} onChange={setPage} />
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

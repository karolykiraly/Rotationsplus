import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { Pagination } from "../components/Pagination";
import { useMe } from "../useMe";
import { getPreceptor, type Preceptor, type PreceptorInput } from "../api";
import searchIcon from "../assets/icons/search.png";
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

const PAGE_SIZE = 10;

export function PreceptorsPage() {
  const { user } = useMe();
  const { list, create, update, remove } = usePreceptors();
  const specialties = usePreceptorSpecialties();

  const [editId, setEditId] = useState<EditId>(null);
  const [deleting, setDeleting] = useState<Preceptor | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => setPage(1), [search]);

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

  // Client-side search + pagination.
  const q = search.trim().toLowerCase();
  const filtered = preceptors.filter(
    (p) => !q || `${p.fullName} ${p.email} ${p.primarySpecialtyName} ${[p.city, p.state].filter(Boolean).join(", ")}`.toLowerCase().includes(q)
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
            Add preceptor
          </button>
          <div className="search-form2">
            <img src={searchIcon} alt="" />
            <input
              placeholder="Search for Name/Email/Specialty"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              aria-label="Search for preceptors"
            />
          </div>
        </div>

        {list.isLoading && <div className="state">Loading preceptors…</div>}
        {list.isError && <div className="state">Couldn’t load preceptors: {(list.error as Error).message}</div>}

        {!list.isLoading && !list.isError && (
          rows.length === 0 ? (
            <div className="state">There is no data available.</div>
          ) : (
            <table className="program-table">
              <tbody>
                {rows.map((p) => (
                  <tr key={p.id} className="rot-row">
                    <td className="first-td">
                      <div className="place-holder">Name</div>
                      <div className="heading-xxxs">{p.fullName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Email</div>
                      <div className="heading-xxxs-normal">{p.email}</div>
                    </td>
                    <td>
                      <div className="place-holder">Specialty</div>
                      <div className="heading-xxxs-normal">{p.primarySpecialtyName}</div>
                    </td>
                    <td>
                      <div className="place-holder">Location</div>
                      <div className="heading-xxxs-normal">{[p.city, p.state].filter(Boolean).join(", ") || "—"}</div>
                    </td>
                    <td>
                      <div className="place-holder">Status</div>
                      <div><span className="badge">{preceptorStatusLabel(p.status)}</span></div>
                    </td>
                    <td className="last-td">
                      <div className="row-actions">
                        <button className="btn-link" onClick={() => { setFormError(null); setEditId(p.id); }}>Edit</button>
                        <button className="btn-link danger" onClick={() => setDeleting(p)}>Delete</button>
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

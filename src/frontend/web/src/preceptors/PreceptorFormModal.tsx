import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Modal } from "../components/Modal";
import { type PreceptorInput, type PreceptorStatus, type Specialty } from "../api";
import { PRECEPTOR_STATUSES } from "./preceptorStatuses";

const optional = (max: number, label: string) =>
  z.string().trim().max(max, `${label} must be ${max} characters or fewer.`).optional();

// Mirrors the API's TryNormalize rules (required names/email + length caps, valid email).
const schema = z.object({
  firstName: z.string().trim().min(1, "First name is required.").max(100, "First name must be 100 characters or fewer."),
  lastName: z.string().trim().min(1, "Last name is required.").max(100, "Last name must be 100 characters or fewer."),
  email: z.string().trim().min(1, "Email is required.").max(256, "Email must be 256 characters or fewer.").email("Enter a valid email."),
  primarySpecialtyId: z.string().min(1, "Select a specialty."),
  status: z.string().min(1, "Select a status."),
  medicalLicenseNumber: optional(50, "License number"),
  licenseState: optional(50, "License state"),
  city: optional(100, "City"),
  state: optional(50, "State"),
  bio: optional(4000, "Bio")
});
type FormValues = z.infer<typeof schema>;

export interface PreceptorFormInitial {
  firstName: string;
  lastName: string;
  email: string;
  primarySpecialtyId: string;
  status: PreceptorStatus;
  medicalLicenseNumber: string;
  licenseState: string;
  city: string;
  state: string;
  bio: string;
}

interface Props {
  title: string;
  initial: PreceptorFormInitial;
  specialties: Specialty[];
  pending: boolean;
  serverError?: string | null;
  onSubmit: (input: PreceptorInput) => void;
  onClose: () => void;
}

const orNull = (v?: string) => (v && v.trim() ? v.trim() : null);

/** Create/edit form for a preceptor directory record. Client validation mirrors the server;
 *  server failures (duplicate email 409, unknown specialty 400) surface in a banner. */
export function PreceptorFormModal({ title, initial, specialties, pending, serverError, onSubmit, onClose }: Props) {
  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: initial });

  const submit = handleSubmit((v) =>
    onSubmit({
      firstName: v.firstName.trim(),
      lastName: v.lastName.trim(),
      email: v.email.trim(),
      primarySpecialtyId: v.primarySpecialtyId,
      status: v.status as PreceptorStatus,
      medicalLicenseNumber: orNull(v.medicalLicenseNumber),
      licenseState: orNull(v.licenseState),
      city: orNull(v.city),
      state: orNull(v.state),
      bio: orNull(v.bio)
    })
  );

  return (
    <Modal title={title} onClose={onClose} wide>
      {/* noValidate: let zod own validation (incl. the email message) instead of native bubbles. */}
      <form onSubmit={submit} noValidate>
        <div className="modal-body">
          {serverError && <div className="banner error" role="alert">{serverError}</div>}

          <div className="form-grid">
            <div className="field">
              <label htmlFor="pr-first">First name</label>
              <input id="pr-first" autoFocus {...register("firstName")} />
              {errors.firstName && <span className="err">{errors.firstName.message}</span>}
            </div>
            <div className="field">
              <label htmlFor="pr-last">Last name</label>
              <input id="pr-last" {...register("lastName")} />
              {errors.lastName && <span className="err">{errors.lastName.message}</span>}
            </div>

            <div className="field span-2">
              <label htmlFor="pr-email">Email</label>
              <input id="pr-email" type="email" autoComplete="off" {...register("email")} />
              {errors.email && <span className="err">{errors.email.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="pr-specialty">Primary specialty</label>
              <select id="pr-specialty" {...register("primarySpecialtyId")}>
                <option value="">Select…</option>
                {specialties.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
              {errors.primarySpecialtyId && <span className="err">{errors.primarySpecialtyId.message}</span>}
            </div>
            <div className="field">
              <label htmlFor="pr-status">Status</label>
              <select id="pr-status" {...register("status")}>
                {PRECEPTOR_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
              {errors.status && <span className="err">{errors.status.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="pr-license">License # <span className="hint">(optional)</span></label>
              <input id="pr-license" {...register("medicalLicenseNumber")} />
              {errors.medicalLicenseNumber && <span className="err">{errors.medicalLicenseNumber.message}</span>}
            </div>
            <div className="field">
              <label htmlFor="pr-licstate">License state <span className="hint">(optional)</span></label>
              <input id="pr-licstate" {...register("licenseState")} />
              {errors.licenseState && <span className="err">{errors.licenseState.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="pr-city">City <span className="hint">(optional)</span></label>
              <input id="pr-city" {...register("city")} />
              {errors.city && <span className="err">{errors.city.message}</span>}
            </div>
            <div className="field">
              <label htmlFor="pr-state">State <span className="hint">(optional)</span></label>
              <input id="pr-state" {...register("state")} />
              {errors.state && <span className="err">{errors.state.message}</span>}
            </div>

            <div className="field span-2">
              <label htmlFor="pr-bio">Bio <span className="hint">(optional)</span></label>
              <textarea id="pr-bio" {...register("bio")} />
              {errors.bio && <span className="err">{errors.bio.message}</span>}
            </div>
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={pending}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={pending}>{pending ? "Saving…" : "Save"}</button>
        </div>
      </form>
    </Modal>
  );
}

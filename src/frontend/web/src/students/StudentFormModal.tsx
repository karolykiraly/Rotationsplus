import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Modal } from "../components/Modal";
import { type AcademicStatus, type StudentInput, type StudentStatus, type VisaStatus } from "../api";
import { ACADEMIC_STATUSES, STUDENT_STATUSES, VISA_STATUSES } from "./studentStatuses";

// Mirrors the API's TryNormalize rules: required name/email + length caps, valid email, optional caps.
const schema = z.object({
  firstName: z.string().trim().min(1, "Enter a first name.").max(100, "At most 100 characters."),
  lastName: z.string().trim().min(1, "Enter a last name.").max(100, "At most 100 characters."),
  email: z.string().trim().min(1, "Enter an email.").max(256, "At most 256 characters.").email("Enter a valid email."),
  mobilePhone: z.string().trim().max(40, "At most 40 characters.").optional(),
  academicStatus: z.string().min(1, "Select an academic status."),
  visaStatus: z.string().optional(),
  medicalSchool: z.string().trim().max(200, "At most 200 characters.").optional(),
  medicalSchoolCountry: z.string().trim().max(100, "At most 100 characters.").optional(),
  city: z.string().trim().max(100, "At most 100 characters.").optional(),
  state: z.string().trim().max(50, "At most 50 characters.").optional(),
  status: z.string().min(1, "Select a status."),
  studentOid: z.string().trim().max(64, "At most 64 characters.").optional()
});
type FormValues = z.infer<typeof schema>;

export interface StudentFormInitial {
  firstName: string;
  lastName: string;
  email: string;
  mobilePhone: string;
  academicStatus: AcademicStatus;
  visaStatus: string; // "" when none
  medicalSchool: string;
  medicalSchoolCountry: string;
  city: string;
  state: string;
  status: StudentStatus;
  studentOid: string; // "" when unlinked
}

interface Props {
  title: string;
  initial: StudentFormInitial;
  pending: boolean;
  serverError?: string | null;
  onSubmit: (input: StudentInput) => void;
  onClose: () => void;
}

const orNull = (v?: string) => (v?.trim() ? v.trim() : null);

/** Create/edit form for a student directory record. Client validation mirrors the server; server-side
 *  failures (duplicate email) surface in a banner. */
export function StudentFormModal({ title, initial, pending, serverError, onSubmit, onClose }: Props) {
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
      mobilePhone: orNull(v.mobilePhone),
      academicStatus: v.academicStatus as AcademicStatus,
      visaStatus: v.visaStatus ? (v.visaStatus as VisaStatus) : null,
      medicalSchool: orNull(v.medicalSchool),
      medicalSchoolCountry: orNull(v.medicalSchoolCountry),
      city: orNull(v.city),
      state: orNull(v.state),
      status: v.status as StudentStatus,
      studentOid: orNull(v.studentOid)
    })
  );

  return (
    <Modal title={title} onClose={onClose} wide>
      {/* noValidate: let zod own validation so its messages render instead of native input bubbles. */}
      <form onSubmit={submit} noValidate>
        <div className="modal-body">
          {serverError && <div className="banner error" role="alert">{serverError}</div>}

          <div className="form-grid">
            <div className="field">
              <label htmlFor="s-first">First name</label>
              <input id="s-first" type="text" {...register("firstName")} />
              {errors.firstName && <span className="err">{errors.firstName.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-last">Last name</label>
              <input id="s-last" type="text" {...register("lastName")} />
              {errors.lastName && <span className="err">{errors.lastName.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-email">Email</label>
              <input id="s-email" type="email" {...register("email")} />
              {errors.email && <span className="err">{errors.email.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-phone">Mobile phone <span className="hint">(optional)</span></label>
              <input id="s-phone" type="tel" {...register("mobilePhone")} />
              {errors.mobilePhone && <span className="err">{errors.mobilePhone.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-academic">Academic status</label>
              <select id="s-academic" {...register("academicStatus")}>
                {ACADEMIC_STATUSES.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
              </select>
              {errors.academicStatus && <span className="err">{errors.academicStatus.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-visa">Visa status <span className="hint">(optional)</span></label>
              <select id="s-visa" {...register("visaStatus")}>
                <option value="">— Not captured —</option>
                {VISA_STATUSES.map((v) => <option key={v.value} value={v.value}>{v.label}</option>)}
              </select>
            </div>

            <div className="field">
              <label htmlFor="s-school">Medical school <span className="hint">(optional)</span></label>
              <input id="s-school" type="text" {...register("medicalSchool")} />
              {errors.medicalSchool && <span className="err">{errors.medicalSchool.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-country">School country <span className="hint">(optional)</span></label>
              <input id="s-country" type="text" {...register("medicalSchoolCountry")} />
              {errors.medicalSchoolCountry && <span className="err">{errors.medicalSchoolCountry.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-city">City <span className="hint">(optional)</span></label>
              <input id="s-city" type="text" {...register("city")} />
              {errors.city && <span className="err">{errors.city.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-state">State <span className="hint">(optional)</span></label>
              <input id="s-state" type="text" {...register("state")} />
              {errors.state && <span className="err">{errors.state.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-status">Status</label>
              <select id="s-status" {...register("status")}>
                {STUDENT_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
              {errors.status && <span className="err">{errors.status.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="s-oid">CIAM object id <span className="hint">(optional)</span></label>
              <input id="s-oid" type="text" {...register("studentOid")} />
              {errors.studentOid && <span className="err">{errors.studentOid.message}</span>}
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

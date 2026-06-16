import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Modal } from "../components/Modal";
import { type Program, type RotationInput, type RotationStatus } from "../api";
import { programTypeLabel } from "../programs/programTypes";
import { ROTATION_STATUSES } from "./rotationStatuses";

// Mirrors the API's TryValidate rules: name/email required + length caps, valid email, oid length,
// and EndDate strictly after StartDate (Weeks is derived server-side, not entered here).
const schema = z
  .object({
    programId: z.string().min(1, "Select a program."),
    studentName: z.string().trim().min(1, "Enter the student's name.").max(200, "At most 200 characters."),
    studentEmail: z
      .string()
      .trim()
      .min(1, "Enter the student's email.")
      .max(256, "At most 256 characters.")
      .email("Enter a valid email."),
    studentOid: z.string().trim().max(64, "At most 64 characters.").optional(),
    startDate: z.string().min(1, "Pick a start date."),
    endDate: z.string().min(1, "Pick an end date."),
    status: z.string().min(1, "Select a status.")
  })
  .refine((v) => v.endDate > v.startDate, {
    path: ["endDate"],
    message: "End date must be after the start date."
  });
type FormValues = z.infer<typeof schema>;

export interface RotationFormInitial {
  programId: string;
  studentName: string;
  studentEmail: string;
  studentOid: string; // "" when unlinked
  startDate: string; // YYYY-MM-DD
  endDate: string; // YYYY-MM-DD
  status: RotationStatus;
}

interface Props {
  title: string;
  initial: RotationFormInitial;
  programs: Program[];
  pending: boolean;
  serverError?: string | null;
  onSubmit: (input: RotationInput) => void;
  onClose: () => void;
}

/** Label a program for the dropdown: specialty · type (· preceptor). */
function programLabel(p: Program): string {
  const base = `${p.specialtyName} · ${programTypeLabel(p.programType)}`;
  return p.preceptorName ? `${base} · ${p.preceptorName}` : base;
}

/** Create/edit form for a rotation booking. Client validation mirrors the server; server-side failures
 *  (unknown program, bad email) surface in a banner. */
export function RotationFormModal({ title, initial, programs, pending, serverError, onSubmit, onClose }: Props) {
  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: initial });

  const submit = handleSubmit((v) =>
    onSubmit({
      programId: v.programId,
      studentName: v.studentName.trim(),
      studentEmail: v.studentEmail.trim(),
      studentOid: v.studentOid?.trim() ? v.studentOid.trim() : null,
      startDate: v.startDate,
      endDate: v.endDate,
      status: v.status as RotationStatus
    })
  );

  return (
    <Modal title={title} onClose={onClose} wide>
      {/* noValidate: let zod own validation so its messages render instead of native input bubbles. */}
      <form onSubmit={submit} noValidate>
        <div className="modal-body">
          {serverError && <div className="banner error" role="alert">{serverError}</div>}

          <div className="form-grid">
            <div className="field span-2">
              <label htmlFor="r-program">Program</label>
              <select id="r-program" {...register("programId")}>
                <option value="">Select…</option>
                {programs.map((p) => <option key={p.id} value={p.id}>{programLabel(p)}</option>)}
              </select>
              {errors.programId && <span className="err">{errors.programId.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="r-name">Student name</label>
              <input id="r-name" type="text" {...register("studentName")} />
              {errors.studentName && <span className="err">{errors.studentName.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="r-email">Student email</label>
              <input id="r-email" type="email" {...register("studentEmail")} />
              {errors.studentEmail && <span className="err">{errors.studentEmail.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="r-start">Start date</label>
              <input id="r-start" type="date" {...register("startDate")} />
              {errors.startDate && <span className="err">{errors.startDate.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="r-end">End date</label>
              <input id="r-end" type="date" {...register("endDate")} />
              {errors.endDate && <span className="err">{errors.endDate.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="r-status">Status</label>
              <select id="r-status" {...register("status")}>
                {ROTATION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
              {errors.status && <span className="err">{errors.status.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="r-oid">CIAM object id <span className="hint">(optional)</span></label>
              <input id="r-oid" type="text" {...register("studentOid")} />
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

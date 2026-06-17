import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Modal } from "../components/Modal";
import { type Program, type RotationInput, type RotationStatus, type Student } from "../api";
import { programTypeLabel } from "../programs/programTypes";
import { ROTATION_STATUSES } from "./rotationStatuses";

// Mirrors the API: a program + a directory student are required, status valid, end strictly after start
// (Weeks is derived server-side; the student's name/email/oid are snapshotted from the directory record).
const schema = z
  .object({
    programId: z.string().min(1, "Select a program."),
    studentId: z.string().min(1, "Select a student."),
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
  studentId: string; // "" when unset (legacy rotation with no directory link)
  startDate: string; // YYYY-MM-DD
  endDate: string; // YYYY-MM-DD
  status: RotationStatus;
}

interface Props {
  title: string;
  initial: RotationFormInitial;
  programs: Program[];
  students: Student[];
  /** Statuses the dropdown may offer: every status when creating; the current + its allowed
   *  transitions when editing (so the form can't request a move the server would reject). */
  allowedStatuses: RotationStatus[];
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

/** Create/edit form for a rotation booking. The student is chosen from the directory; client validation
 *  mirrors the server, and server-side failures (unknown program/student) surface in a banner. */
export function RotationFormModal({ title, initial, programs, students, allowedStatuses, pending, serverError, onSubmit, onClose }: Props) {
  // Render in canonical lifecycle order, limited to the statuses this form may offer.
  const statusOptions = ROTATION_STATUSES.filter((s) => allowedStatuses.includes(s.value));
  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: initial });

  const submit = handleSubmit((v) =>
    onSubmit({
      programId: v.programId,
      studentId: v.studentId,
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

            <div className="field span-2">
              <label htmlFor="r-student">Student</label>
              <select id="r-student" {...register("studentId")}>
                <option value="">Select…</option>
                {students.map((s) => <option key={s.id} value={s.id}>{s.fullName} — {s.email}</option>)}
              </select>
              {errors.studentId && <span className="err">{errors.studentId.message}</span>}
              {students.length === 0 && (
                <span className="hint">No students yet — add one in the Students directory first.</span>
              )}
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
                {statusOptions.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
              {errors.status && <span className="err">{errors.status.message}</span>}
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

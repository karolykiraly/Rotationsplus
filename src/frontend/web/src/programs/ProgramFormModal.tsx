import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Modal } from "../components/Modal";
import { type Preceptor, type ProgramInput, type ProgramType, type Specialty } from "../api";
import { PROGRAM_TYPES } from "./programTypes";

// Mirrors the API's TryValidate rules: capacity caps, money ceiling + cents-only, description length.
const money = z.coerce
  .number({ message: "Enter an amount." })
  .min(0, "Must be 0 or more.")
  .max(99_999_999.99, "Too large.")
  .refine((v) => Math.abs(v * 100 - Math.round(v * 100)) < 1e-6, "At most 2 decimal places.");

const schema = z.object({
  specialtyId: z.string().min(1, "Select a specialty."),
  programType: z.string().min(1, "Select a type."),
  maxStudentsPerRotation: z.coerce.number().int("Whole number.").min(1, "At least 1.").max(1000, "At most 1000."),
  minWeeksPerRotation: z.coerce.number().int("Whole number.").min(1, "At least 1.").max(520, "At most 520."),
  retailAmountPerWeek: money,
  weeklyHonorarium: money,
  preceptorId: z.string().optional(),
  description: z.string().max(4000, "At most 4000 characters.").optional()
});
type FormValues = z.infer<typeof schema>;

export interface ProgramFormInitial {
  specialtyId: string;
  programType: ProgramType;
  maxStudentsPerRotation: number;
  minWeeksPerRotation: number;
  retailAmountPerWeek: number;
  weeklyHonorarium: number;
  preceptorId: string; // "" when unassigned
  description: string; // "" when none
}

interface Props {
  title: string;
  initial: ProgramFormInitial;
  specialties: Specialty[];
  preceptors: Preceptor[];
  pending: boolean;
  serverError?: string | null;
  onSubmit: (input: ProgramInput) => void;
  onClose: () => void;
  /** When provided (edit mode), a Delete action appears in the footer. */
  onDelete?: () => void;
  /** When provided (edit mode), a "Required documents" action appears in the footer. */
  onConfigureDocuments?: () => void;
}

/** Create/edit form for a marketplace program. Client validation mirrors the server; server-side
 *  failures (bad specialty/preceptor, money precision) surface in a banner. */
export function ProgramFormModal({ title, initial, specialties, preceptors, pending, serverError, onSubmit, onClose, onDelete, onConfigureDocuments }: Props) {
  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: initial });

  const submit = handleSubmit((v) =>
    onSubmit({
      specialtyId: v.specialtyId,
      programType: v.programType as ProgramType,
      maxStudentsPerRotation: v.maxStudentsPerRotation,
      minWeeksPerRotation: v.minWeeksPerRotation,
      retailAmountPerWeek: v.retailAmountPerWeek,
      weeklyHonorarium: v.weeklyHonorarium,
      description: v.description?.trim() ? v.description.trim() : null,
      preceptorId: v.preceptorId ? v.preceptorId : null
    })
  );

  return (
    <Modal title={title} onClose={onClose} wide>
      {/* noValidate: let zod own validation so its messages render, instead of the native number-input
          stepMismatch bubble blocking submit before the resolver runs. */}
      <form onSubmit={submit} noValidate>
        <div className="modal-body">
          {serverError && <div className="banner error" role="alert">{serverError}</div>}

          <div className="form-grid">
            <div className="field">
              <label htmlFor="p-specialty">Specialty</label>
              <select id="p-specialty" {...register("specialtyId")}>
                <option value="">Select…</option>
                {specialties.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
              {errors.specialtyId && <span className="err">{errors.specialtyId.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="p-type">Type</label>
              <select id="p-type" {...register("programType")}>
                {PROGRAM_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
              {errors.programType && <span className="err">{errors.programType.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="p-max">Max students / rotation</label>
              <input id="p-max" type="number" min={1} step={1} {...register("maxStudentsPerRotation")} />
              {errors.maxStudentsPerRotation && <span className="err">{errors.maxStudentsPerRotation.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="p-weeks">Min weeks / rotation</label>
              <input id="p-weeks" type="number" min={1} step={1} {...register("minWeeksPerRotation")} />
              {errors.minWeeksPerRotation && <span className="err">{errors.minWeeksPerRotation.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="p-retail">Retail / week ($)</label>
              <input id="p-retail" type="number" min={0} step={0.01} {...register("retailAmountPerWeek")} />
              {errors.retailAmountPerWeek && <span className="err">{errors.retailAmountPerWeek.message}</span>}
            </div>

            <div className="field">
              <label htmlFor="p-honorarium">Weekly honorarium ($)</label>
              <input id="p-honorarium" type="number" min={0} step={0.01} {...register("weeklyHonorarium")} />
              {errors.weeklyHonorarium && <span className="err">{errors.weeklyHonorarium.message}</span>}
            </div>

            <div className="field span-2">
              <label htmlFor="p-preceptor">Preceptor <span className="hint">(optional)</span></label>
              <select id="p-preceptor" {...register("preceptorId")}>
                <option value="">— Unassigned —</option>
                {preceptors.map((p) => <option key={p.id} value={p.id}>{p.fullName}</option>)}
              </select>
            </div>

            <div className="field span-2">
              <label htmlFor="p-desc">Description <span className="hint">(optional)</span></label>
              <textarea id="p-desc" {...register("description")} />
              {errors.description && <span className="err">{errors.description.message}</span>}
            </div>
          </div>
        </div>
        <div className="modal-foot">
          {onDelete && (
            <button type="button" className="btn-link danger" style={{ marginRight: "auto" }} onClick={onDelete} disabled={pending}>
              Delete
            </button>
          )}
          {onConfigureDocuments && (
            <button type="button" className="btn-link" style={onDelete ? undefined : { marginRight: "auto" }} onClick={onConfigureDocuments} disabled={pending}>
              Required documents
            </button>
          )}
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={pending}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={pending}>{pending ? "Saving…" : "Save"}</button>
        </div>
      </form>
    </Modal>
  );
}

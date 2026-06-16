import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Modal } from "../components/Modal";

// Mirrors the API's TryNormalizeName rule (required, trimmed, <= 200 chars).
const schema = z.object({
  name: z.string().trim().min(1, "Name is required.").max(200, "Name must be 200 characters or fewer.")
});
type FormValues = z.infer<typeof schema>;

interface Props {
  title: string;
  initialName?: string;
  pending: boolean;
  serverError?: string | null;
  onSubmit: (name: string) => void;
  onClose: () => void;
}

/** Create/edit form for a specialty. Client validation mirrors the server; server-side failures
 *  (e.g. 409 duplicate) surface in a banner so the user can correct and retry. */
export function SpecialtyFormModal({ title, initialName = "", pending, serverError, onSubmit, onClose }: Props) {
  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: initialName }
  });

  return (
    <Modal title={title} onClose={onClose}>
      <form onSubmit={handleSubmit((v) => onSubmit(v.name.trim()))}>
        <div className="modal-body">
          {serverError && <div className="banner error" role="alert">{serverError}</div>}
          <div className="field">
            <label htmlFor="specialty-name">Name</label>
            <input
              id="specialty-name"
              autoFocus
              autoComplete="off"
              {...register("name")}
            />
            {errors.name && <span className="err">{errors.name.message}</span>}
          </div>
        </div>
        <div className="modal-foot">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={pending}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={pending}>
            {pending ? "Saving…" : "Save"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import {
  createDocumentType,
  getProgramRequiredDocuments,
  setProgramRequiredDocuments,
  type DocumentCategory,
  type DocumentType,
  type ProgramRequiredDocuments
} from "../api";

interface Props {
  programId: string;
  programLabel: string;
  onClose: () => void;
}

const CATEGORIES: DocumentCategory[] = [
  "Immunization", "Identity", "Insurance", "Certification", "Professional", "MedicalTest", "Agreement", "Other"
];

/** Configure which documents a program requires (the checklist), the due-days, and add custom document
 *  types — the rewrite of the legacy "Required Documents" admin form (PHASE 2g-3b), placed on the
 *  Program. Saves through the program required-documents endpoint. */
export function ProgramDocumentsModal({ programId, programLabel, onClose }: Props) {
  const queryClient = useQueryClient();
  const config = useQuery({
    queryKey: ["program-required-documents", programId],
    queryFn: () => getProgramRequiredDocuments(programId)
  });

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [dueDays, setDueDays] = useState(14);
  const [customName, setCustomName] = useState("");
  const [customCategory, setCustomCategory] = useState<DocumentCategory>("Other");

  // Seed local state from the saved config exactly ONCE, on first load. Re-seeding on every
  // config.data change would clobber the admin's in-progress checkbox/due-days edits the moment the
  // catalog cache updates (e.g. after adding a custom type below).
  const seeded = useRef(false);
  useEffect(() => {
    if (config.data && !seeded.current) {
      setSelected(new Set(config.data.requiredDocumentTypeIds));
      setDueDays(config.data.documentDueDays);
      seeded.current = true;
    }
  }, [config.data]);

  const addCustom = useMutation({
    mutationFn: () => createDocumentType(customName.trim(), customCategory),
    onSuccess: (created) => {
      setSelected((s) => new Set(s).add(created.id)); // auto-select the new type
      setCustomName("");
      // Append the new type into the cached catalog directly (don't refetch — that would re-seed
      // and wipe the admin's current edits). The once-only seed guard above is preserved.
      queryClient.setQueryData<ProgramRequiredDocuments>(
        ["program-required-documents", programId],
        (prev) => (prev ? { ...prev, catalog: [...prev.catalog, created] } : prev)
      );
    }
  });

  const save = useMutation({
    mutationFn: () => setProgramRequiredDocuments(programId, dueDays, [...selected]),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["program-required-documents", programId] });
      onClose();
    }
  });

  const catalog = config.data?.catalog ?? [];
  const toggle = (id: string) =>
    setSelected((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const byCategory = (cat: DocumentCategory): DocumentType[] => catalog.filter((t) => t.category === cat);

  return (
    <Modal title={`Required Documents — ${programLabel}`} onClose={onClose} wide>
      <div className="modal-body">
        {config.isLoading && <div className="state">Loading…</div>}
        {config.isError && (
          <div className="banner error" role="alert">Couldn’t load the configuration: {(config.error as Error).message}</div>
        )}

        {config.data && (
          <>
            <div className="doc-config-grid">
              {CATEGORIES.map((cat) => {
                const types = byCategory(cat);
                if (types.length === 0) return null;
                return (
                  <fieldset key={cat} className="doc-config-group">
                    <legend>{cat}</legend>
                    {types.map((t) => (
                      <label key={t.id} className="doc-check">
                        <input type="checkbox" checked={selected.has(t.id)} onChange={() => toggle(t.id)} />
                        {t.name}
                      </label>
                    ))}
                  </fieldset>
                );
              })}
            </div>

            <div className="doc-config-row">
              <label htmlFor="dc-add">Add custom document type</label>
              <div className="doc-add">
                <input
                  id="dc-add"
                  value={customName}
                  onChange={(e) => setCustomName(e.target.value)}
                  placeholder="e.g. Hospital Orientation"
                />
                <select value={customCategory} onChange={(e) => setCustomCategory(e.target.value as DocumentCategory)} aria-label="Category">
                  {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
                </select>
                <button
                  type="button"
                  className="btn btn-ghost button-sm"
                  onClick={() => addCustom.mutate()}
                  disabled={!customName.trim() || addCustom.isPending}
                >
                  {addCustom.isPending ? "Adding…" : "Add"}
                </button>
              </div>
              {addCustom.isError && (
                <span className="err">{(addCustom.error as Error).message}</span>
              )}
            </div>

            <div className="doc-config-row">
              <label htmlFor="dc-due">Documents due (days before start)</label>
              <input
                id="dc-due"
                type="number"
                min={0}
                max={365}
                value={dueDays}
                onChange={(e) => setDueDays(e.target.value === "" ? 0 : Number(e.target.value))}
                className="doc-due-input"
              />
            </div>

            {save.isError && <div className="banner error" role="alert">{(save.error as Error).message}</div>}
          </>
        )}
      </div>

      <div className="modal-foot">
        <button type="button" className="btn btn-ghost" onClick={onClose} disabled={save.isPending}>Cancel</button>
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => save.mutate()}
          disabled={!config.data || save.isPending || dueDays < 0 || dueDays > 365}
        >
          {save.isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </Modal>
  );
}

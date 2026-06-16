import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createPreceptor,
  deletePreceptor,
  getPreceptors,
  getSpecialties,
  updatePreceptor,
  type Preceptor,
  type PreceptorInput,
  type Specialty
} from "../api";

const KEY = ["preceptors"];

/** List query + create/update/delete mutations for the preceptor directory. */
export function usePreceptors() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: KEY });

  const list = useQuery<Preceptor[]>({ queryKey: KEY, queryFn: getPreceptors });

  const create = useMutation({
    mutationFn: (input: PreceptorInput) => createPreceptor(input),
    onSuccess: invalidate
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: PreceptorInput }) => updatePreceptor(id, input),
    // Seed the detail cache from the response so a re-edit pre-fills from fresh data, not the
    // pre-save snapshot the form captured at mount (same fix as the programs slice).
    onSuccess: (data, vars) => {
      invalidate();
      qc.setQueryData(["preceptor", vars.id], data);
    }
  });

  const remove = useMutation({
    mutationFn: (id: string) => deletePreceptor(id),
    onSuccess: (_data, id) => {
      invalidate();
      qc.removeQueries({ queryKey: ["preceptor", id] });
    }
  });

  return { list, create, update, remove };
}

/** Specialty options for the primary-specialty dropdown. */
export function usePreceptorSpecialties() {
  return useQuery<Specialty[]>({ queryKey: ["specialties"], queryFn: getSpecialties });
}

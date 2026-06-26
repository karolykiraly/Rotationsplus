import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createProgram,
  deleteProgram,
  getPreceptorOptions,
  getPrograms,
  getSpecialties,
  updateProgram,
  type Program,
  type ProgramInput,
  type Preceptor,
  type Specialty
} from "../api";

const KEY = ["programs"];

/** List query + create/update/delete mutations for marketplace programs. */
export function usePrograms() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: KEY });

  const list = useQuery<Program[]>({ queryKey: KEY, queryFn: getPrograms });

  const create = useMutation({
    mutationFn: (input: ProgramInput) => createProgram(input),
    onSuccess: invalidate
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: ProgramInput }) => updateProgram(id, input),
    // Refresh the list AND seed the detail cache with the server's response, so a re-edit pre-fills
    // from fresh data instead of the pre-save snapshot the form captured at mount.
    onSuccess: (data, vars) => {
      invalidate();
      qc.setQueryData(["program", vars.id], data);
    }
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteProgram(id),
    onSuccess: (_data, id) => {
      invalidate();
      qc.removeQueries({ queryKey: ["program", id] });
    }
  });

  return { list, create, update, remove };
}

/** Specialty + preceptor option lists for the program form dropdowns. */
export function useProgramFormOptions() {
  const specialties = useQuery<Specialty[]>({ queryKey: ["specialties"], queryFn: getSpecialties });
  const preceptors = useQuery<Preceptor[]>({ queryKey: ["preceptor-options"], queryFn: getPreceptorOptions });
  return { specialties, preceptors };
}

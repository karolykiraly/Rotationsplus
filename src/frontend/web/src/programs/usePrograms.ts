import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createProgram,
  deleteProgram,
  getPreceptorOptions,
  getPrograms,
  getSpecialties,
  updateProgram,
  type PagedResponse,
  type Program,
  type ProgramFilter,
  type ProgramInput,
  type ProgramType,
  type Preceptor,
  type Specialty
} from "../api";

/** Server-paginated program list (program-type tabs + name search + Filter modal + page) + create/update/
 *  delete mutations. keepPreviousData keeps the current page visible while the next loads. */
export function usePrograms(
  programTypes: ProgramType[], search: string, page: number, pageSize: number, filter: ProgramFilter
) {
  const qc = useQueryClient();
  // Invalidate every program list (all tabs/pages) AND the full catalog (browse + form picker), not just one.
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["programs"] });
    qc.invalidateQueries({ queryKey: ["program-catalog"] });
  };

  const list = useQuery<PagedResponse<Program>>({
    queryKey: ["programs", { programTypes, search: search || null, page, pageSize, filter }],
    queryFn: () => getPrograms({ programType: programTypes, q: search || undefined, page, pageSize, ...filter }),
    placeholderData: keepPreviousData
  });

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

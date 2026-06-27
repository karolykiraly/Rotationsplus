import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createPreceptor,
  deletePreceptor,
  getPreceptors,
  getSpecialties,
  updatePreceptor,
  type PagedResponse,
  type Preceptor,
  type PreceptorInput,
  type Specialty
} from "../api";

/** Server-paginated preceptor list (free-text search + page) + create/update/delete mutations.
 *  keepPreviousData keeps the current page visible while the next loads (no flash on page/search). */
export function usePreceptors(search: string, page: number, pageSize: number) {
  const qc = useQueryClient();
  // Invalidate every preceptor list (all filters/pages) AND the picker options, not just the active one.
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["preceptors"] });
    qc.invalidateQueries({ queryKey: ["preceptor-options"] });
  };

  const list = useQuery<PagedResponse<Preceptor>>({
    queryKey: ["preceptors", { search: search || null, page, pageSize }],
    queryFn: () => getPreceptors({ q: search || undefined, page, pageSize }),
    placeholderData: keepPreviousData
  });

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

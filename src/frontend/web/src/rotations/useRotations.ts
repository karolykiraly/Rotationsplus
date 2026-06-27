import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createRotation,
  getProgramCatalog,
  getRotations,
  getStudentOptions,
  updateRotation,
  type PagedResponse,
  type Program,
  type Rotation,
  type RotationFilter,
  type RotationInput,
  type Student
} from "../api";

/** One server-paginated rotation section (Current or Historical, selected by `scope`) with its own
 *  free-text search + page. `keepPreviousData` keeps the current page visible while the next loads, so
 *  paging/searching doesn't flash an empty table. Mutations live in {@link useRotationMutations} so the
 *  two sections + the detail panel share one create/update path. */
export function useRotationsList(
  scope: "current" | "historical", search: string, page: number, pageSize: number, filter: RotationFilter
) {
  return useQuery<PagedResponse<Rotation>>({
    queryKey: ["rotations", { scope, search: search || null, page, pageSize, filter }],
    queryFn: () => getRotations({
      scope,
      q: search || undefined,
      page,
      pageSize,
      ...filter
    }),
    placeholderData: keepPreviousData
  });
}

/** The shared rotation create/update mutations (the Add modal + the Selected Rotation panel). Both
 *  invalidate every rotation list so the Current/Historical sections refetch (a status change can move a
 *  row between them); update also seeds the detail cache so the panel re-reads fresh, not the pre-save snapshot. */
export function useRotationMutations() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: ["rotations"] });

  const create = useMutation({
    mutationFn: (input: RotationInput) => createRotation(input),
    onSuccess: invalidate
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: RotationInput }) => updateRotation(id, input),
    onSuccess: (data, vars) => {
      invalidate();
      qc.setQueryData(["rotation", vars.id], data);
    }
  });

  return { create, update };
}

/** Program option list for the rotation form's program dropdown (the unpaginated catalog endpoint). */
export function useRotationPrograms() {
  return useQuery<Program[]>({ queryKey: ["program-catalog"], queryFn: getProgramCatalog });
}

/** Student option list for the rotation form's student picker (the unpaginated options endpoint). */
export function useRotationStudents() {
  return useQuery<Student[]>({ queryKey: ["student-options"], queryFn: getStudentOptions });
}

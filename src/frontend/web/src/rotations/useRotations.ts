import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createRotation,
  deleteRotation,
  getProgramCatalog,
  getRotations,
  getStudentOptions,
  refundRotation,
  updateRotation,
  type PagedResponse,
  type Program,
  type Rotation,
  type RotationInput,
  type RotationStatus,
  type Student
} from "../api";

/** Server-paginated rotation list (status filter + free-text search + page) plus create/update/delete
 *  mutations. `keepPreviousData` keeps the current page visible while the next loads, so paging/searching
 *  doesn't flash an empty table. */
export function useRotations(status: RotationStatus | "", search: string, page: number, pageSize: number) {
  const qc = useQueryClient();
  // Invalidate every rotation list (all filters/pages), not just the active one.
  const invalidate = () => qc.invalidateQueries({ queryKey: ["rotations"] });

  const list = useQuery<PagedResponse<Rotation>>({
    queryKey: ["rotations", { status: status || null, search: search || null, page, pageSize }],
    queryFn: () => getRotations({
      status: status || undefined,
      q: search || undefined,
      page,
      pageSize
    }),
    placeholderData: keepPreviousData
  });

  const create = useMutation({
    mutationFn: (input: RotationInput) => createRotation(input),
    onSuccess: invalidate
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: RotationInput }) => updateRotation(id, input),
    // Refresh the list AND seed the detail cache with the server's response, so a re-edit pre-fills
    // from fresh data instead of the pre-save snapshot the form captured at mount.
    onSuccess: (data, vars) => {
      invalidate();
      qc.setQueryData(["rotation", vars.id], data);
    }
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteRotation(id),
    onSuccess: (_data, id) => {
      invalidate();
      qc.removeQueries({ queryKey: ["rotation", id] });
    }
  });

  const refund = useMutation({
    mutationFn: (id: string) => refundRotation(id),
    onSuccess: (_data, id) => {
      invalidate();
      qc.invalidateQueries({ queryKey: ["rotation", id] });
    }
  });

  return { list, create, update, remove, refund };
}

/** Program option list for the rotation form's program dropdown (the unpaginated catalog endpoint). */
export function useRotationPrograms() {
  return useQuery<Program[]>({ queryKey: ["program-catalog"], queryFn: getProgramCatalog });
}

/** Student option list for the rotation form's student picker (the unpaginated options endpoint). */
export function useRotationStudents() {
  return useQuery<Student[]>({ queryKey: ["student-options"], queryFn: getStudentOptions });
}

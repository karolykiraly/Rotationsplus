import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createStudent,
  deleteStudent,
  getStudents,
  updateStudent,
  type PagedResponse,
  type Student,
  type StudentInput,
  type StudentStatus
} from "../api";

/** Server-paginated student list (status filter + free-text search + page) + create/update/delete mutations.
 *  keepPreviousData keeps the current page visible while the next loads (no flash on page/search). */
export function useStudents(status: StudentStatus | "", search: string, page: number, pageSize: number) {
  const qc = useQueryClient();
  // Invalidate every student list (all filters/pages) AND the picker options, not just the active one.
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["students"] });
    qc.invalidateQueries({ queryKey: ["student-options"] });
  };

  const list = useQuery<PagedResponse<Student>>({
    queryKey: ["students", { status: status || null, search: search || null, page, pageSize }],
    queryFn: () => getStudents({
      status: status || undefined,
      q: search || undefined,
      page,
      pageSize
    }),
    placeholderData: keepPreviousData
  });

  const create = useMutation({
    mutationFn: (input: StudentInput) => createStudent(input),
    onSuccess: invalidate
  });

  const update = useMutation({
    mutationFn: ({ id, input }: { id: string; input: StudentInput }) => updateStudent(id, input),
    // Refresh the list AND seed the detail cache with the server's response, so a re-edit pre-fills
    // from fresh data instead of the pre-save snapshot the form captured at mount.
    onSuccess: (data, vars) => {
      invalidate();
      qc.setQueryData(["student", vars.id], data);
    }
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteStudent(id),
    onSuccess: (_data, id) => {
      invalidate();
      qc.removeQueries({ queryKey: ["student", id] });
    }
  });

  return { list, create, update, remove };
}

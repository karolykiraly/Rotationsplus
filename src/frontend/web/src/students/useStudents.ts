import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createStudent,
  deleteStudent,
  getStudents,
  updateStudent,
  type Student,
  type StudentInput,
  type StudentStatus
} from "../api";

/** List query (optionally filtered by lifecycle status) + create/update/delete mutations for students. */
export function useStudents(status: StudentStatus | "") {
  const qc = useQueryClient();
  // Invalidate every student list (all status filters), not just the active one.
  const invalidate = () => qc.invalidateQueries({ queryKey: ["students"] });

  const list = useQuery<Student[]>({
    queryKey: ["students", { status: status || null }],
    queryFn: () => getStudents(status ? { status } : undefined)
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

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createRotation,
  deleteRotation,
  getPrograms,
  getRotations,
  updateRotation,
  type Program,
  type Rotation,
  type RotationInput,
  type RotationStatus
} from "../api";

/** List query (optionally filtered by status) + create/update/delete mutations for rotations. */
export function useRotations(status: RotationStatus | "") {
  const qc = useQueryClient();
  // Invalidate every rotation list (all status filters), not just the active one.
  const invalidate = () => qc.invalidateQueries({ queryKey: ["rotations"] });

  const list = useQuery<Rotation[]>({
    queryKey: ["rotations", { status: status || null }],
    queryFn: () => getRotations(status ? { status } : undefined)
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

  return { list, create, update, remove };
}

/** Program option list for the rotation form's program dropdown. */
export function useRotationPrograms() {
  return useQuery<Program[]>({ queryKey: ["programs"], queryFn: getPrograms });
}

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createSpecialty,
  deleteSpecialty,
  getSpecialties,
  updateSpecialty,
  type Specialty
} from "../api";

const KEY = ["specialties"];

/** List query + create/update/delete mutations for marketplace specialties. Every mutation
 *  invalidates the list so the table reflects the server after a write. */
export function useSpecialties() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: KEY });

  const list = useQuery<Specialty[]>({ queryKey: KEY, queryFn: getSpecialties });

  const create = useMutation({
    mutationFn: (name: string) => createSpecialty(name),
    onSuccess: invalidate
  });

  const update = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) => updateSpecialty(id, name),
    onSuccess: invalidate
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteSpecialty(id),
    onSuccess: invalidate
  });

  return { list, create, update, remove };
}

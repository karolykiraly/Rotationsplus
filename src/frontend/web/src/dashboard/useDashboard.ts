import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getDashboard, setRotationConfirmations, type Dashboard } from "../api";

/** Loads the admin dashboard aggregate (GET /api/dashboard). */
export function useDashboard() {
  return useQuery<Dashboard>({ queryKey: ["dashboard"], queryFn: getDashboard });
}

/** Toggles an Upcoming-Starts row's "Documents Approved" / "Preceptor Confirmed" flags, then refreshes
 *  the dashboard so the checkboxes reflect the persisted state. */
export function useRotationConfirmations() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, documentsApproved, preceptorConfirmed }: { id: string; documentsApproved: boolean; preceptorConfirmed: boolean }) =>
      setRotationConfirmations(id, { documentsApproved, preceptorConfirmed }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["dashboard"] })
  });
}

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getPreceptors, savePreceptorPermissions, type PagedResponse, type Preceptor } from "../api";

/** The admin preceptor-approval queue (/admin/permission): the Pending preceptors (server-paginated) plus
 *  a single batch Save that activates the checked rows and rejects the others — mirroring the production
 *  Activated/Reject checkbox + Save flow. A save changes statuses, so it invalidates the queue + the
 *  directory list + the form-picker options + the dashboard to-do count. keepPreviousData keeps the
 *  current page visible while the next loads. */
export function usePermissionQueue(page: number, pageSize: number) {
  const qc = useQueryClient();
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["preceptors"] });
    qc.invalidateQueries({ queryKey: ["preceptor-options"] });
    qc.invalidateQueries({ queryKey: ["dashboard-todos"] });
  };

  const list = useQuery<PagedResponse<Preceptor>>({
    queryKey: ["preceptors", { status: "Pending", page, pageSize }],
    queryFn: () => getPreceptors({ status: "Pending", page, pageSize }),
    placeholderData: keepPreviousData
  });

  const save = useMutation({
    mutationFn: ({ activateIds, rejectIds }: { activateIds: string[]; rejectIds: string[] }) =>
      savePreceptorPermissions(activateIds, rejectIds),
    onSuccess: invalidate
  });

  return { list, save };
}

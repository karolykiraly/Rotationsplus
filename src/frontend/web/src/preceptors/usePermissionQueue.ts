import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { approvePreceptor, getPreceptors, rejectPreceptor, type PagedResponse, type Preceptor, type PreceptorDetail } from "../api";

/** The admin preceptor-approval queue (/admin/permission): Pending preceptors (server-paginated) plus
 *  approve/reject mutations. A decision drops the row from the queue and updates the dashboard to-do count.
 *  keepPreviousData keeps the current page visible while the next loads. */
export function usePermissionQueue(page: number, pageSize: number) {
  const qc = useQueryClient();
  // Invalidate the queue + directory list AND the form-picker options (the decided preceptor's status
  // changed), and refresh the dashboard to-do count. Mirrors usePreceptors' invalidation breadth.
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["preceptors"] });
    qc.invalidateQueries({ queryKey: ["preceptor-options"] });
    qc.invalidateQueries({ queryKey: ["dashboard-todos"] });
  };
  // Seed the detail cache from the response so a follow-on edit pre-fills from the post-decision state.
  const settle = (data: PreceptorDetail) => { invalidate(); qc.setQueryData(["preceptor", data.id], data); };

  const list = useQuery<PagedResponse<Preceptor>>({
    queryKey: ["preceptors", { status: "Pending", page, pageSize }],
    queryFn: () => getPreceptors({ status: "Pending", page, pageSize }),
    placeholderData: keepPreviousData
  });

  const approve = useMutation({
    mutationFn: (id: string) => approvePreceptor(id),
    onSuccess: settle
  });

  const reject = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => rejectPreceptor(id, reason),
    onSuccess: settle
  });

  return { list, approve, reject };
}

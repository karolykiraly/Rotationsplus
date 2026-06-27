import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  deleteHonorarium,
  getHonorariums,
  payHonorarium,
  setHonorariumRefund,
  type Honorarium,
  type HonorariumStage,
  type PagedResponse
} from "../api";

/** The admin honorarium (preceptor payout) screen for one stage tab: a server-paginated list of payout
 *  rows plus the pay / refunded-flag mutations. Paying a stage can unlock the next stage's "Pay" on
 *  another tab, so a mutation invalidates the WHOLE ["honorariums"] key (all stages + pages), not just
 *  the current one. keepPreviousData keeps the page visible while the next loads. */
export function useHonorariums(stage: HonorariumStage, page: number, pageSize: number) {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: ["honorariums"] });

  const list = useQuery<PagedResponse<Honorarium>>({
    queryKey: ["honorariums", { stage, page, pageSize }],
    queryFn: () => getHonorariums({ stage, page, pageSize }),
    placeholderData: keepPreviousData
  });

  const pay = useMutation({
    mutationFn: (id: string) => payHonorarium(id),
    onSuccess: invalidate
  });

  const setRefund = useMutation({
    mutationFn: ({ id, refunded }: { id: string; refunded: boolean }) => setHonorariumRefund(id, refunded),
    onSuccess: invalidate
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteHonorarium(id),
    onSuccess: invalidate
  });

  return { list, pay, setRefund, remove };
}

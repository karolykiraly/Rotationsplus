import { useQuery } from "@tanstack/react-query";
import { getCustomerMe } from "./customerApi";

/** Loads + caches the signed-in customer's identity (GET /api/customer/me). Returns explicit fields
 *  (not a spread of the query result — RQ v5's result exposes a `promise` getter that floats on error). */
export function useCustomerMe() {
  const query = useQuery({ queryKey: ["customer-me"], queryFn: getCustomerMe, staleTime: 5 * 60_000 });
  return {
    customer: query.data,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error
  };
}

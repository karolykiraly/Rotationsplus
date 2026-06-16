import { QueryClient } from "@tanstack/react-query";

/** Single app-wide TanStack Query client. Conservative defaults: one retry, refetch off on focus
 *  (staff tools don't need aggressive refetch), 30s freshness window. */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000
    }
  }
});

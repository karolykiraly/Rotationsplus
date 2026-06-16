import { useQuery } from "@tanstack/react-query";
import { getMe, type MeResponse } from "./api";

/** Roles the SPA reasons about for nav/page gating (server still enforces every endpoint). */
export const ROLE_ADMIN = "Admin";

export interface CurrentUser extends MeResponse {
  isAdmin: boolean;
}

/** Loads + caches the signed-in staff identity (GET /api/me) and derives role flags. The query is
 *  the SPA's source of truth for the caller's roles; nav and pages gate on it, the API enforces it. */
export function useMe() {
  const query = useQuery({
    queryKey: ["me"],
    queryFn: getMe,
    staleTime: 5 * 60_000
  });

  const user: CurrentUser | undefined = query.data
    ? { ...query.data, isAdmin: query.data.roles.includes(ROLE_ADMIN) }
    : undefined;

  // Return explicit fields rather than spreading the query result: React Query v5's result exposes
  // a `promise` getter, and spreading it materializes a floating (uncaught) promise on error.
  return {
    user,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error
  };
}

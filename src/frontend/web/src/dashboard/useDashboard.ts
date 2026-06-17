import { useQuery } from "@tanstack/react-query";
import { getDashboard, type Dashboard } from "../api";

/** Loads the admin dashboard aggregate (GET /api/dashboard). */
export function useDashboard() {
  return useQuery<Dashboard>({ queryKey: ["dashboard"], queryFn: getDashboard });
}

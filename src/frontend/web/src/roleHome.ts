/** Post-login destination for a signed-in staff member, by role. The legacy app routed admins to the
 *  dashboard, sales/institution to the program list, and SDRs to their own dashboard. The SDR/Sales
 *  scoped dashboards don't exist in the rewrite yet, so those fall back to the admin dashboard.
 *
 *  Pure + table-driven so the branchy routing logic is unit-tested in one place. Roles are matched
 *  case-insensitively; the first matching role (in priority order) wins. */
const ROLE_ROUTES: { role: string; path: string }[] = [
  { role: "admin", path: "/admin/dashboard" },
  { role: "sales", path: "/admin/programs" },
  // SDR scoped dashboard not built yet → land on the admin dashboard for now.
  { role: "sdr", path: "/admin/dashboard" }
];

export function roleHome(roles: readonly string[] | undefined): string {
  const lower = (roles ?? []).map((r) => r.toLowerCase());
  for (const { role, path } of ROLE_ROUTES) {
    if (lower.includes(role)) return path;
  }
  // Any other staff role (e.g. Coordinator) — the dashboard is the safe default landing.
  return "/admin/dashboard";
}

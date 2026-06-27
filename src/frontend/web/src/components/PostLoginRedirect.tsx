import { Navigate } from "react-router-dom";
import { useMe } from "../useMe";
import { roleHome } from "../roleHome";

/** The /admin index (the workforce MSAL redirect target). Once the signed-in identity loads, it
 *  forwards the user to their role's home (admin → dashboard, sales → programs, …). Rendered only
 *  inside the authenticated AppLayout, so there's always a session by the time it mounts. */
export function PostLoginRedirect() {
  const { user, isLoading } = useMe();

  if (isLoading) {
    return <div className="card state">Loading your console…</div>;
  }
  return <Navigate to={roleHome(user?.roles)} replace />;
}

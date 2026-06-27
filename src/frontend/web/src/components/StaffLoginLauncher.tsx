import { useEffect, useState } from "react";
import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { Navigate } from "react-router-dom";
import { loginRequest } from "../authConfig";
import { useMe } from "../useMe";
import { roleHome } from "../roleHome";

/** Which staff entry point the user came through. The legacy site exposed distinct login URLs
 *  (/rotationsplusadmin, /rotationsplussales, /rotationsplussdr); they all use the SAME workforce
 *  tenant, so the entry only seeds a fallback destination — the actual role from /api/me wins. */
export type StaffEntry = "admin" | "sales" | "sdr";

/** Fallback landing per entry URL, used only if the signed-in roles can't be read. */
const ENTRY_HOME: Record<StaffEntry, string> = {
  admin: "/admin/dashboard",
  sales: "/admin/programs",
  sdr: "/admin/dashboard"
};

/** The staff entry routes. When unauthenticated it fires the workforce redirect (MSAL navigates the
 *  browser away to Entra). When already signed in it forwards to the caller's role home. */
export function StaffLoginLauncher({ entry }: { entry: StaffEntry }) {
  return (
    <>
      <UnauthenticatedTemplate>
        <LoginLaunch />
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <AuthedHome entry={entry} />
      </AuthenticatedTemplate>
    </>
  );
}

/** Fires loginRedirect once on mount; on failure offers a retry (MSAL otherwise leaves a dead page). */
function LoginLaunch() {
  const { instance } = useMsal();
  const [error, setError] = useState<string | null>(null);

  const start = () => {
    setError(null);
    void instance.loginRedirect(loginRequest).catch((e) => setError(String(e)));
  };

  useEffect(() => {
    start();
    // Run once: MSAL redirects the browser away, so re-runs can't happen in practice.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="signin">
      <div className="card">
        <h1>Rotations Plus</h1>
        <p>Redirecting to sign-in…</p>
        {error && (
          <>
            <p className="banner error" role="alert" style={{ marginTop: 20 }}>{error}</p>
            <button className="btn btn-primary" style={{ marginTop: 12 }} onClick={start}>Try again</button>
          </>
        )}
      </div>
    </div>
  );
}

/** Already signed in: wait for the identity to load, then forward to the role's home (so a Sales user
 *  who lands on /rotationsplusadmin still routes by role, not by entry). useMe() is called here — inside
 *  the authenticated subtree — so the unauthenticated launcher never fires a token-bearing /api/me. */
function AuthedHome({ entry }: { entry: StaffEntry }) {
  const { user, isLoading } = useMe();
  if (isLoading) return <div className="card state">Signing you in…</div>;
  return <Navigate to={user ? roleHome(user.roles) : ENTRY_HOME[entry]} replace />;
}

import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { Link, Outlet } from "react-router-dom";
import { useCustomerMe } from "./useCustomerMe";
import { CustomerSignIn } from "./CustomerSignIn";

/** The signed-in customer shell: top bar + routed content. Unauthenticated visitors get sign-in. */
export function PortalLayout() {
  const { instance } = useMsal(); // customer instance
  const { customer } = useCustomerMe();

  return (
    <>
      <UnauthenticatedTemplate>
        <CustomerSignIn />
      </UnauthenticatedTemplate>

      <AuthenticatedTemplate>
        <div className="portal">
          <header className="portal-top">
            <Link to="/portal" className="brand">
              <span className="brand-dot" />
              Rotations Plus
            </Link>
            <div className="user-chip">
              <span>{customer?.name ?? customer?.username ?? "…"}</span>
              <button className="btn btn-ghost" onClick={() => void instance.logoutRedirect()}>Sign out</button>
            </div>
          </header>
          <main className="portal-main">
            <Outlet />
          </main>
        </div>
      </AuthenticatedTemplate>
    </>
  );
}

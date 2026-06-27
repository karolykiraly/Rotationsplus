import { MsalProvider } from "@azure/msal-react";
import { Outlet } from "react-router-dom";
import { msalInstance } from "../authConfig";

/** Roots the staff branch on the WORKFORCE MSAL instance. It renders an <Outlet/> so the staff login
 *  launchers (/rotationsplusadmin|sales|sdr) and the authenticated /admin console all share this one
 *  provider. Kept as the sole MSAL provider on the staff routes so it never contends with the customer
 *  instance for a redirect hash (there is no root MsalProvider — see main.tsx). */
export function StaffMsalShell() {
  return (
    <MsalProvider instance={msalInstance}>
      <Outlet />
    </MsalProvider>
  );
}

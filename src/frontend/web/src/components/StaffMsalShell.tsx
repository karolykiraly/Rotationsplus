import { MsalProvider } from "@azure/msal-react";
import { msalInstance } from "../authConfig";
import { AppLayout } from "./AppLayout";

/** Roots the staff console on the WORKFORCE MSAL instance. Kept as the sole MSAL provider on the
 *  "/" + "/admin" routes so it never contends with the customer instance for a redirect hash. */
export function StaffMsalShell() {
  return (
    <MsalProvider instance={msalInstance}>
      <AppLayout />
    </MsalProvider>
  );
}

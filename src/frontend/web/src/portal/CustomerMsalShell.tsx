import { MsalProvider } from "@azure/msal-react";
import { customerMsalInstance } from "../authConfig";
import { PortalLayout } from "./PortalLayout";

/** Roots the /portal subtree on the CUSTOMER MSAL instance (nested inside the app's staff provider),
 *  so portal components authenticate against CIAM while the staff console stays on the workforce app. */
export function CustomerMsalShell() {
  return (
    <MsalProvider instance={customerMsalInstance}>
      <PortalLayout />
    </MsalProvider>
  );
}

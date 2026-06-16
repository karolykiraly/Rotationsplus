import React from "react";
import ReactDOM from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { EventType, type AuthenticationResult, type IPublicClientApplication } from "@azure/msal-browser";
import { customerMsalInstance, msalInstance } from "./authConfig";
import { queryClient } from "./queryClient";
import { router } from "./router";
import "./styles.css";

/** Set the active account from cache and keep it current on each successful login. Redirect
 *  responses are processed by each route's MsalProvider (StaffMsalShell / CustomerMsalShell), which
 *  emit LOGIN_SUCCESS — handled here. There is no root MsalProvider, so the two instances never
 *  contend for the same auth-response hash. */
function wireActiveAccount(instance: IPublicClientApplication) {
  const accounts = instance.getAllAccounts();
  if (accounts.length > 0) {
    instance.setActiveAccount(accounts[0]);
  }
  instance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      instance.setActiveAccount((event.payload as AuthenticationResult).account);
    }
  });
}

// MSAL v5 requires initialize() before use; do both instances before rendering.
void Promise.all([msalInstance.initialize(), customerMsalInstance.initialize()]).then(() => {
  wireActiveAccount(msalInstance);
  wireActiveAccount(customerMsalInstance);

  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </React.StrictMode>
  );
});

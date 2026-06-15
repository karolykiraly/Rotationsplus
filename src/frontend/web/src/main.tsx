import React from "react";
import ReactDOM from "react-dom/client";
import { MsalProvider } from "@azure/msal-react";
import { EventType, type AuthenticationResult } from "@azure/msal-browser";
import { msalInstance } from "./authConfig";
import App from "./App";

// MSAL v5 must be initialized before use.
void msalInstance.initialize().then(() => {
  const accounts = msalInstance.getAllAccounts();
  if (accounts.length > 0) {
    msalInstance.setActiveAccount(accounts[0]);
  }

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      msalInstance.setActiveAccount((event.payload as AuthenticationResult).account);
    }
  });

  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <App />
      </MsalProvider>
    </React.StrictMode>
  );
});

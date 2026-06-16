import { useState } from "react";
import { useMsal } from "@azure/msal-react";
import { customerLoginRequest } from "../authConfig";

/** Unauthenticated portal landing: customer (CIAM) sign-in / sign-up via the Entra External ID flow. */
export function CustomerSignIn() {
  const { instance } = useMsal(); // the customer instance (nearest provider)
  const [error, setError] = useState<string | null>(null);

  const signIn = () => {
    setError(null);
    void instance.loginRedirect(customerLoginRequest).catch((e) => setError(String(e)));
  };

  return (
    <div className="signin">
      <div className="card">
        <h1>Rotations Plus</h1>
        <p>Find and book your clinical rotation.</p>
        <button className="btn btn-primary" onClick={signIn}>Sign in / Sign up</button>
        {error && <p className="banner error" role="alert" style={{ marginTop: 20 }}>{error}</p>}
      </div>
    </div>
  );
}

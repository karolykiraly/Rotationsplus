import { useState } from "react";
import { useMsal } from "@azure/msal-react";
import { loginRequest } from "../authConfig";

/** Unauthenticated landing: brand + a single staff sign-in button (Entra workforce redirect flow). */
export function SignIn() {
  const { instance } = useMsal();
  const [error, setError] = useState<string | null>(null);

  const signIn = () => {
    setError(null);
    void instance.loginRedirect(loginRequest).catch((e) => setError(String(e)));
  };

  return (
    <div className="signin">
      <div className="card">
        <h1>Rotations Plus</h1>
        <p>Staff console — sign in with your work account.</p>
        <button className="btn btn-primary" onClick={signIn}>Sign in</button>
        {error && <p className="banner error" role="alert" style={{ marginTop: 20 }}>{error}</p>}
      </div>
    </div>
  );
}

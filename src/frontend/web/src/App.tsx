import { useState } from "react";
import { useMsal } from "@azure/msal-react";
import { loginRequest } from "./authConfig";
import { getMe, type MeResponse } from "./api";

const BRAND = "#FF4874";

export default function App() {
  const { instance, accounts } = useMsal();
  const [me, setMe] = useState<MeResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const isSignedIn = accounts.length > 0;

  const signIn = () => {
    setError(null);
    void instance.loginRedirect(loginRequest).catch((e) => setError(String(e)));
  };

  const signOut = () => {
    setMe(null);
    void instance.logoutRedirect();
  };

  const callApi = async () => {
    try {
      setError(null);
      setMe(await getMe());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  };

  return (
    <main style={{ fontFamily: "system-ui, sans-serif", maxWidth: 640, margin: "3rem auto", padding: "0 1rem" }}>
      <h1 style={{ color: BRAND }}>Rotations Plus — Staff</h1>
      <p>P1 foundation: Entra (workforce) login round-trip.</p>

      {!isSignedIn ? (
        <button onClick={signIn} style={{ background: BRAND, color: "white", border: 0, padding: "0.6rem 1.2rem", borderRadius: 6, cursor: "pointer" }}>
          Sign in
        </button>
      ) : (
        <div style={{ display: "flex", gap: "0.75rem" }}>
          <button onClick={callApi} style={{ background: BRAND, color: "white", border: 0, padding: "0.6rem 1.2rem", borderRadius: 6, cursor: "pointer" }}>
            Call /api/me
          </button>
          <button onClick={signOut} style={{ background: "transparent", color: BRAND, border: `1px solid ${BRAND}`, padding: "0.6rem 1.2rem", borderRadius: 6, cursor: "pointer" }}>
            Sign out
          </button>
        </div>
      )}

      {error && <p role="alert" style={{ color: "#b00020" }}>{error}</p>}

      {me && (
        <section style={{ marginTop: "1.5rem" }}>
          <h2>Authenticated identity</h2>
          <dl>
            <dt>Name</dt><dd>{me.name ?? "—"}</dd>
            <dt>Username</dt><dd>{me.username ?? "—"}</dd>
            <dt>Object ID</dt><dd>{me.objectId}</dd>
            <dt>Roles</dt><dd>{me.roles.length > 0 ? me.roles.join(", ") : "—"}</dd>
            <dt>Is staff</dt><dd>{String(me.isStaff)}</dd>
          </dl>
        </section>
      )}
    </main>
  );
}

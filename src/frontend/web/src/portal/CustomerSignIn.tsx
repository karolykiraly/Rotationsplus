import { useState } from "react";
import { useMsal } from "@azure/msal-react";
import { customerLoginRequest } from "../authConfig";
import logo from "../assets/images/logo.png";

/** Unauthenticated portal landing: an on-brand welcome hero with the customer (CIAM) sign-in / sign-up
 *  CTA. Credentials are entered on the Entra External ID hosted page (the combined rplus-susi flow),
 *  so this screen is a branded entry point, not an in-app credential form. */
export function CustomerSignIn() {
  const { instance } = useMsal(); // the customer instance (nearest provider)
  const [error, setError] = useState<string | null>(null);

  const signIn = async () => {
    setError(null);
    try {
      await instance.loginRedirect(customerLoginRequest);
    } catch (e) {
      setError(String(e));
    }
  };

  return (
    <div className="signin">
      <div className="signin-hero">
        <img className="signin-logo" src={logo} alt="Rotations Plus" />
        <h1 className="signin-title">Find your clinical rotation</h1>
        <p className="signin-sub">
          Browse and book clinical-rotation programs, track your rotations, and manage your deposits —
          all in one place.
        </p>
        <button className="btn btn-primary signin-cta" onClick={signIn}>Sign in / Sign up</button>
        <p className="signin-note">Students and preceptors sign in here.</p>
        {error && <p className="banner error" role="alert">{error}</p>}
      </div>
    </div>
  );
}

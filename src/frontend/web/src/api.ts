import { apiBaseUrl, loginRequest, msalInstance } from "./authConfig";

/** Mirror of the API's MeResponse contract (System.Text.Json camelCases by default). */
export interface MeResponse {
  objectId: string;
  name?: string | null;
  username?: string | null;
  roles: string[];
  isStaff: boolean;
}

/** Acquires a workforce access token and calls GET /api/me — the P1 login round-trip. */
export async function getMe(): Promise<MeResponse> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await msalInstance.acquireTokenSilent({ ...loginRequest, account });

  const response = await fetch(`${apiBaseUrl}/api/me`, {
    headers: { Authorization: `Bearer ${result.accessToken}` }
  });

  if (!response.ok) {
    throw new Error(`GET /api/me failed: ${response.status}`);
  }

  return (await response.json()) as MeResponse;
}

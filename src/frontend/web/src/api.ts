import { apiBaseUrl, loginRequest, msalInstance } from "./authConfig";

/** Mirror of the API's MeResponse contract (System.Text.Json camelCases by default). */
export interface MeResponse {
  objectId: string;
  name?: string | null;
  username?: string | null;
  roles: string[];
  isStaff: boolean;
  /** Persisted profile id (from the DB, not the token) — proves the write round-trip. */
  profileId: string;
  lastSignInAtUtc?: string | null;
}

/** Mirror of the API's SpecialtyResponse contract. */
export interface Specialty {
  id: string;
  name: string;
}

/** Mirror of the API's ProgramSummaryResponse contract (enums serialized as strings). */
export interface Program {
  id: string;
  specialtyName: string;
  programType: string;
  maxStudentsPerRotation: number;
  minWeeksPerRotation: number;
  retailAmountPerWeek: number;
}

async function getJson<T>(path: string): Promise<T> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await msalInstance.acquireTokenSilent({ ...loginRequest, account });

  const response = await fetch(`${apiBaseUrl}${path}`, {
    headers: { Authorization: `Bearer ${result.accessToken}` }
  });

  if (!response.ok) {
    throw new Error(`GET ${path} failed: ${response.status}`);
  }

  return (await response.json()) as T;
}

/** Acquires a workforce access token and calls GET /api/me — the staff login round-trip. */
export const getMe = (): Promise<MeResponse> => getJson<MeResponse>("/api/me");

/** Lists marketplace specialties (GET /api/specialties). */
export const getSpecialties = (): Promise<Specialty[]> => getJson<Specialty[]>("/api/specialties");

/** Lists marketplace programs (GET /api/programs). */
export const getPrograms = (): Promise<Program[]> => getJson<Program[]>("/api/programs");

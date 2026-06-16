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
  preceptorName?: string | null;
}

/** Mirror of the API's PreceptorSummaryResponse contract (enums serialized as strings). */
export interface Preceptor {
  id: string;
  fullName: string;
  email: string;
  primarySpecialtyName: string;
  city?: string | null;
  state?: string | null;
  status: string;
}

/** An unsuccessful API response. Carries the HTTP status so callers can branch (e.g. 409 → duplicate). */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/** Reads the most human-friendly message out of an error response (the API returns a JSON string
 *  for validation/conflict bodies, or a ProblemDetails object). Falls back to the status. */
async function errorMessage(response: Response): Promise<string> {
  try {
    const text = await response.text();
    if (!text) return `Request failed (${response.status})`;
    try {
      const parsed: unknown = JSON.parse(text);
      if (typeof parsed === "string") return parsed;
      if (parsed && typeof parsed === "object") {
        const p = parsed as Record<string, unknown>;
        return (p.detail ?? p.title ?? p.message ?? text) as string;
      }
      return text;
    } catch {
      return text;
    }
  } catch {
    return `Request failed (${response.status})`;
  }
}

/** Acquires a workforce access token and issues a JSON request to the API. Throws {@link ApiError}
 *  on a non-2xx response; returns `undefined` for 204 No Content. */
export async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await msalInstance.acquireTokenSilent({ ...loginRequest, account });

  const headers: Record<string, string> = { Authorization: `Bearer ${result.accessToken}` };
  if (body !== undefined) headers["Content-Type"] = "application/json";

  const response = await fetch(`${apiBaseUrl}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined
  });

  if (!response.ok) {
    throw new ApiError(response.status, await errorMessage(response));
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

// ---- Identity ----
/** Acquires a workforce access token and calls GET /api/me — the staff login round-trip. */
export const getMe = (): Promise<MeResponse> => request<MeResponse>("GET", "/api/me");

// ---- Specialties (read: StaffOnly; writes: AdminOnly) ----
export const getSpecialties = (): Promise<Specialty[]> => request<Specialty[]>("GET", "/api/specialties");
export const createSpecialty = (name: string): Promise<Specialty> =>
  request<Specialty>("POST", "/api/specialties", { name });
export const updateSpecialty = (id: string, name: string): Promise<Specialty> =>
  request<Specialty>("PUT", `/api/specialties/${id}`, { name });
export const deleteSpecialty = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/specialties/${id}`);

// ---- Programs / Preceptors (read) ----
export const getPrograms = (): Promise<Program[]> => request<Program[]>("GET", "/api/programs");
export const getPreceptors = (): Promise<Preceptor[]> => request<Preceptor[]>("GET", "/api/preceptors");

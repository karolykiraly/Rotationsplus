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

/** Program delivery types (mirrors the API's ProgramType enum; serialized as these string names). */
export type ProgramType =
  | "InPerson"
  | "InPersonResearch"
  | "Consultation"
  | "ConsultationSub"
  | "TeleRotation"
  | "TeleResearch"
  | "Dental";

/** Mirror of the API's ProgramSummaryResponse contract (enums serialized as strings). */
export interface Program {
  id: string;
  specialtyName: string;
  programType: ProgramType;
  maxStudentsPerRotation: number;
  minWeeksPerRotation: number;
  retailAmountPerWeek: number;
  preceptorName?: string | null;
}

/** Mirror of the API's ProgramDetailResponse contract — the editable shape. */
export interface ProgramDetail {
  id: string;
  specialtyId: string;
  specialtyName: string;
  programType: ProgramType;
  maxStudentsPerRotation: number;
  minWeeksPerRotation: number;
  retailAmountPerWeek: number;
  /** Staff-only (preceptor pay / platform margin); null for customer callers. */
  weeklyHonorarium: number | null;
  description?: string | null;
  preceptorId?: string | null;
  preceptorName?: string | null;
}

/** Admin create/update payload (mirrors Create/UpdateProgramRequest). */
export interface ProgramInput {
  specialtyId: string;
  programType: ProgramType;
  maxStudentsPerRotation: number;
  minWeeksPerRotation: number;
  retailAmountPerWeek: number;
  weeklyHonorarium: number;
  description?: string | null;
  preceptorId?: string | null;
}

/** Preceptor lifecycle statuses (mirrors the API's PreceptorStatus enum; serialized as these names). */
export type PreceptorStatus =
  | "Registered"
  | "Pending"
  | "MemberProfileCompleted"
  | "MemberActivated"
  | "MemberValidated"
  | "MemberSigned";

/** Mirror of the API's PreceptorSummaryResponse contract (enums serialized as strings). */
export interface Preceptor {
  id: string;
  fullName: string;
  email: string;
  primarySpecialtyName: string;
  city?: string | null;
  state?: string | null;
  status: PreceptorStatus;
}

/** Mirror of the API's PreceptorDetailResponse contract — the editable shape. */
export interface PreceptorDetail {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  primarySpecialtyId: string;
  primarySpecialtyName: string;
  medicalLicenseNumber?: string | null;
  licenseState?: string | null;
  city?: string | null;
  state?: string | null;
  status: PreceptorStatus;
  bio?: string | null;
}

/** Admin create/update payload (mirrors Create/UpdatePreceptorRequest). */
export interface PreceptorInput {
  firstName: string;
  lastName: string;
  email: string;
  primarySpecialtyId: string;
  medicalLicenseNumber?: string | null;
  licenseState?: string | null;
  city?: string | null;
  state?: string | null;
  status: PreceptorStatus;
  bio?: string | null;
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

/** Issues a JSON request with a caller-supplied bearer token. Throws {@link ApiError} on a non-2xx
 *  response; returns `undefined` for 204 No Content. Shared by the staff and customer token flows. */
export async function apiFetch<T>(method: string, path: string, accessToken: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Authorization: `Bearer ${accessToken}` };
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

/** Acquires a workforce (staff) access token and issues the request. */
export async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await msalInstance.acquireTokenSilent({ ...loginRequest, account });
  return apiFetch<T>(method, path, result.accessToken, body);
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

// ---- Programs (read: StaffOnly; writes: AdminOnly) ----
export const getPrograms = (): Promise<Program[]> => request<Program[]>("GET", "/api/programs");
export const getProgram = (id: string): Promise<ProgramDetail> =>
  request<ProgramDetail>("GET", `/api/programs/${id}`);
export const createProgram = (input: ProgramInput): Promise<ProgramDetail> =>
  request<ProgramDetail>("POST", "/api/programs", input);
export const updateProgram = (id: string, input: ProgramInput): Promise<ProgramDetail> =>
  request<ProgramDetail>("PUT", `/api/programs/${id}`, input);
export const deleteProgram = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/programs/${id}`);

// ---- Preceptors (read: StaffOnly; writes: AdminOnly) ----
export const getPreceptors = (): Promise<Preceptor[]> => request<Preceptor[]>("GET", "/api/preceptors");
export const getPreceptor = (id: string): Promise<PreceptorDetail> =>
  request<PreceptorDetail>("GET", `/api/preceptors/${id}`);
export const createPreceptor = (input: PreceptorInput): Promise<PreceptorDetail> =>
  request<PreceptorDetail>("POST", "/api/preceptors", input);
export const updatePreceptor = (id: string, input: PreceptorInput): Promise<PreceptorDetail> =>
  request<PreceptorDetail>("PUT", `/api/preceptors/${id}`, input);
export const deletePreceptor = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/preceptors/${id}`);

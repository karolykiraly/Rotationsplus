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

/** Rotation lifecycle statuses (mirrors the API's RotationStatus enum; serialized as these names). */
export type RotationStatus =
  | "Pending"
  | "NotStarted"
  | "Active"
  | "ToBeEvaluated"
  | "Completed"
  | "Cancelled"
  | "Refunded"
  | "Abandoned"
  | "Rejected";

/** Mirror of the API's RotationSummaryResponse contract (enums serialized as strings; dates as YYYY-MM-DD). */
export interface Rotation {
  id: string;
  studentName: string;
  studentEmail: string;
  specialtyName: string;
  programType: ProgramType;
  preceptorName?: string | null;
  startDate: string;
  endDate: string;
  weeks: number;
  status: RotationStatus;
}

/** Mirror of the API's RotationDetailResponse contract — the editable shape. The student name/email/oid
 *  are a snapshot taken at write time; `studentId` links to the directory record (null only for legacy
 *  rows) and is what the form's picker binds to. */
export interface RotationDetail {
  id: string;
  programId: string;
  specialtyName: string;
  programType: ProgramType;
  preceptorName?: string | null;
  studentId?: string | null;
  studentName: string;
  studentEmail: string;
  studentOid?: string | null;
  startDate: string;
  endDate: string;
  weeks: number;
  status: RotationStatus;
}

/** Admin create/update payload (mirrors Create/UpdateRotationRequest). The student is chosen from the
 *  directory by `studentId`; the server snapshots their identity. Weeks is derived server-side. */
export interface RotationInput {
  programId: string;
  studentId: string;
  startDate: string;
  endDate: string;
  status: RotationStatus;
}

/** Student academic track (mirrors the API's AcademicStatus enum; serialized as these names). */
export type AcademicStatus =
  | "UsPreMed"
  | "MdStudent"
  | "DoStudent"
  | "DentalStudent"
  | "InternationalMedicalStudent"
  | "InternationalMedicalGraduate"
  | "PhysicianAssistantStudent"
  | "NursePractitionerStudent";

/** Student work-authorization status (mirrors the API's VisaStatus enum; serialized as these names). */
export type VisaStatus = "CitizenOrGreenCard" | "ValidVisa" | "InterviewScheduled" | "NeedsVisaHelp";

/** Student lifecycle status (mirrors the API's StudentStatus enum; serialized as these names). */
export type StudentStatus = "Registered" | "MemberProfileCompleted" | "MemberActivated" | "TurnedIntoContact";

/** Mirror of the API's StudentSummaryResponse contract (enums serialized as strings). */
export interface Student {
  id: string;
  fullName: string;
  email: string;
  mobilePhone?: string | null;
  academicStatus: AcademicStatus;
  visaStatus?: VisaStatus | null;
  city?: string | null;
  state?: string | null;
  status: StudentStatus;
}

/** Mirror of the API's StudentDetailResponse contract — the editable shape. */
export interface StudentDetail {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  mobilePhone?: string | null;
  academicStatus: AcademicStatus;
  visaStatus?: VisaStatus | null;
  medicalSchool?: string | null;
  medicalSchoolCountry?: string | null;
  city?: string | null;
  state?: string | null;
  status: StudentStatus;
  studentOid?: string | null;
}

/** Admin create/update payload (mirrors Create/UpdateStudentRequest). */
export interface StudentInput {
  firstName: string;
  lastName: string;
  email: string;
  mobilePhone?: string | null;
  academicStatus: AcademicStatus;
  visaStatus?: VisaStatus | null;
  medicalSchool?: string | null;
  medicalSchoolCountry?: string | null;
  city?: string | null;
  state?: string | null;
  status: StudentStatus;
  studentOid?: string | null;
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

// ---- Rotations (AdminOnly — reads + writes) ----
export const getRotations = (params?: { status?: RotationStatus; programId?: string }): Promise<Rotation[]> => {
  const q = new URLSearchParams();
  if (params?.status) q.set("status", params.status);
  if (params?.programId) q.set("programId", params.programId);
  const suffix = q.toString();
  return request<Rotation[]>("GET", `/api/rotations${suffix ? `?${suffix}` : ""}`);
};
export const getRotation = (id: string): Promise<RotationDetail> =>
  request<RotationDetail>("GET", `/api/rotations/${id}`);
export const createRotation = (input: RotationInput): Promise<RotationDetail> =>
  request<RotationDetail>("POST", "/api/rotations", input);
export const updateRotation = (id: string, input: RotationInput): Promise<RotationDetail> =>
  request<RotationDetail>("PUT", `/api/rotations/${id}`, input);
export const deleteRotation = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/rotations/${id}`);

// ---- Students (read: StaffOnly; writes: AdminOnly) ----
export const getStudents = (params?: { status?: StudentStatus; academicStatus?: AcademicStatus }): Promise<Student[]> => {
  const q = new URLSearchParams();
  if (params?.status) q.set("status", params.status);
  if (params?.academicStatus) q.set("academicStatus", params.academicStatus);
  const suffix = q.toString();
  return request<Student[]>("GET", `/api/students${suffix ? `?${suffix}` : ""}`);
};
export const getStudent = (id: string): Promise<StudentDetail> =>
  request<StudentDetail>("GET", `/api/students/${id}`);
export const createStudent = (input: StudentInput): Promise<StudentDetail> =>
  request<StudentDetail>("POST", "/api/students", input);
export const updateStudent = (id: string, input: StudentInput): Promise<StudentDetail> =>
  request<StudentDetail>("PUT", `/api/students/${id}`, input);
export const deleteStudent = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/students/${id}`);

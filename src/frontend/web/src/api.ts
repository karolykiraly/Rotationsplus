import {
  BrowserAuthError,
  InteractionRequiredAuthError,
  type AccountInfo,
  type AuthenticationResult,
  type IPublicClientApplication,
  type RedirectRequest,
  type SilentRequest
} from "@azure/msal-browser";
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
  programNumber: number;
  specialtyName: string;
  programType: ProgramType;
  maxStudentsPerRotation: number;
  minWeeksPerRotation: number;
  retailAmountPerWeek: number;
  /** Staff-only (preceptor pay / platform margin); null for customer callers. The admin Programs list
   *  shows it under the "Retail Amount" column, mirroring the legacy admin screen. */
  weeklyHonorarium?: number | null;
  preceptorName?: string | null;
  city?: string | null;
  state?: string | null;
  isOpen: boolean;
  tags: string[];
  /** Short-lived read URL for the hospital image, or null/absent when none (client shows a placeholder). */
  imageUrl?: string | null;
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
  isOpen: boolean;
  programNumber: number;
  city?: string | null;
  state?: string | null;
  tags: string[];
  /** Short-lived read URL for the hospital image, or null/absent when none (client shows a placeholder). */
  imageUrl?: string | null;
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
  mobilePhone?: string | null;
  callScheduled: boolean;
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
  reviewedAtUtc?: string | null;
  rejectionReason?: string | null;
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
  rotationNumber: number;
  studentName: string;
  studentEmail: string;
  specialtyName: string;
  programType: ProgramType;
  preceptorName?: string | null;
  startDate: string;
  endDate: string;
  weeks: number;
  status: RotationStatus;
  /** Retail cost of the booking (program retail/week × weeks) — the "Retail Amount" column. */
  retailAmount: number;
  /** True when the booked student needs visa help — drives the "Needs Visa" checkbox. */
  needsVisa: boolean;
}

/** Mirror of the API's RotationDetailResponse contract — the editable shape. The student name/email/oid
 *  are a snapshot taken at write time; `studentId` links to the directory record (null only for legacy
 *  rows) and is what the form's picker binds to. */
export interface RotationDetail {
  id: string;
  rotationNumber: number;
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
  /** The program's sequential number — the "Program ID" shown in the Selected Rotation panel. */
  programNumber: number;
  /** The booking's retail cost (program retail/week × weeks) — the panel's "Rotation Cost". */
  retailAmount: number;
  /** Sum of the rotation's captured (Succeeded) payments — the panel's "Paid Amount". */
  paidAmount: number;
  /** Statuses this rotation may transition to (excludes the current one); the edit form offers the
   *  current status plus these, and the server enforces the same lifecycle state machine. */
  allowedNextStatuses: RotationStatus[];
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

/** Mirror of the API's RotationStatusCount contract. */
export interface RotationStatusCount {
  status: RotationStatus;
  count: number;
}

/** Mirror of the API's UpcomingRotation contract. */
export interface UpcomingRotation {
  id: string;
  studentName: string;
  specialtyName: string;
  startDate: string;
  status: RotationStatus;
}

/** Mirror of the API's ProgramTypeCount contract — how many programs are of a given delivery type. */
export interface ProgramTypeCount {
  type: ProgramType;
  count: number;
}

/** Mirror of the API's TodayMetrics contract — the "Today's LiveScore" movement (business day). */
export interface TodayMetrics {
  newPrograms: number;
  newProgramsByType: ProgramTypeCount[];
  newStudents: number;
  newPreceptors: number;
  issuesReported: number;
  rotationsStarting: number;
  rotationsInProgress: number;
  rotationsCompleting: number;
  rotationsCancelled: number;
}

/** Mirror of the API's DashboardResponse contract — the admin hub aggregate. */
export interface Dashboard {
  students: number;
  programs: number;
  preceptors: number;
  specialties: number;
  rotations: number;
  programsByType: ProgramTypeCount[];
  rotationsByStatus: RotationStatusCount[];
  upcomingStarts: UpcomingRotation[];
  today: TodayMetrics;
}

/** Mirror of the API's TodoBucket<T> — a work queue: full outstanding count + a capped preview. */
export interface TodoBucket<T> {
  count: number;
  items: T[];
}

/** A document submitted by a student and awaiting admin review. */
export interface DocumentTodoItem {
  documentId: string;
  rotationId: string;
  rotationNumber: number;
  studentId?: string | null;
  studentName: string;
  documentTypeName: string;
  dueDate: string;
  submittedAtUtc?: string | null;
}

/** A booked rotation whose deposit hasn't been received yet (Pending). */
export interface PaymentTodoItem {
  rotationId: string;
  rotationNumber: number;
  studentName: string;
  specialtyName: string;
  startDate: string;
}

/** A preceptor awaiting admin approval. */
export interface PreceptorTodoItem {
  preceptorId: string;
  fullName: string;
  specialtyName: string;
  email: string;
  createdAtUtc: string;
}

/** Mirror of the API's DashboardTodosResponse — the admin hub's "ToDo's" tab queues. */
export interface DashboardTodos {
  documentsToReview: TodoBucket<DocumentTodoItem>;
  awaitingPayment: TodoBucket<PaymentTodoItem>;
  preceptorApprovals: TodoBucket<PreceptorTodoItem>;
}

/** Collected revenue for one program delivery type. */
export interface RevenueByType {
  type: ProgramType;
  amount: number;
}

/** Collected revenue within one business month, for the trend series. */
export interface RevenueByMonth {
  year: number;
  month: number;
  amount: number;
}

/** Mirror of the API's DashboardRevenueResponse — the admin hub's "Revenue" tab. All figures are
 *  platform revenue (deposits captured); a refund nets out of `collected` and is shown via `refunded`. */
export interface DashboardRevenue {
  currency: string;
  collected: number;
  refunded: number;
  outstandingReceivable: number;
  collectedThisMonth: number;
  byProgramType: RevenueByType[];
  monthlyTrend: RevenueByMonth[];
}

/** New students + preceptors who registered within one business month. */
export interface RegistrationsByMonth {
  year: number;
  month: number;
  students: number;
  preceptors: number;
}

/** How many rotations belong to a given specialty (busiest first). */
export interface RotationsBySpecialty {
  specialtyName: string;
  rotationCount: number;
}

/** Mirror of the API's DashboardReportsResponse — the admin hub's "Reports" tab. */
export interface DashboardReports {
  totalStudents: number;
  studentsWithBooking: number;
  totalRotations: number;
  registrations: RegistrationsByMonth[];
  topSpecialties: RotationsBySpecialty[];
}

/** Mirror of the API's RotationQuoteResponse — the server-computed price for a booking of N weeks.
 *  `depositAmount` is due now; `outstandingAmount` is billed later; `depositPercent` is 0.10 (or 1.00 for
 *  an open/instant-approval program). Pricing is server-authoritative — never recompute it client-side. */
export interface RotationQuote {
  programId: string;
  weeks: number;
  currency: string;
  retailAmountPerWeek: number;
  totalAmount: number;
  depositAmount: number;
  outstandingAmount: number;
  depositPercent: number;
  isOpen: boolean;
}

/** Payment lifecycle status (mirrors the API's PaymentStatus enum; serialized as these names). */
export type PaymentStatus = "Pending" | "Succeeded" | "Failed" | "Refunded";

/** Mirror of the API's PaymentIntentResponse — the opened deposit and the client secret the SPA confirms
 *  the card against. `amount` is the deposit due now; `totalAmount`/`outstandingAmount` are the full price
 *  and the remainder billed later. All amounts are major units (dollars), already rounded to the cent. */
export interface PaymentIntentResponse {
  paymentId: string;
  clientSecret: string;
  amount: number;
  totalAmount: number;
  outstandingAmount: number;
  currency: string;
  status: PaymentStatus;
}

/** Mirror of the API's DEV-only PaymentSimulationResponse — the payment's status after a simulated outcome. */
export interface PaymentSimulationResponse {
  paymentId: string;
  status: PaymentStatus;
}

/** Mirror of the API's RefundResponse — the rotation's new status after a refund + how many payments were refunded. */
export interface RefundResult {
  rotationId: string;
  status: RotationStatus;
  paymentsRefunded: number;
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

/** Issues a multipart/form-data upload with a caller-supplied bearer token. The browser sets the
 *  multipart Content-Type (with boundary) from the FormData, so we must NOT set it ourselves. */
export async function apiUpload<T>(method: string, path: string, accessToken: string, form: FormData): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method,
    headers: { Authorization: `Bearer ${accessToken}` },
    body: form
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

  const result = await acquireTokenOrRedirect(msalInstance, loginRequest, account);
  return apiFetch<T>(method, path, result.accessToken, body);
}

/** Like {@link request} but for a multipart upload with the staff (workforce) token. */
export async function requestUpload<T>(method: string, path: string, form: FormData): Promise<T> {
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await acquireTokenOrRedirect(msalInstance, loginRequest, account);
  return apiUpload<T>(method, path, result.accessToken, form);
}

/** True when a failed silent token acquisition can only be resolved by interactive sign-in — i.e. a
 *  full-page redirect. Besides the explicit `InteractionRequiredAuthError`, the *common* expired-session
 *  path in msal-browser v5 is a hidden-iframe renewal that fails with a `BrowserAuthError` (timeout, empty
 *  hash, blocked third-party cookies, …). We treat any such `BrowserAuthError` as redirectable EXCEPT
 *  `interaction_in_progress`, which means another request already kicked off the redirect. */
function needsInteractiveRedirect(e: unknown): boolean {
  if (e instanceof InteractionRequiredAuthError) return true;
  return e instanceof BrowserAuthError && e.errorCode !== "interaction_in_progress";
}

/** Acquires an access token silently for the given account; on an expired/renewal-failed session, falls
 *  back to a full redirect to re-authenticate rather than surfacing a raw MSAL error. The redirect
 *  navigates away, so this never returns in that path — it throws a friendly sentinel so the caller's
 *  request doesn't proceed with an undefined token. Shared by the staff and customer token flows. */
export async function acquireTokenOrRedirect(
  instance: IPublicClientApplication,
  loginParams: SilentRequest & RedirectRequest,
  account: AccountInfo
): Promise<AuthenticationResult> {
  try {
    return await instance.acquireTokenSilent({ ...loginParams, account });
  } catch (e) {
    // A redirect is already in flight (concurrent failing requests) — the page is navigating away.
    if (e instanceof BrowserAuthError && e.errorCode === "interaction_in_progress") {
      throw new Error("Redirecting to sign in…");
    }
    if (needsInteractiveRedirect(e)) {
      await instance.acquireTokenRedirect({ ...loginParams, account });
      throw new Error("Redirecting to sign in…");
    }
    throw e;
  }
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

// ---- Programs (read: MarketplaceViewer; writes: AdminOnly) ----
// Paged admin list — program-type tabs (multi) + name search over specialty/preceptor.
/** FilterProgram modal selections (all optional; AND-combined server-side). */
export interface ProgramFilter {
  specialtyId?: string;
  /** The "City, State" string the location dropdown lists. */
  city?: string;
  /** Instant Approval: true (Yes) / false (No) / undefined (Both). */
  instantApproval?: boolean;
  honorariumMin?: number;
  honorariumMax?: number;
  programNumber?: number;
  tags?: string[];
}

export const getPrograms = (params?: {
  programType?: ProgramType[];
  q?: string;
  page?: number;
  pageSize?: number;
} & ProgramFilter): Promise<PagedResponse<Program>> => {
  const sp = new URLSearchParams();
  for (const t of params?.programType ?? []) sp.append("programType", t);
  if (params?.q) sp.set("q", params.q);
  if (params?.specialtyId) sp.set("specialtyId", params.specialtyId);
  if (params?.city) sp.set("city", params.city);
  if (params?.instantApproval !== undefined) sp.set("instantApproval", String(params.instantApproval));
  if (params?.honorariumMin !== undefined) sp.set("honorariumMin", String(params.honorariumMin));
  if (params?.honorariumMax !== undefined) sp.set("honorariumMax", String(params.honorariumMax));
  if (params?.programNumber !== undefined) sp.set("programNumber", String(params.programNumber));
  for (const tag of params?.tags ?? []) sp.append("tags", tag);
  if (params?.page) sp.set("page", String(params.page));
  if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
  const suffix = sp.toString();
  return request<PagedResponse<Program>>("GET", `/api/programs${suffix ? `?${suffix}` : ""}`);
};
/** Full program catalog (unpaginated) — the rotation form's program picker needs every option. */
export const getProgramCatalog = (): Promise<Program[]> =>
  request<Program[]>("GET", "/api/programs/catalog");
export const getProgram = (id: string): Promise<ProgramDetail> =>
  request<ProgramDetail>("GET", `/api/programs/${id}`);
export const createProgram = (input: ProgramInput): Promise<ProgramDetail> =>
  request<ProgramDetail>("POST", "/api/programs", input);
export const updateProgram = (id: string, input: ProgramInput): Promise<ProgramDetail> =>
  request<ProgramDetail>("PUT", `/api/programs/${id}`, input);
export const deleteProgram = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/programs/${id}`);

// ---- Preceptors (read: StaffOnly; writes: AdminOnly) ----
export const getPreceptors = (params?: {
  status?: PreceptorStatus;
  q?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResponse<Preceptor>> => {
  const sp = new URLSearchParams();
  if (params?.status) sp.set("status", params.status);
  if (params?.q) sp.set("q", params.q);
  if (params?.page) sp.set("page", String(params.page));
  if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
  const suffix = sp.toString();
  return request<PagedResponse<Preceptor>>("GET", `/api/preceptors${suffix ? `?${suffix}` : ""}`);
};
/** Unpaginated preceptor list for form pickers (the program form's preceptor dropdown needs every option). */
export const getPreceptorOptions = (): Promise<Preceptor[]> =>
  request<Preceptor[]>("GET", "/api/preceptors/options");
export const getPreceptor = (id: string): Promise<PreceptorDetail> =>
  request<PreceptorDetail>("GET", `/api/preceptors/${id}`);
export const createPreceptor = (input: PreceptorInput): Promise<PreceptorDetail> =>
  request<PreceptorDetail>("POST", "/api/preceptors", input);
export const updatePreceptor = (id: string, input: PreceptorInput): Promise<PreceptorDetail> =>
  request<PreceptorDetail>("PUT", `/api/preceptors/${id}`, input);
export const deletePreceptor = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/preceptors/${id}`);
/** Result of a Permission save (mirror of SavePreceptorPermissionsResponse). */
export interface SavePermissionsResult {
  activated: number;
  rejected: number;
}
/** Permission screen batch save: activate the checked preceptors, reject the others (Pending only). */
export const savePreceptorPermissions = (
  activateIds: string[],
  rejectIds: string[]
): Promise<SavePermissionsResult> =>
  request<SavePermissionsResult>("POST", "/api/preceptors/permissions", { activateIds, rejectIds });

/** One page of a server-paginated list (mirror of the API's PagedResponse<T>). `totalCount` is the full
 *  filtered row count across all pages, for the pager. */
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

// ---- Rotations (AdminOnly — reads + writes) ----
/** FilterRotation modal selections (all optional; AND-combined server-side). */
export interface RotationFilter {
  status?: RotationStatus;
  /** start_date >= startFrom (YYYY-MM-DD). */
  startFrom?: string;
  /** end_date <= endTo (YYYY-MM-DD). */
  endTo?: string;
  retailMin?: number;
  retailMax?: number;
  /** Only rotations whose student needs visa help (the "Needs Visa" checkbox). */
  needsVisa?: boolean;
  rotationNumber?: number;
}

export const getRotations = (params?: {
  /** "current" (non-terminal lifecycle) or "historical" (terminal) — the two admin sections. */
  scope?: "current" | "historical";
  programId?: string;
  q?: string;
  page?: number;
  pageSize?: number;
} & RotationFilter): Promise<PagedResponse<Rotation>> => {
  const sp = new URLSearchParams();
  if (params?.status) sp.set("status", params.status);
  if (params?.scope) sp.set("scope", params.scope);
  if (params?.programId) sp.set("programId", params.programId);
  if (params?.q) sp.set("q", params.q);
  if (params?.startFrom) sp.set("startFrom", params.startFrom);
  if (params?.endTo) sp.set("endTo", params.endTo);
  if (params?.retailMin !== undefined) sp.set("retailMin", String(params.retailMin));
  if (params?.retailMax !== undefined) sp.set("retailMax", String(params.retailMax));
  if (params?.needsVisa) sp.set("needsVisa", "true");
  if (params?.rotationNumber !== undefined) sp.set("rotationNumber", String(params.rotationNumber));
  if (params?.page) sp.set("page", String(params.page));
  if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
  const suffix = sp.toString();
  return request<PagedResponse<Rotation>>("GET", `/api/rotations${suffix ? `?${suffix}` : ""}`);
};
export const getRotation = (id: string): Promise<RotationDetail> =>
  request<RotationDetail>("GET", `/api/rotations/${id}`);
export const createRotation = (input: RotationInput): Promise<RotationDetail> =>
  request<RotationDetail>("POST", "/api/rotations", input);
export const updateRotation = (id: string, input: RotationInput): Promise<RotationDetail> =>
  request<RotationDetail>("PUT", `/api/rotations/${id}`, input);
export const deleteRotation = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/rotations/${id}`);
/** Refunds a rotation's captured payments and moves it to Refunded (admin; rotation must be
 *  Cancelled/Completed with a captured payment). */
export const refundRotation = (id: string): Promise<RefundResult> =>
  request<RefundResult>("POST", `/api/rotations/${id}/refund`);

// ---- Dashboard (AdminOnly) ----
export const getDashboard = (): Promise<Dashboard> => request<Dashboard>("GET", "/api/dashboard");
export const getDashboardTodos = (): Promise<DashboardTodos> =>
  request<DashboardTodos>("GET", "/api/dashboard/todos");
export const getDashboardRevenue = (): Promise<DashboardRevenue> =>
  request<DashboardRevenue>("GET", "/api/dashboard/revenue");
export const getDashboardReports = (): Promise<DashboardReports> =>
  request<DashboardReports>("GET", "/api/dashboard/reports");

// ---- Email campaigns (AdminOnly) ----
/** Mirror of the API's EmailAudience enum. */
export type EmailAudience = "AllStudents" | "StudentsWithBooking" | "StudentsWithoutBooking" | "AllPreceptors";

/** Mirror of the API's CampaignStatus enum. */
export type CampaignStatus = "Draft" | "Queued" | "Sending" | "Sent" | "Failed";

/** Mirror of the API's CampaignSummaryResponse (list row — no body). */
export interface CampaignSummary {
  id: string;
  subject: string;
  audience: EmailAudience;
  status: CampaignStatus;
  recipientCount: number;
  sentCount: number;
  failedCount: number;
  createdAtUtc: string;
  sentAtUtc?: string | null;
}

/** Mirror of the API's CampaignDetailResponse (with body). */
export interface CampaignDetail extends CampaignSummary {
  body: string;
}

export const getCampaigns = (params?: {
  page?: number;
  pageSize?: number;
}): Promise<PagedResponse<CampaignSummary>> => {
  const sp = new URLSearchParams();
  if (params?.page) sp.set("page", String(params.page));
  if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
  const suffix = sp.toString();
  return request<PagedResponse<CampaignSummary>>("GET", `/api/campaigns${suffix ? `?${suffix}` : ""}`);
};

/** Composes a campaign as a draft. */
export const createCampaign = (subject: string, body: string, audience: EmailAudience): Promise<CampaignDetail> =>
  request<CampaignDetail>("POST", "/api/campaigns", { subject, body, audience });

/** Queues a draft campaign for sending (the Worker fans out + tallies). */
export const sendCampaign = (id: string): Promise<CampaignDetail> =>
  request<CampaignDetail>("POST", `/api/campaigns/${id}/send`);

// ---- Honorarium (preceptor payouts — AdminOnly) ----
/** Mirror of the API's HonorariumStage enum (the three payout-screen tabs). */
export type HonorariumStage = "Deposit" | "Start" | "Evaluation";

/** Mirror of the API's HonorariumStatus enum. */
export type HonorariumStatus = "Pending" | "Paid" | "Cancelled";

/** Mirror of the API's HonorariumResponse (one payout row). */
export interface Honorarium {
  id: string;
  rotationId: string;
  rotationNumber: number;
  preceptorId?: string | null;
  preceptorName: string;
  studentName: string;
  stage: HonorariumStage;
  amount: number;
  currency: string;
  status: HonorariumStatus;
  refunded: boolean;
  rotationStartDate: string;
  /** Evaluation-tab due date (rotation end date + 7-day grace); null on rows generated before the column existed. */
  evaluationDueDate?: string | null;
  paidAtUtc?: string | null;
}

export const getHonorariums = (params?: {
  stage?: HonorariumStage;
  status?: HonorariumStatus;
  q?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResponse<Honorarium>> => {
  const sp = new URLSearchParams();
  if (params?.stage) sp.set("stage", params.stage);
  if (params?.status) sp.set("status", params.status);
  if (params?.q) sp.set("q", params.q);
  if (params?.page) sp.set("page", String(params.page));
  if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
  const suffix = sp.toString();
  return request<PagedResponse<Honorarium>>("GET", `/api/honorariums${suffix ? `?${suffix}` : ""}`);
};

/** Marks a honorarium stage paid (bookkeeping; stages must be paid in order — the server gates this). */
export const payHonorarium = (id: string): Promise<Honorarium> =>
  request<Honorarium>("POST", `/api/honorariums/${id}/pay`);

/** Toggles the independent "refunded" bookkeeping flag on a honorarium row. */
export const setHonorariumRefund = (id: string, refunded: boolean): Promise<Honorarium> =>
  request<Honorarium>("POST", `/api/honorariums/${id}/refund-flag`, { refunded });

/** Removes an erroneously-generated payout row (soft-delete). Server refuses (409) if it is already Paid. */
export const deleteHonorarium = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/honorariums/${id}`);

// ---- Students (read: StaffOnly; writes: AdminOnly) ----
export const getStudents = (params?: {
  status?: StudentStatus;
  academicStatus?: AcademicStatus;
  q?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedResponse<Student>> => {
  const sp = new URLSearchParams();
  if (params?.status) sp.set("status", params.status);
  if (params?.academicStatus) sp.set("academicStatus", params.academicStatus);
  if (params?.q) sp.set("q", params.q);
  if (params?.page) sp.set("page", String(params.page));
  if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
  const suffix = sp.toString();
  return request<PagedResponse<Student>>("GET", `/api/students${suffix ? `?${suffix}` : ""}`);
};
/** Unpaginated student list for form pickers (the rotation form's student dropdown needs every option). */
export const getStudentOptions = (): Promise<Student[]> =>
  request<Student[]>("GET", "/api/students/options");
export const getStudent = (id: string): Promise<StudentDetail> =>
  request<StudentDetail>("GET", `/api/students/${id}`);
export const createStudent = (input: StudentInput): Promise<StudentDetail> =>
  request<StudentDetail>("POST", "/api/students", input);
export const updateStudent = (id: string, input: StudentInput): Promise<StudentDetail> =>
  request<StudentDetail>("PUT", `/api/students/${id}`, input);
export const deleteStudent = (id: string): Promise<void> =>
  request<void>("DELETE", `/api/students/${id}`);

// ---- Documents (admin: catalog, per-program required-docs config) ----
/** Coarse grouping of a document type (mirrors the API's DocumentCategory enum). */
export type DocumentCategory =
  | "Immunization" | "Identity" | "Insurance" | "Certification"
  | "Professional" | "MedicalTest" | "Agreement" | "Other";

/** Mirror of the API's DocumentTypeResponse — a catalog entry. */
export interface DocumentType {
  id: string;
  name: string;
  category: DocumentCategory;
}

/** Mirror of the API's ProgramRequiredDocumentsResponse — a program's config + the full catalog. */
export interface ProgramRequiredDocuments {
  documentDueDays: number;
  requiredDocumentTypeIds: string[];
  catalog: DocumentType[];
}

export const getDocumentTypes = (): Promise<DocumentType[]> =>
  request<DocumentType[]>("GET", "/api/document-types");

/** Adds a custom document type to the catalog (admin). */
export const createDocumentType = (name: string, category: DocumentCategory): Promise<DocumentType> =>
  request<DocumentType>("POST", "/api/document-types", { name, category });

export const getProgramRequiredDocuments = (programId: string): Promise<ProgramRequiredDocuments> =>
  request<ProgramRequiredDocuments>("GET", `/api/programs/${programId}/required-documents`);

/** Sets a program's required document types + due-days (full replace). */
export const setProgramRequiredDocuments = (
  programId: string,
  documentDueDays: number,
  requiredDocumentTypeIds: string[]
): Promise<ProgramRequiredDocuments> =>
  request<ProgramRequiredDocuments>("PUT", `/api/programs/${programId}/required-documents`, {
    documentDueDays,
    requiredDocumentTypeIds
  });

// ---- Documents (admin: per-student review) ----
/** Mirror of the API's DocumentStatus enum (serialized as these string names). */
export type DocumentStatus = "UploadNeeded" | "Submitted" | "Approved" | "Rejected" | "Expired";

/** Mirror of the API's AdminRotationDocumentResponse — a student's document with rotation context
 *  (number, for filtering) plus the upload/review metadata shown on the admin review screen. */
export interface AdminRotationDocument {
  id: string;
  rotationId: string;
  rotationNumber: number;
  documentTypeName: string;
  category: DocumentCategory;
  status: DocumentStatus;
  dueDate: string;
  fileName?: string | null;
  fileUrl?: string | null;
  uploadedAtUtc?: string | null;
  reviewedAtUtc?: string | null;
  rejectionReason?: string | null;
}

/** Lists every document across a student's rotations (admin review screen). */
export const getStudentDocuments = (studentId: string): Promise<AdminRotationDocument[]> =>
  request<AdminRotationDocument[]>("GET", `/api/students/${studentId}/documents`);

/** Sets a document's lifecycle status (the review dropdown). A reason is expected on Rejected. */
export const setDocumentStatus = (
  documentId: string,
  status: DocumentStatus,
  rejectionReason: string | null
): Promise<AdminRotationDocument> =>
  request<AdminRotationDocument>("PUT", `/api/documents/${documentId}/status`, { status, rejectionReason });

/** Uploads a file on the student's behalf (multipart); the document moves to Submitted. */
export const uploadDocumentFile = (documentId: string, file: File): Promise<AdminRotationDocument> => {
  const form = new FormData();
  form.append("file", file);
  return requestUpload<AdminRotationDocument>("POST", `/api/documents/${documentId}/file`, form);
};

/** Clears a document's file (→ UploadNeeded). */
export const clearDocumentFile = (documentId: string): Promise<AdminRotationDocument> =>
  request<AdminRotationDocument>("DELETE", `/api/documents/${documentId}/file`);

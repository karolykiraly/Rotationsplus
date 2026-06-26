import { customerLoginRequest, customerMsalInstance } from "../authConfig";
import {
  acquireTokenOrRedirect,
  apiFetch,
  apiUpload,
  type PaymentIntentResponse,
  type PaymentSimulationResponse,
  type Program,
  type ProgramDetail,
  type ProgramType,
  type RotationQuote,
  type RotationStatus,
  type Specialty
} from "../api";

/** Mirror of the API's DocumentStatus enum (serialized as these string names). */
export type DocumentStatus = "UploadNeeded" | "Submitted" | "Approved" | "Rejected" | "Expired";

/** Mirror of the API's DocumentCategory enum. */
export type DocumentCategory =
  | "Immunization"
  | "Identity"
  | "Insurance"
  | "Certification"
  | "Professional"
  | "MedicalTest"
  | "Agreement"
  | "Other";

/** Mirror of the API's RotationDocumentsState — the "Documents" tracker column value. */
export type RotationDocumentsState = "NotRequired" | "Missing" | "Complete";

/** Mirror of the API's RotationDocumentResponse — one row of a rotation's document checklist. */
export interface RotationDocument {
  id: string;
  documentTypeName: string;
  category: DocumentCategory;
  status: DocumentStatus;
  dueDate: string;
  fileName?: string | null;
  fileUrl?: string | null;
  submittedAtUtc?: string | null;
  rejectionReason?: string | null;
}

/** Mirror of the API's CustomerMeResponse contract. */
export interface CustomerMe {
  objectId: string;
  name?: string | null;
  username?: string | null;
  roles: string[];
  isStudent: boolean;
  isPreceptor: boolean;
}

/** Mirror of the API's CustomerRotationResponse contract — the student's own rotation, as tracked. */
export interface CustomerRotation {
  id: string;
  rotationNumber: number;
  specialtyName: string;
  programType: ProgramType;
  preceptorName?: string | null;
  startDate: string;
  endDate: string;
  weeks: number;
  status: RotationStatus;
  /** The "Documents" tracker column — NotRequired / Missing / Complete. */
  documentsState: RotationDocumentsState;
}

/** Acquires a CIAM (customer) access token and issues the request. Mirrors the staff `request`,
 *  but against the customer MSAL instance + the access_as_customer scope. */
async function customerRequest<T>(method: string, path: string, body?: unknown): Promise<T> {
  const account = customerMsalInstance.getActiveAccount() ?? customerMsalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await acquireTokenOrRedirect(customerMsalInstance, customerLoginRequest, account);
  return apiFetch<T>(method, path, result.accessToken, body);
}

/** Customer multipart upload (acquires the CIAM token, then POSTs FormData). */
async function customerUpload<T>(method: string, path: string, form: FormData): Promise<T> {
  const account = customerMsalInstance.getActiveAccount() ?? customerMsalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await acquireTokenOrRedirect(customerMsalInstance, customerLoginRequest, account);
  return apiUpload<T>(method, path, result.accessToken, form);
}

/** The signed-in customer's identity (GET /api/customer/me). */
export const getCustomerMe = (): Promise<CustomerMe> => customerRequest<CustomerMe>("GET", "/api/customer/me");

/** Browse the program catalog with the given query string (e.g. "?specialtyId=…&q=…"). The catalog
 *  endpoint returns the full filtered list (the admin list at /api/programs is paginated). */
export const browsePrograms = (queryString = ""): Promise<Program[]> =>
  customerRequest<Program[]>("GET", `/api/programs/catalog${queryString}`);

/** A single program's detail (honorarium comes back null for customers). */
export const getCustomerProgram = (id: string): Promise<ProgramDetail> =>
  customerRequest<ProgramDetail>("GET", `/api/programs/${id}`);

/** Specialties, for the browse filter dropdown. */
export const getCustomerSpecialties = (): Promise<Specialty[]> =>
  customerRequest<Specialty[]>("GET", "/api/specialties");

/** The signed-in student's own rotations (GET /api/customer/rotations). */
export const getCustomerRotations = (): Promise<CustomerRotation[]> =>
  customerRequest<CustomerRotation[]>("GET", "/api/customer/rotations");

/** Server-computed price for booking a program for N weeks (GET /api/programs/{id}/quote?weeks=N). */
export const getProgramQuote = (programId: string, weeks: number): Promise<RotationQuote> =>
  customerRequest<RotationQuote>("GET", `/api/programs/${programId}/quote?weeks=${weeks}`);

/** Books a rotation for the signed-in student (POST /api/customer/rotations) — created Pending; the
 *  deposit is paid from the rotations tracker afterwards. */
export const bookRotation = (programId: string, startDate: string, weeks: number): Promise<CustomerRotation> =>
  customerRequest<CustomerRotation>("POST", "/api/customer/rotations", { programId, startDate, weeks });

/** Opens (or re-offers) the deposit for the student's own rotation, returning the intent + amount
 *  breakdown. Idempotent server-side: a second call on a pending deposit returns the same intent. */
export const openDepositIntent = (rotationId: string): Promise<PaymentIntentResponse> =>
  customerRequest<PaymentIntentResponse>("POST", `/api/rotations/${rotationId}/payment-intent`);

/** DEV/test only: drive the fake gateway to a terminal outcome (the Stripe-CLI analog), so the deposit
 *  round-trip completes from the browser without real Stripe.js. Absent on PREPROD/PROD. */
export const simulateDeposit = (
  paymentId: string,
  outcome: "succeeded" | "failed"
): Promise<PaymentSimulationResponse> =>
  customerRequest<PaymentSimulationResponse>("POST", `/api/dev/payments/${paymentId}/simulate`, { outcome });

/** The signed-in student's document checklist for one of their rotations. */
export const getRotationDocuments = (rotationId: string): Promise<RotationDocument[]> =>
  customerRequest<RotationDocument[]>("GET", `/api/customer/rotations/${rotationId}/documents`);

/** Uploads a file for a rotation document (multipart); the document moves to Submitted. */
export const uploadRotationDocument = (
  rotationId: string,
  documentId: string,
  file: File
): Promise<RotationDocument> => {
  const form = new FormData();
  form.append("file", file);
  return customerUpload<RotationDocument>(
    "POST",
    `/api/customer/rotations/${rotationId}/documents/${documentId}/file`,
    form
  );
};

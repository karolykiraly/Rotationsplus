import { customerLoginRequest, customerMsalInstance } from "../authConfig";
import {
  acquireTokenOrRedirect,
  apiFetch,
  type PaymentIntentResponse,
  type PaymentSimulationResponse,
  type Program,
  type ProgramDetail,
  type ProgramType,
  type RotationStatus,
  type Specialty
} from "../api";

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
  specialtyName: string;
  programType: ProgramType;
  preceptorName?: string | null;
  startDate: string;
  endDate: string;
  weeks: number;
  status: RotationStatus;
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

/** The signed-in customer's identity (GET /api/customer/me). */
export const getCustomerMe = (): Promise<CustomerMe> => customerRequest<CustomerMe>("GET", "/api/customer/me");

/** Browse the program catalog with the given query string (e.g. "?specialtyId=…&q=…"). */
export const browsePrograms = (queryString = ""): Promise<Program[]> =>
  customerRequest<Program[]>("GET", `/api/programs${queryString}`);

/** A single program's detail (honorarium comes back null for customers). */
export const getCustomerProgram = (id: string): Promise<ProgramDetail> =>
  customerRequest<ProgramDetail>("GET", `/api/programs/${id}`);

/** Specialties, for the browse filter dropdown. */
export const getCustomerSpecialties = (): Promise<Specialty[]> =>
  customerRequest<Specialty[]>("GET", "/api/specialties");

/** The signed-in student's own rotations (GET /api/customer/rotations). */
export const getCustomerRotations = (): Promise<CustomerRotation[]> =>
  customerRequest<CustomerRotation[]>("GET", "/api/customer/rotations");

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

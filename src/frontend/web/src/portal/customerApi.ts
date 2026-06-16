import { customerLoginRequest, customerMsalInstance } from "../authConfig";
import { apiFetch, type Program, type ProgramDetail, type Specialty } from "../api";

/** Mirror of the API's CustomerMeResponse contract. */
export interface CustomerMe {
  objectId: string;
  name?: string | null;
  username?: string | null;
  roles: string[];
  isStudent: boolean;
  isPreceptor: boolean;
}

/** Acquires a CIAM (customer) access token and issues the request. Mirrors the staff `request`,
 *  but against the customer MSAL instance + the access_as_customer scope. */
async function customerRequest<T>(method: string, path: string, body?: unknown): Promise<T> {
  const account = customerMsalInstance.getActiveAccount() ?? customerMsalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in");
  }

  const result = await customerMsalInstance.acquireTokenSilent({ ...customerLoginRequest, account });
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

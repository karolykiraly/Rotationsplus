import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { type Program, type Specialty } from "../api";
import { browsePrograms, getCustomerSpecialties } from "./customerApi";

/** Programs matching the given query string (e.g. "?specialtyId=…&q=…"). keepPreviousData holds the
 *  last results visible while a new filter/keystroke fetches, so the grid doesn't flash to a spinner. */
export function useCustomerPrograms(queryString: string) {
  return useQuery<Program[]>({
    queryKey: ["portal-programs", queryString],
    queryFn: () => browsePrograms(queryString),
    placeholderData: keepPreviousData
  });
}

/** Specialties for the browse filter dropdown. */
export function usePortalSpecialties() {
  return useQuery<Specialty[]>({ queryKey: ["portal-specialties"], queryFn: getCustomerSpecialties });
}

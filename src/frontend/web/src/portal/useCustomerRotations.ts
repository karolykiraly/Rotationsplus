import { useQuery } from "@tanstack/react-query";
import { getCustomerRotations, type CustomerRotation } from "./customerApi";

/** Loads the signed-in student's own rotations (GET /api/customer/rotations). */
export function useCustomerRotations() {
  return useQuery<CustomerRotation[]>({ queryKey: ["customer-rotations"], queryFn: getCustomerRotations });
}

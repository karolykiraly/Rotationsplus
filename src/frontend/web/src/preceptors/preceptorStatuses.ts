import type { PreceptorStatus } from "../api";

/** Display labels for the PreceptorStatus enum, in lifecycle order (used in the status dropdown). */
export const PRECEPTOR_STATUSES: { value: PreceptorStatus; label: string }[] = [
  { value: "Registered", label: "Registered" },
  { value: "Pending", label: "Pending" },
  { value: "MemberProfileCompleted", label: "Profile completed" },
  { value: "MemberActivated", label: "Activated" },
  { value: "MemberValidated", label: "Validated" },
  { value: "MemberSigned", label: "Signed" }
];

const LABELS: Record<PreceptorStatus, string> = Object.fromEntries(
  PRECEPTOR_STATUSES.map((s) => [s.value, s.label])
) as Record<PreceptorStatus, string>;

/** Human label for a preceptor status, falling back to the raw value if unknown. */
export const preceptorStatusLabel = (value: PreceptorStatus | string): string =>
  LABELS[value as PreceptorStatus] ?? value;

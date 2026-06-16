import type { RotationStatus } from "../api";

/** Display labels for the RotationStatus enum, in lifecycle order (used in the status dropdown + filter).
 *  The legacy "NotStarted" state is surfaced to staff as "Approved" — the enum name is kept on the wire
 *  to match the legacy vocabulary, but the booking is approved-and-awaiting-start at this point. */
export const ROTATION_STATUSES: { value: RotationStatus; label: string }[] = [
  { value: "Pending", label: "Pending" },
  { value: "NotStarted", label: "Approved" },
  { value: "Active", label: "Active" },
  { value: "ToBeEvaluated", label: "To be evaluated" },
  { value: "Completed", label: "Completed" },
  { value: "Cancelled", label: "Cancelled" },
  { value: "Refunded", label: "Refunded" },
  { value: "Abandoned", label: "Abandoned" },
  { value: "Rejected", label: "Rejected" }
];

const LABELS: Record<RotationStatus, string> = Object.fromEntries(
  ROTATION_STATUSES.map((s) => [s.value, s.label])
) as Record<RotationStatus, string>;

/** Human label for a rotation status, falling back to the raw value if unknown. */
export const rotationStatusLabel = (value: RotationStatus | string): string =>
  LABELS[value as RotationStatus] ?? value;

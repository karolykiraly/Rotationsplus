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

/** Colour tone for the status text in the admin list, mirroring the legacy colour-coding: active/good
 *  states are green, cancellation/refund states are brand-pink, the rest are neutral. */
export type StatusTone = "ok" | "brand" | "muted";

const TONE: Record<RotationStatus, StatusTone> = {
  Pending: "muted",
  NotStarted: "ok",      // "Approved" — booking is approved/awaiting start
  Active: "ok",
  ToBeEvaluated: "muted",
  Completed: "ok",
  Cancelled: "brand",
  Refunded: "brand",
  Abandoned: "muted",
  Rejected: "brand"
};

/** The CSS class for a status's coloured text (green `.ok-text`, pink `.brand-text`, or neutral). */
export const rotationStatusClass = (value: RotationStatus): string => {
  const tone = TONE[value] ?? "muted";
  return tone === "ok" ? "ok-text" : tone === "brand" ? "brand-text" : "";
};

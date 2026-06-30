import type { ProgramType } from "../api";

/** Display labels for the ProgramType enum, in the order shown in the type dropdown. */
export const PROGRAM_TYPES: { value: ProgramType; label: string }[] = [
  { value: "InPerson", label: "In person" },
  { value: "InPersonResearch", label: "In-person research" },
  { value: "Consultation", label: "Consultation" },
  { value: "ConsultationSub", label: "Consultation (sub)" },
  { value: "TeleRotation", label: "Tele-rotation" },
  { value: "TeleResearch", label: "Tele-research" },
  { value: "Dental", label: "Dental" }
];

const LABELS: Record<ProgramType, string> = Object.fromEntries(
  PROGRAM_TYPES.map((t) => [t.value, t.label])
) as Record<ProgramType, string>;

/** Human label for a program type, falling back to the raw value if unknown. */
export const programTypeLabel = (value: ProgramType | string): string =>
  LABELS[value as ProgramType] ?? value;

/** Short code prefix per program family — the legacy scheme (InPerson→IP, Consultation→CS,
 *  Tele→TL, Dental→DN). */
const CODE_PREFIX: Record<ProgramType, string> = {
  InPerson: "IP",
  InPersonResearch: "IP",
  Consultation: "CS",
  ConsultationSub: "CS",
  TeleRotation: "TL",
  TeleResearch: "TL",
  Dental: "DN"
};

/** The user-facing program code, e.g. "IP1042" (prefix by type + the server-assigned number). */
export const programCode = (type: ProgramType | string, number: number): string =>
  `${CODE_PREFIX[type as ProgramType] ?? "PR"}${number}`;

/** The program's display name shown in the admin Programs list's "Program Name" column. Uses the
 *  program's own name when set; otherwise falls back to the legacy default "{Specialty} Physician"
 *  (preceptor.program_name's default) so rows without an explicit name still render a sensible label.
 *  The separate "Specialty" column always shows the bare specialty. */
export const programDisplayName = (programName: string | null | undefined, specialtyName: string): string =>
  programName?.trim() ? programName.trim() : `${specialtyName} Physician`;

/** The display families the dashboard breaks programs into (research/sub variants fold into their base). */
export type ProgramFamily = "InPerson" | "Consultation" | "TeleRotation" | "Dental";

const FAMILY: Record<ProgramType, ProgramFamily> = {
  InPerson: "InPerson",
  InPersonResearch: "InPerson",
  Consultation: "Consultation",
  ConsultationSub: "Consultation",
  TeleRotation: "TeleRotation",
  TeleResearch: "TeleRotation",
  Dental: "Dental"
};

/** Sum the per-type counts that belong to one display family (e.g. InPerson + InPersonResearch). */
export const programFamilyCount = (
  counts: { type: ProgramType; count: number }[],
  family: ProgramFamily
): number => counts.filter((c) => FAMILY[c.type] === family).reduce((n, c) => n + c.count, 0);

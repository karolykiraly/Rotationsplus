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

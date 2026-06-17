import type { AcademicStatus, StudentStatus, VisaStatus } from "../api";

/** Display labels for the StudentStatus enum, in lifecycle order (status dropdown + filter). */
export const STUDENT_STATUSES: { value: StudentStatus; label: string }[] = [
  { value: "Registered", label: "Registered" },
  { value: "MemberProfileCompleted", label: "Profile completed" },
  { value: "MemberActivated", label: "Activated" },
  { value: "TurnedIntoContact", label: "Turned into contact" }
];

/** Display labels for the AcademicStatus enum (academic-track dropdown + filter). */
export const ACADEMIC_STATUSES: { value: AcademicStatus; label: string }[] = [
  { value: "UsPreMed", label: "US Pre-med" },
  { value: "MdStudent", label: "MD Student" },
  { value: "DoStudent", label: "DO Student" },
  { value: "DentalStudent", label: "Dental Student" },
  { value: "InternationalMedicalStudent", label: "International Medical Student" },
  { value: "InternationalMedicalGraduate", label: "International Medical Graduate" },
  { value: "PhysicianAssistantStudent", label: "Physician Assistant Student" },
  { value: "NursePractitionerStudent", label: "Nurse Practitioner Student" }
];

/** Display labels for the VisaStatus enum (optional; visa dropdown). */
export const VISA_STATUSES: { value: VisaStatus; label: string }[] = [
  { value: "CitizenOrGreenCard", label: "Citizen / Green card" },
  { value: "ValidVisa", label: "Valid visa" },
  { value: "InterviewScheduled", label: "Visa interview scheduled" },
  { value: "NeedsVisaHelp", label: "Needs help with visa" }
];

function labelMap<T extends string>(items: { value: T; label: string }[]): Record<T, string> {
  return Object.fromEntries(items.map((i) => [i.value, i.label])) as Record<T, string>;
}

const STATUS_LABELS = labelMap(STUDENT_STATUSES);
const ACADEMIC_LABELS = labelMap(ACADEMIC_STATUSES);
const VISA_LABELS = labelMap(VISA_STATUSES);

/** Human label for a student lifecycle status, falling back to the raw value. */
export const studentStatusLabel = (value: StudentStatus | string): string =>
  STATUS_LABELS[value as StudentStatus] ?? value;

/** Human label for an academic status, falling back to the raw value. */
export const academicStatusLabel = (value: AcademicStatus | string): string =>
  ACADEMIC_LABELS[value as AcademicStatus] ?? value;

/** Human label for a visa status (or a dash when none), falling back to the raw value. */
export const visaStatusLabel = (value: VisaStatus | string | null | undefined): string =>
  value ? VISA_LABELS[value as VisaStatus] ?? value : "—";

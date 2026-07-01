import type { AcademicStatus, Gender, ImmigrationStatus, StudentIdType, StudentStatus, VisaStatus } from "../api";

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

/** Legacy production slug for each academic status (the raw `academic_status` string the production
 *  Contacts → Students "Type" column renders, e.g. "international-medical-graduate"). Matches the
 *  legacy AddNewStudent modal's ids; `UsPreMed` is the one non-kebab special case ("pre-med"). */
const ACADEMIC_SLUGS: Record<AcademicStatus, string> = {
  UsPreMed: "pre-med",
  MdStudent: "md-student",
  DoStudent: "do-student",
  DentalStudent: "dental-student",
  InternationalMedicalStudent: "international-medical-student",
  InternationalMedicalGraduate: "international-medical-graduate",
  PhysicianAssistantStudent: "physician-assistant-student",
  NursePractitionerStudent: "nurse-practitioner-student"
};

/** Production "Type" value for a student — the raw academic-status slug, matching production exactly. */
export const academicStatusSlug = (value: AcademicStatus | string): string =>
  ACADEMIC_SLUGS[value as AcademicStatus] ?? value;

/** Gender options (profile Personal Information). Legacy stored 'male'/'female'/'none'. */
export const GENDERS: { value: Gender; label: string }[] = [
  { value: "Male", label: "Male" },
  { value: "Female", label: "Female" },
  { value: "NonBinary", label: "Non binary" }
];

/** Immigration-status options (profile Personal Information) — the exact legacy `visa_status` list. */
export const IMMIGRATION_STATUSES: { value: ImmigrationStatus; label: string }[] = [
  { value: "UsCitizen", label: "US Citizen" },
  { value: "UsPermanentResident", label: "US Permanent Resident" },
  { value: "PermanentResidentPending", label: "Permanent Resident Pending" },
  { value: "B1B2", label: "B1/B2" },
  { value: "F1", label: "F1" },
  { value: "J1", label: "J1" },
  { value: "H1B", label: "H1-B" },
  { value: "H4", label: "H4" },
  { value: "Esta", label: "ESTA" },
  { value: "NeedVisaInterviewScheduled", label: "I need a Visa, but I have interview scheduled" },
  { value: "NeedVisaNoInterview", label: "I need a Visa, and I do NOT have an interview scheduled" },
  { value: "Other", label: "Other" }
];

/** ID-type options (profile Personal Information — D.O. students provide an ID in place of a passport). */
export const ID_TYPES: { value: StudentIdType; label: string }[] = [
  { value: "DrivingLicense", label: "Driving License" },
  { value: "Passport", label: "Passport" }
];

/** Human label for a visa status (or a dash when none), falling back to the raw value. */
export const visaStatusLabel = (value: VisaStatus | string | null | undefined): string =>
  value ? VISA_LABELS[value as VisaStatus] ?? value : "—";

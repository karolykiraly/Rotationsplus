import type { EducationYear } from "../api";

/** Score / attempt / year option lists for the profile Education tab, matching legacy StudentProfile.js.
 *  Scores are the 3-digit range 180–300 (USMLE/COMLEX). USMLE Step 1 additionally allows Pass / Fail, so
 *  scores are stored as strings. Attempts are 1–10. Years mirror the legacy pre-med dropdown. */

/** 3-digit score range shared by USMLE Step 2CK/3 and COMLEX Level 2CE/3. */
export const SCORE_OPTIONS: string[] = Array.from({ length: 300 - 180 + 1 }, (_, i) => String(180 + i));

/** USMLE Step 1 scores — Pass / Fail precede the 3-digit range (legacy `scores1`). */
export const STEP1_SCORE_OPTIONS: string[] = ["Pass", "Fail", ...SCORE_OPTIONS];

/** Number of attempts (legacy `attemps`: 1–10). */
export const ATTEMPTS_OPTIONS: number[] = Array.from({ length: 10 }, (_, i) => i + 1);

/** Undergrad year options (legacy `years`): the enum value plus the production display label ("5th+"). */
export const YEAR_OPTIONS: { value: EducationYear; label: string }[] = [
  { value: "Freshman", label: "Freshman" },
  { value: "Sophomore", label: "Sophomore" },
  { value: "Junior", label: "Junior" },
  { value: "Senior", label: "Senior" },
  { value: "FifthPlus", label: "5th+" }
];

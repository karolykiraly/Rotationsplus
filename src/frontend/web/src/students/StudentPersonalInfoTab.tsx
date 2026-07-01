import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation } from "@tanstack/react-query";
import {
  updateStudentPersonalInfo,
  type AcademicStatus,
  type Gender,
  type ImmigrationStatus,
  type StudentDetail,
  type StudentIdType,
  type StudentPersonalInfoInput
} from "../api";
import { ACADEMIC_STATUSES, GENDERS, ID_TYPES, IMMIGRATION_STATUSES } from "./studentStatuses";

// Mirrors the API's TryNormalizePersonalInfo rules (required names, optional caps). Dates are yyyy-mm-dd
// strings from <input type="date">; enums are the empty string when "not captured".
const schema = z.object({
  firstName: z.string().trim().min(1, "Enter a first name.").max(100, "At most 100 characters."),
  lastName: z.string().trim().min(1, "Enter a last name.").max(100, "At most 100 characters."),
  mobilePhone: z.string().trim().max(40, "At most 40 characters.").optional(),
  academicStatus: z.string().min(1, "Select an academic status."),
  birthdate: z.string().optional(),
  gender: z.string().optional(),
  immigrationStatus: z.string().optional(),
  immigrationStatusOther: z.string().trim().max(120, "At most 120 characters.").optional(),
  visaInterviewDate: z.string().optional(),
  passportIssuedCountry: z.string().trim().max(100, "At most 100 characters.").optional(),
  passportNumber: z.string().trim().max(60, "At most 60 characters.").optional(),
  selectedIdType: z.string().optional(),
  idNumber: z.string().trim().max(60, "At most 60 characters.").optional()
});
type FormValues = z.infer<typeof schema>;

const orNull = (v?: string) => (v?.trim() ? v.trim() : null);

/** The student profile's Personal Information tab (legacy StudentProfile.js tab 0 / onSaveProfile1).
 *  Passport vs ID fields switch on the D.O. track; the visa-interview date + free-text override appear
 *  conditionally, matching production. Email is read-only (CIAM/Entra-linked identity). */
export function StudentPersonalInfoTab({
  student,
  onSaved
}: {
  student: StudentDetail;
  onSaved: (updated: StudentDetail) => void;
}) {
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors }
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      firstName: student.firstName,
      lastName: student.lastName,
      mobilePhone: student.mobilePhone ?? "",
      academicStatus: student.academicStatus,
      birthdate: student.birthdate ?? "",
      gender: student.gender ?? "",
      immigrationStatus: student.immigrationStatus ?? "",
      immigrationStatusOther: student.immigrationStatusOther ?? "",
      visaInterviewDate: student.visaInterviewDate ?? "",
      passportIssuedCountry: student.passportIssuedCountry ?? "",
      passportNumber: student.passportNumber ?? "",
      selectedIdType: student.selectedIdType ?? "",
      idNumber: student.idNumber ?? ""
    }
  });

  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  const save = useMutation({
    mutationFn: (input: StudentPersonalInfoInput) => updateStudentPersonalInfo(student.id, input),
    onSuccess: (updated) => { setBanner({ type: "ok", text: "Personal information saved." }); onSaved(updated); },
    onError: (e) => setBanner({ type: "error", text: (e as Error).message })
  });

  // The D.O. track collects an ID (driving license / passport) instead of a passport country + number.
  const isDo = watch("academicStatus") === "DoStudent";
  const immigration = watch("immigrationStatus");

  const submit = handleSubmit((v) =>
    save.mutate({
      firstName: v.firstName.trim(),
      lastName: v.lastName.trim(),
      mobilePhone: orNull(v.mobilePhone),
      academicStatus: v.academicStatus as AcademicStatus,
      birthdate: orNull(v.birthdate),
      gender: v.gender ? (v.gender as Gender) : null,
      immigrationStatus: v.immigrationStatus ? (v.immigrationStatus as ImmigrationStatus) : null,
      immigrationStatusOther: immigration === "Other" ? orNull(v.immigrationStatusOther) : null,
      visaInterviewDate: immigration === "NeedVisaInterviewScheduled" ? orNull(v.visaInterviewDate) : null,
      passportIssuedCountry: isDo ? null : orNull(v.passportIssuedCountry),
      passportNumber: isDo ? null : orNull(v.passportNumber),
      selectedIdType: isDo && v.selectedIdType ? (v.selectedIdType as StudentIdType) : null,
      idNumber: isDo ? orNull(v.idNumber) : null
    })
  );

  return (
    <form onSubmit={submit} noValidate className="profile-form">
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      <div className="form-grid">
        <div className="field">
          <label htmlFor="p-first">First name</label>
          <input id="p-first" type="text" {...register("firstName")} />
          {errors.firstName && <span className="err">{errors.firstName.message}</span>}
        </div>
        <div className="field">
          <label htmlFor="p-last">Last name</label>
          <input id="p-last" type="text" {...register("lastName")} />
          {errors.lastName && <span className="err">{errors.lastName.message}</span>}
        </div>

        <div className="field">
          <label htmlFor="p-mobile">Mobile number</label>
          <input id="p-mobile" type="tel" {...register("mobilePhone")} />
          {errors.mobilePhone && <span className="err">{errors.mobilePhone.message}</span>}
        </div>
        <div className="field">
          {/* Email is the CIAM/Entra-linked identity — shown read-only (edited via the identity provider). */}
          <label htmlFor="p-email">Email</label>
          <input id="p-email" type="email" value={student.email} readOnly disabled />
        </div>

        <div className="field">
          <label htmlFor="p-academic">Academic Status</label>
          <select id="p-academic" {...register("academicStatus")}>
            {ACADEMIC_STATUSES.map((a) => <option key={a.value} value={a.value}>{a.label}</option>)}
          </select>
          {errors.academicStatus && <span className="err">{errors.academicStatus.message}</span>}
        </div>
        <div className="field">
          <label htmlFor="p-birth">Birthday</label>
          <input id="p-birth" type="date" {...register("birthdate")} />
        </div>

        {isDo ? (
          <>
            <div className="field">
              <label htmlFor="p-idtype">Select ID</label>
              <select id="p-idtype" {...register("selectedIdType")}>
                <option value="">— Select —</option>
                {ID_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>
            <div className="field">
              <label htmlFor="p-idnum">ID Number</label>
              <input id="p-idnum" type="text" {...register("idNumber")} />
              {errors.idNumber && <span className="err">{errors.idNumber.message}</span>}
            </div>
          </>
        ) : (
          <>
            <div className="field">
              <label htmlFor="p-passcountry">Which country issued your passport?</label>
              <input id="p-passcountry" type="text" {...register("passportIssuedCountry")} />
              {errors.passportIssuedCountry && <span className="err">{errors.passportIssuedCountry.message}</span>}
            </div>
            <div className="field">
              <label htmlFor="p-passnum">Passport Number</label>
              <input id="p-passnum" type="text" {...register("passportNumber")} />
              {errors.passportNumber && <span className="err">{errors.passportNumber.message}</span>}
            </div>
          </>
        )}

        <div className="field">
          <label htmlFor="p-immigration">Immigration Status</label>
          <select id="p-immigration" {...register("immigrationStatus")}>
            <option value="">— Select —</option>
            {IMMIGRATION_STATUSES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
        </div>
        {immigration === "NeedVisaInterviewScheduled" && (
          <div className="field">
            <label htmlFor="p-visadate">Visa Interview Date</label>
            <input id="p-visadate" type="date" {...register("visaInterviewDate")} />
          </div>
        )}
        {immigration === "Other" && (
          <div className="field">
            <label htmlFor="p-imother">Enter his/her immigration status</label>
            <input id="p-imother" type="text" {...register("immigrationStatusOther")} />
            {errors.immigrationStatusOther && <span className="err">{errors.immigrationStatusOther.message}</span>}
          </div>
        )}
      </div>

      <fieldset className="field radio-group">
        <legend>Gender</legend>
        {GENDERS.map((g) => (
          <label key={g.value} className="radio">
            <input type="radio" value={g.value} {...register("gender")} /> {g.label}
          </label>
        ))}
      </fieldset>

      <div className="profile-form-foot">
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>
          {save.isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}

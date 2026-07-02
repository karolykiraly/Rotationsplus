import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import {
  updateStudentEducation,
  type EducationYear,
  type ExamStatus,
  type StudentDetail,
  type StudentEducationInput
} from "../api";
import { ATTEMPTS_OPTIONS, SCORE_OPTIONS, STEP1_SCORE_OPTIONS, YEAR_OPTIONS } from "./educationOptions";

/** Which Education branch a student's academic track renders (legacy StudentProfile.js tab 2 keyed on
 *  `academic_status`): D.O.→COMLEX, Dental→Dental, US pre-med→Pre-med, everyone else→IMS/IMG USMLE. */
type Branch = "comlex" | "dental" | "premed" | "usmle";
function branchFor(status: StudentDetail["academicStatus"]): Branch {
  if (status === "DoStudent") return "comlex";
  if (status === "DentalStudent") return "dental";
  if (status === "UsPreMed") return "premed";
  return "usmle";
}

/** A Yes/No radio pair bound to a nullable boolean (legacy pattern for ECFMG / MATCH / TOEFL / INDBE). */
function YesNo({
  legend,
  name,
  value,
  onChange
}: {
  legend: string;
  name: string;
  value: boolean | null;
  onChange: (v: boolean) => void;
}) {
  return (
    <fieldset className="field radio-group">
      <legend>{legend}</legend>
      <label className="radio">
        <input type="radio" name={name} checked={value === true} onChange={() => onChange(true)} /> Yes
      </label>
      <label className="radio">
        <input type="radio" name={name} checked={value === false} onChange={() => onChange(false)} /> No
      </label>
    </fieldset>
  );
}

/** The USMLE-step / COMLEX-level control: a three-way status radio (Taken → score + attempts; WillTake →
 *  scheduled date; NoPlan) matching legacy production. Score options differ per step (Step 1 adds Pass/Fail). */
function ExamStep({
  legend,
  name,
  scoreOptions,
  dateLabel,
  willTakeLabel,
  status,
  score,
  attempts,
  date,
  onStatus,
  onScore,
  onAttempts,
  onDate
}: {
  legend: string;
  name: string;
  scoreOptions: string[];
  dateLabel: string;
  willTakeLabel: string;
  status: ExamStatus | null;
  score: string | null;
  attempts: number | null;
  date: string | null;
  onStatus: (v: ExamStatus) => void;
  onScore: (v: string) => void;
  onAttempts: (v: number | null) => void;
  onDate: (v: string) => void;
}) {
  return (
    <fieldset className="field exam-step">
      <legend>{legend}</legend>

      <label className="radio">
        <input type="radio" name={name} checked={status === "Taken"} onChange={() => onStatus("Taken")} /> Yes
      </label>
      {status === "Taken" && (
        <div className="exam-step-detail">
          <div className="field">
            <label htmlFor={`${name}-score`}>3 digit score</label>
            <select id={`${name}-score`} value={score ?? ""} onChange={(e) => onScore(e.target.value)}>
              <option value="">3 digit score</option>
              {scoreOptions.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>
          <div className="field">
            <label htmlFor={`${name}-attempts`}>Number of attempts</label>
            <select
              id={`${name}-attempts`}
              value={attempts ?? ""}
              onChange={(e) => onAttempts(e.target.value ? Number(e.target.value) : null)}
            >
              <option value="">Number of attempts</option>
              {ATTEMPTS_OPTIONS.map((a) => <option key={a} value={a}>{a}</option>)}
            </select>
          </div>
        </div>
      )}

      <label className="radio">
        <input type="radio" name={name} checked={status === "WillTake"} onChange={() => onStatus("WillTake")} /> {willTakeLabel}
      </label>
      {status === "WillTake" && (
        <div className="field exam-step-detail">
          <label htmlFor={`${name}-date`}>{dateLabel}</label>
          <input id={`${name}-date`} type="date" value={date ?? ""} onChange={(e) => onDate(e.target.value)} />
        </div>
      )}

      <label className="radio">
        <input type="radio" name={name} checked={status === "NoPlan"} onChange={() => onStatus("NoPlan")} /> No plan on taking
      </label>
    </fieldset>
  );
}

/** The student profile's Education tab (legacy StudentProfile.js tab 2 / onSaveProfile3). Renders one of
 *  four academic-track branches; each saves independently via PUT /api/students/{id}/education. Shared
 *  columns (school / country / graduation date) are passed through unchanged on branches that don't edit
 *  them, so a save never wipes data owned by another tab. */
export function StudentEducationTab({
  student,
  onSaved
}: {
  student: StudentDetail;
  onSaved: (updated: StudentDetail) => void;
}) {
  const branch = branchFor(student.academicStatus);
  const [banner, setBanner] = useState<{ type: "ok" | "error"; text: string } | null>(null);

  // Shared identity/education fields (IMS/IMG + Dental edit these; other branches pass them through).
  const [medicalSchool, setMedicalSchool] = useState(student.medicalSchool ?? "");
  const [medicalSchoolCountry, setMedicalSchoolCountry] = useState(student.medicalSchoolCountry ?? "");
  const [graduationDate, setGraduationDate] = useState(student.graduationDate ?? "");

  // USMLE (IMS/IMG).
  const [usmleStep1, setUsmleStep1] = useState<ExamStatus | null>(student.usmleStep1 ?? null);
  const [usmleScore1, setUsmleScore1] = useState<string | null>(student.usmleScore1 ?? null);
  const [usmleAttempts1, setUsmleAttempts1] = useState<number | null>(student.usmleAttempts1 ?? null);
  const [usmleDate1, setUsmleDate1] = useState<string | null>(student.usmleDate1 ?? null);
  const [usmleStep2, setUsmleStep2] = useState<ExamStatus | null>(student.usmleStep2 ?? null);
  const [usmleScore2, setUsmleScore2] = useState<string | null>(student.usmleScore2 ?? null);
  const [usmleAttempts2, setUsmleAttempts2] = useState<number | null>(student.usmleAttempts2 ?? null);
  const [usmleDate2, setUsmleDate2] = useState<string | null>(student.usmleDate2 ?? null);
  const [usmleStep3, setUsmleStep3] = useState<ExamStatus | null>(student.usmleStep3 ?? null);
  const [usmleScore3, setUsmleScore3] = useState<string | null>(student.usmleScore3 ?? null);
  const [usmleAttempts3, setUsmleAttempts3] = useState<number | null>(student.usmleAttempts3 ?? null);
  const [usmleDate3, setUsmleDate3] = useState<string | null>(student.usmleDate3 ?? null);
  const [ecfmgCertified, setEcfmgCertified] = useState<boolean | null>(student.ecfmgCertified ?? null);
  const [appliedMatch, setAppliedMatch] = useState<boolean | null>(student.appliedMatch ?? null);

  // COMLEX (D.O.).
  const [comlexLevel1Taken, setComlexLevel1Taken] = useState<boolean | null>(student.comlexLevel1Taken ?? null);
  const [comlexLevel1Passed, setComlexLevel1Passed] = useState<boolean | null>(student.comlexLevel1Passed ?? null);
  const [comlexLevel2, setComlexLevel2] = useState<ExamStatus | null>(student.comlexLevel2 ?? null);
  const [comlexLevel2Score, setComlexLevel2Score] = useState<string | null>(student.comlexLevel2Score ?? null);
  const [comlexLevel2Attempts, setComlexLevel2Attempts] = useState<number | null>(student.comlexLevel2Attempts ?? null);
  const [comlexLevel2Date, setComlexLevel2Date] = useState<string | null>(student.comlexLevel2Date ?? null);
  const [comlexLevel3, setComlexLevel3] = useState<ExamStatus | null>(student.comlexLevel3 ?? null);
  const [comlexLevel3Score, setComlexLevel3Score] = useState<string | null>(student.comlexLevel3Score ?? null);
  const [comlexLevel3Attempts, setComlexLevel3Attempts] = useState<number | null>(student.comlexLevel3Attempts ?? null);
  const [comlexLevel3Date, setComlexLevel3Date] = useState<string | null>(student.comlexLevel3Date ?? null);

  // Pre-med.
  const [undergrad, setUndergrad] = useState(student.undergrad ?? "");
  const [educationYear, setEducationYear] = useState<EducationYear | "">(student.educationYear ?? "");
  const [isAmsa, setIsAmsa] = useState<boolean>(student.isAmsa ?? false);
  const [association, setAssociation] = useState(student.association ?? "");
  const [isLeadership, setIsLeadership] = useState<boolean | null>(student.isLeadership ?? null);

  // Dental.
  const [isToefl, setIsToefl] = useState<boolean>(student.isToefl ?? false);
  const [isIndbe, setIsIndbe] = useState<boolean>(student.isIndbe ?? false);

  const save = useMutation({
    mutationFn: (input: StudentEducationInput) => updateStudentEducation(student.id, input),
    onSuccess: (updated) => { setBanner({ type: "ok", text: "Education saved." }); onSaved(updated); },
    onError: (e) => setBanner({ type: "error", text: (e as Error).message })
  });

  const orNull = (v: string) => (v.trim() ? v.trim() : null);

  // Mirror production's Save-guard: a Taken step needs a score + attempts, a WillTake step needs a date.
  const usmleStepIncomplete = (
    status: ExamStatus | null, score: string | null, attempts: number | null, date: string | null
  ) => (status === "Taken" && (!score || attempts == null)) || (status === "WillTake" && !date);

  const submit = (e: React.FormEvent) => {
    e.preventDefault();

    if (branch === "usmle") {
      if (
        usmleStepIncomplete(usmleStep1, usmleScore1, usmleAttempts1, usmleDate1) ||
        usmleStepIncomplete(usmleStep2, usmleScore2, usmleAttempts2, usmleDate2) ||
        usmleStepIncomplete(usmleStep3, usmleScore3, usmleAttempts3, usmleDate3)
      ) {
        setBanner({ type: "error", text: "Complete the score/attempts (or date) for each USMLE step you answered." });
        return;
      }
      save.mutate({
        medicalSchool: orNull(medicalSchool),
        medicalSchoolCountry: orNull(medicalSchoolCountry),
        graduationDate: orNull(graduationDate),
        usmleStep1, usmleScore1, usmleAttempts1, usmleDate1,
        usmleStep2, usmleScore2, usmleAttempts2, usmleDate2,
        usmleStep3, usmleScore3, usmleAttempts3, usmleDate3,
        ecfmgCertified, appliedMatch
      });
      return;
    }

    if (branch === "comlex") {
      if (
        (comlexLevel1Taken === true && comlexLevel1Passed == null) ||
        usmleStepIncomplete(comlexLevel2, comlexLevel2Score, comlexLevel2Attempts, comlexLevel2Date) ||
        usmleStepIncomplete(comlexLevel3, comlexLevel3Score, comlexLevel3Attempts, comlexLevel3Date)
      ) {
        setBanner({ type: "error", text: "Complete the COMLEX answers (pass/fail, or score/attempts, or date)." });
        return;
      }
      save.mutate({
        // School / country / graduation belong to other tabs for a D.O. student — pass through unchanged.
        medicalSchool: orNull(medicalSchool),
        medicalSchoolCountry: orNull(medicalSchoolCountry),
        graduationDate: orNull(graduationDate),
        comlexLevel1Taken, comlexLevel1Passed,
        comlexLevel2, comlexLevel2Score, comlexLevel2Attempts, comlexLevel2Date,
        comlexLevel3, comlexLevel3Score, comlexLevel3Attempts, comlexLevel3Date
      });
      return;
    }

    if (branch === "premed") {
      save.mutate({
        // Pre-med has no medical school here — pass the identity columns through unchanged.
        medicalSchool: orNull(medicalSchool),
        medicalSchoolCountry: orNull(medicalSchoolCountry),
        graduationDate: orNull(graduationDate),
        undergrad: orNull(undergrad),
        educationYear: educationYear || null,
        isAmsa,
        association: orNull(association),
        isLeadership
      });
      return;
    }

    // Dental.
    save.mutate({
      medicalSchool: orNull(medicalSchool),
      medicalSchoolCountry: orNull(medicalSchoolCountry),
      graduationDate: orNull(graduationDate),
      isToefl,
      isIndbe,
      appliedMatch
    });
  };

  return (
    <form onSubmit={submit} className="profile-form">
      {banner && <div className={`banner ${banner.type}`} role="alert">{banner.text}</div>}

      {branch === "comlex" && (
        <>
          <div className="heading-xs">Education (D.O. Student)</div>

          <fieldset className="field radio-group comlex-l1">
            <legend>Have you taken COMLEX Level 1?</legend>
            <label className="radio">
              <input type="radio" name="comlexL1" checked={comlexLevel1Taken === true} onChange={() => setComlexLevel1Taken(true)} /> Yes
            </label>
            {comlexLevel1Taken === true && (
              <div className="exam-step-detail" role="group" aria-label="How did you do?">
                <span className="field-label">How did you do?</span>
                <label className="radio">
                  <input type="radio" name="comlexL1Passed" checked={comlexLevel1Passed === true} onChange={() => setComlexLevel1Passed(true)} /> Passed
                </label>
                <label className="radio">
                  <input type="radio" name="comlexL1Passed" checked={comlexLevel1Passed === false} onChange={() => setComlexLevel1Passed(false)} /> Failed
                </label>
              </div>
            )}
            <label className="radio">
              <input
                type="radio"
                name="comlexL1"
                checked={comlexLevel1Taken === false}
                onChange={() => { setComlexLevel1Taken(false); setComlexLevel1Passed(null); }}
              /> No
            </label>
          </fieldset>

          <ExamStep
            legend="Have you taken COMLEX Level 2CE?"
            name="comlexL2"
            scoreOptions={SCORE_OPTIONS}
            dateLabel="COMLEX 2 CE Date"
            willTakeLabel="Not yet, but I will take it on..."
            status={comlexLevel2}
            score={comlexLevel2Score}
            attempts={comlexLevel2Attempts}
            date={comlexLevel2Date}
            onStatus={setComlexLevel2}
            onScore={setComlexLevel2Score}
            onAttempts={setComlexLevel2Attempts}
            onDate={setComlexLevel2Date}
          />

          <ExamStep
            legend="Have you taken COMLEX Level 3?"
            name="comlexL3"
            scoreOptions={SCORE_OPTIONS}
            dateLabel="COMLEX Level 3 Date"
            willTakeLabel="Not yet, but I will take it on..."
            status={comlexLevel3}
            score={comlexLevel3Score}
            attempts={comlexLevel3Attempts}
            date={comlexLevel3Date}
            onStatus={setComlexLevel3}
            onScore={setComlexLevel3Score}
            onAttempts={setComlexLevel3Attempts}
            onDate={setComlexLevel3Date}
          />
        </>
      )}

      {branch === "dental" && (
        <>
          <div className="heading-xs">Education (Dental)</div>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="e-country">Country I attend(ed) medical school</label>
              <input id="e-country" type="text" value={medicalSchoolCountry} onChange={(e) => setMedicalSchoolCountry(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="e-school">Name of medical school</label>
              <input id="e-school" type="text" value={medicalSchool} onChange={(e) => setMedicalSchool(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="e-grad">Graduation date</label>
              <input id="e-grad" type="date" value={graduationDate} onChange={(e) => setGraduationDate(e.target.value)} />
            </div>
          </div>
          <YesNo legend="Have you passed TOEFL?" name="toefl" value={isToefl} onChange={setIsToefl} />
          <YesNo legend="Have you passed INDBE?" name="indbe" value={isIndbe} onChange={setIsIndbe} />
          <YesNo legend="Have you applied to the MATCH before?" name="matchDental" value={appliedMatch} onChange={setAppliedMatch} />
        </>
      )}

      {branch === "premed" && (
        <>
          <div className="heading-xs">Education for US pre-med</div>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="e-undergrad">My undergrad program</label>
              <input id="e-undergrad" type="text" value={undergrad} onChange={(e) => setUndergrad(e.target.value)} placeholder="My undergrad program" />
            </div>
            <div className="field">
              <label htmlFor="e-year">Which year?</label>
              <select id="e-year" value={educationYear} onChange={(e) => setEducationYear(e.target.value as EducationYear | "")}>
                <option value="">Which year?</option>
                {YEAR_OPTIONS.map((y) => <option key={y.value} value={y.value}>{y.label}</option>)}
              </select>
            </div>
            <div className="field">
              <label htmlFor="e-grad">My expected graduation</label>
              <input id="e-grad" type="date" value={graduationDate} onChange={(e) => setGraduationDate(e.target.value)} />
            </div>
          </div>
          <label className="checkbox">
            <input type="checkbox" checked={isAmsa} onChange={(e) => setIsAmsa(e.target.checked)} /> I am a member of AMSA
          </label>
          <div className="field">
            <label htmlFor="e-assoc">Other student medical associations I am a member of</label>
            <input id="e-assoc" type="text" value={association} onChange={(e) => setAssociation(e.target.value)} />
          </div>
          <YesNo
            legend="Do you hold a leadership role within your organization?"
            name="leadership"
            value={isLeadership}
            onChange={setIsLeadership}
          />
        </>
      )}

      {branch === "usmle" && (
        <>
          <div className="heading-xs">Education (IMS/IMG)</div>
          <div className="form-grid">
            <div className="field">
              <label htmlFor="e-country">Country I attend(ed) medical school</label>
              <input id="e-country" type="text" value={medicalSchoolCountry} onChange={(e) => setMedicalSchoolCountry(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="e-school">Name of medical school</label>
              <input id="e-school" type="text" value={medicalSchool} onChange={(e) => setMedicalSchool(e.target.value)} />
            </div>
            <div className="field">
              <label htmlFor="e-grad">Graduation date</label>
              <input id="e-grad" type="date" value={graduationDate} onChange={(e) => setGraduationDate(e.target.value)} />
            </div>
          </div>

          <ExamStep
            legend="Have you taken USMLE step 1?"
            name="usmle1"
            scoreOptions={STEP1_SCORE_OPTIONS}
            dateLabel="USMLE Step 1 Date"
            willTakeLabel="No, but I will take it on..."
            status={usmleStep1}
            score={usmleScore1}
            attempts={usmleAttempts1}
            date={usmleDate1}
            onStatus={setUsmleStep1}
            onScore={setUsmleScore1}
            onAttempts={setUsmleAttempts1}
            onDate={setUsmleDate1}
          />
          <ExamStep
            legend="Have you taken USMLE step 2CK?"
            name="usmle2"
            scoreOptions={SCORE_OPTIONS}
            dateLabel="USMLE 2 CK Date"
            willTakeLabel="No, but I will take it on..."
            status={usmleStep2}
            score={usmleScore2}
            attempts={usmleAttempts2}
            date={usmleDate2}
            onStatus={setUsmleStep2}
            onScore={setUsmleScore2}
            onAttempts={setUsmleAttempts2}
            onDate={setUsmleDate2}
          />
          <YesNo legend="Are you ECFMG certified?" name="ecfmg" value={ecfmgCertified} onChange={setEcfmgCertified} />
          <ExamStep
            legend="Have you taken your step 3?"
            name="usmle3"
            scoreOptions={SCORE_OPTIONS}
            dateLabel="USMLE Step 3 Date"
            willTakeLabel="No, but I will take it on..."
            status={usmleStep3}
            score={usmleScore3}
            attempts={usmleAttempts3}
            date={usmleDate3}
            onStatus={setUsmleStep3}
            onScore={setUsmleScore3}
            onAttempts={setUsmleAttempts3}
            onDate={setUsmleDate3}
          />
          <YesNo legend="Have you applied to the MATCH before?" name="matchUsmle" value={appliedMatch} onChange={setAppliedMatch} />
        </>
      )}

      <div className="profile-form-foot">
        <button type="submit" className="btn btn-primary" disabled={save.isPending}>
          {save.isPending ? "Saving…" : "Save"}
        </button>
      </div>
    </form>
  );
}

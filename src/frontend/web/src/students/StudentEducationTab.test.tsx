import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ updateStudentEducation: vi.fn() }));
vi.mock("../api", () => ({
  updateStudentEducation: (id: string, input: unknown) => h.updateStudentEducation(id, input)
}));

import { StudentEducationTab } from "./StudentEducationTab";
import type { StudentDetail } from "../api";

const base = (over: Partial<StudentDetail> = {}): StudentDetail => ({
  id: "s1", firstName: "Hilda", lastName: "Wong", email: "h@x.com", mobilePhone: null,
  academicStatus: "InternationalMedicalGraduate", visaStatus: null, medicalSchool: null,
  medicalSchoolCountry: null, city: null, state: null, status: "Registered", studentOid: null,
  ...over
});

function renderTab(student: StudentDetail) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <StudentEducationTab student={student} onSaved={() => {}} />
    </QueryClientProvider>
  );
}

describe("StudentEducationTab", () => {
  beforeEach(() => {
    h.updateStudentEducation.mockReset().mockImplementation((_id, input) =>
      Promise.resolve(base(input as Partial<StudentDetail>)));
  });

  it("renders the IMS/IMG USMLE branch and saves a Taken step with score + attempts", async () => {
    renderTab(base());
    expect(screen.getByText("Education (IMS/IMG)")).toBeInTheDocument();

    // Answer USMLE Step 1 = Yes (Taken) → score + attempts appear.
    const step1 = screen.getByRole("group", { name: "Have you taken USMLE step 1?" });
    await userEvent.click(within(step1).getByRole("radio", { name: "Yes" }));
    await userEvent.selectOptions(within(step1).getByLabelText("3 digit score"), "Pass");
    await userEvent.selectOptions(within(step1).getByLabelText("Number of attempts"), "2");

    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentEducation).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ usmleStep1: "Taken", usmleScore1: "Pass", usmleAttempts1: 2 })
    );
    expect(await screen.findByText("Education saved.")).toBeInTheDocument();
  });

  it("blocks the USMLE save when a Taken step is missing its score/attempts", async () => {
    renderTab(base());
    const step1 = screen.getByRole("group", { name: "Have you taken USMLE step 1?" });
    await userEvent.click(within(step1).getByRole("radio", { name: "Yes" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(await screen.findByText(/Complete the score\/attempts/)).toBeInTheDocument();
    expect(h.updateStudentEducation).not.toHaveBeenCalled();
  });

  it("saves a WillTake step with only a scheduled date", async () => {
    renderTab(base());
    const step2 = screen.getByRole("group", { name: "Have you taken USMLE step 2CK?" });
    await userEvent.click(within(step2).getByRole("radio", { name: "No, but I will take it on..." }));
    await userEvent.type(within(step2).getByLabelText("USMLE 2 CK Date"), "2027-05-01");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentEducation).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ usmleStep2: "WillTake", usmleDate2: "2027-05-01" })
    );
  });

  it("renders the D.O. COMLEX branch with Level-1 pass/fail follow-up", async () => {
    renderTab(base({ academicStatus: "DoStudent" }));
    expect(screen.getByText("Education (D.O. Student)")).toBeInTheDocument();

    const l1 = screen.getByRole("group", { name: "Have you taken COMLEX Level 1?" });
    await userEvent.click(within(l1).getByRole("radio", { name: "Yes" }));
    // Follow-up "How did you do?" appears only after Yes.
    await userEvent.click(within(l1).getByRole("radio", { name: "Passed" }));

    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentEducation).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ comlexLevel1Taken: true, comlexLevel1Passed: true })
    );
  });

  it("blocks the COMLEX save when Level 1 is Yes but pass/fail is unanswered", async () => {
    renderTab(base({ academicStatus: "DoStudent" }));
    const l1 = screen.getByRole("group", { name: "Have you taken COMLEX Level 1?" });
    await userEvent.click(within(l1).getByRole("radio", { name: "Yes" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(await screen.findByText(/Complete the COMLEX answers/)).toBeInTheDocument();
    expect(h.updateStudentEducation).not.toHaveBeenCalled();
  });

  it("renders the pre-med branch and saves undergrad + year + AMSA", async () => {
    renderTab(base({ academicStatus: "UsPreMed" }));
    expect(screen.getByText("Education for US pre-med")).toBeInTheDocument();

    await userEvent.type(screen.getByLabelText("My undergrad program"), "State University");
    await userEvent.selectOptions(screen.getByLabelText("Which year?"), "FifthPlus");
    await userEvent.click(screen.getByRole("checkbox", { name: /member of AMSA/ }));

    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentEducation).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ undergrad: "State University", educationYear: "FifthPlus", isAmsa: true })
    );
  });

  it("renders the dental branch with TOEFL/INDBE and passes shared school through", async () => {
    renderTab(base({ academicStatus: "DentalStudent", medicalSchool: "Kept School", medicalSchoolCountry: "Kept Country" }));
    expect(screen.getByText("Education (Dental)")).toBeInTheDocument();

    const toefl = screen.getByRole("group", { name: "Have you passed TOEFL?" });
    await userEvent.click(within(toefl).getByRole("radio", { name: "Yes" }));

    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentEducation).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ isToefl: true, medicalSchool: "Kept School", medicalSchoolCountry: "Kept Country" })
    );
  });
});

import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  useMe: vi.fn(), getStudent: vi.fn(), updateStudentPersonalInfo: vi.fn(),
  updateStudentNeeds: vi.fn(), updateStudentEducation: vi.fn(), getSpecialties: vi.fn()
}));
vi.mock("../useMe", () => ({ useMe: () => h.useMe() }));
vi.mock("../api", () => ({
  getStudent: (id: string) => h.getStudent(id),
  updateStudentPersonalInfo: (id: string, input: unknown) => h.updateStudentPersonalInfo(id, input),
  updateStudentNeeds: (id: string, input: unknown) => h.updateStudentNeeds(id, input),
  updateStudentEducation: (id: string, input: unknown) => h.updateStudentEducation(id, input),
  getSpecialties: () => h.getSpecialties()
}));

import { StudentProfilePage } from "./StudentProfilePage";

const DETAIL = {
  id: "s1",
  firstName: "Sam",
  lastName: "Rivera",
  email: "sam@x.com",
  mobilePhone: "+1 555 0100",
  academicStatus: "InternationalMedicalGraduate",
  visaStatus: "NeedsVisaHelp",
  medicalSchool: null, medicalSchoolCountry: null,
  city: "Chicago", state: "IL",
  status: "MemberActivated", studentOid: "oid-1",
  birthdate: "1996-04-22", gender: "Female",
  immigrationStatus: "B1B2", immigrationStatusOther: null, visaInterviewDate: null,
  passportIssuedCountry: "India", passportNumber: "P123", selectedIdType: null, idNumber: null, avatarBlobName: null
};

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

function renderPage() {
  return render(
    <QueryClientProvider client={newClient()}>
      <MemoryRouter initialEntries={["/admin/students/s1"]}>
        <Routes>
          <Route path="/admin/students/:id" element={<StudentProfilePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("StudentProfilePage", () => {
  beforeEach(() => {
    h.useMe.mockReset().mockReturnValue({ user: { isAdmin: true } });
    h.getStudent.mockReset().mockResolvedValue(DETAIL);
    h.updateStudentPersonalInfo.mockReset().mockResolvedValue(DETAIL);
  });

  it("renders the seven production tabs and the loaded Personal Information", async () => {
    renderPage();
    for (const label of ["Personal information", "Needs", "Education", "Rotations", "Achievements", "Documents", "Sales"]) {
      expect(await screen.findByRole("tab", { name: label })).toBeInTheDocument();
    }
    expect(screen.getByText("Sam Rivera")).toBeInTheDocument();
    expect(screen.getByLabelText("First name")).toHaveValue("Sam");
    // Email is read-only (CIAM-linked identity).
    const email = screen.getByLabelText("Email");
    expect(email).toHaveValue("sam@x.com");
    expect(email).toBeDisabled();
    expect(screen.getByLabelText("Immigration Status")).toHaveValue("B1B2");
    expect(h.getStudent).toHaveBeenCalledWith("s1");
  });

  it("saves the Personal Information tab via the per-tab endpoint", async () => {
    renderPage();
    await screen.findByLabelText("First name");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(h.updateStudentPersonalInfo).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({
        firstName: "Sam",
        academicStatus: "InternationalMedicalGraduate",
        immigrationStatus: "B1B2",
        passportIssuedCountry: "India"
      })
    );
    expect(await screen.findByText("Personal information saved.")).toBeInTheDocument();
  });

  it("swaps passport fields for the ID fields on the D.O. track", async () => {
    renderPage();
    await screen.findByLabelText("Which country issued your passport?");
    await userEvent.selectOptions(screen.getByLabelText("Academic Status"), "DoStudent");

    expect(screen.getByLabelText("Select ID")).toBeInTheDocument();
    expect(screen.queryByLabelText("Which country issued your passport?")).not.toBeInTheDocument();
  });

  it("renders the Education tab for the student's academic track", async () => {
    renderPage();
    await userEvent.click(await screen.findByRole("tab", { name: "Education" }));
    // The IMG student sees the IMS/IMG USMLE branch.
    expect(await screen.findByText("Education (IMS/IMG)")).toBeInTheDocument();
  });

  it("shows a coming-soon placeholder for a not-yet-built tab", async () => {
    renderPage();
    await userEvent.click(await screen.findByRole("tab", { name: "Rotations" }));
    const placeholder = screen.getByText(/being ported next/i);
    expect(within(placeholder).getByText("Rotations")).toBeInTheDocument();
  });

  it("blocks non-admins", async () => {
    h.useMe.mockReturnValue({ user: { isAdmin: false } });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
  });
});

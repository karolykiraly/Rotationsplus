import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getSpecialties: vi.fn(), updateStudentNeeds: vi.fn() }));
vi.mock("../api", () => ({
  getSpecialties: () => h.getSpecialties(),
  updateStudentNeeds: (id: string, input: unknown) => h.updateStudentNeeds(id, input)
}));

import { StudentNeedsTab } from "./StudentNeedsTab";
import type { StudentDetail } from "../api";

const base = (over: Partial<StudentDetail> = {}): StudentDetail => ({
  id: "s1", firstName: "Hilda", lastName: "Wong", email: "h@x.com", mobilePhone: null,
  academicStatus: "InternationalMedicalGraduate", visaStatus: null, medicalSchool: null,
  medicalSchoolCountry: null, city: null, state: null, status: "Registered", studentOid: null,
  interests: null, preferredSpecialty: null, specialtyLocations: null, customSpecialtyLocation: null, importants: null,
  ...over
});

function renderTab(student: StudentDetail) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <StudentNeedsTab student={student} onSaved={() => {}} />
    </QueryClientProvider>
  );
}

describe("StudentNeedsTab", () => {
  beforeEach(() => {
    h.getSpecialties.mockReset().mockResolvedValue([{ id: "sp1", name: "Cardiology" }]);
    h.updateStudentNeeds.mockReset().mockImplementation((_id, input) => Promise.resolve(base(input as Partial<StudentDetail>)));
  });

  it("renders the medical interests grid and saves a toggled selection", async () => {
    renderTab(base());
    const internal = screen.getByRole("button", { name: "Internal Medicine" });
    expect(internal).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "All Core Rotations" })).toBeInTheDocument();

    await userEvent.click(internal);
    expect(internal).toHaveAttribute("aria-pressed", "true");

    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentNeeds).toHaveBeenCalledWith("s1", expect.objectContaining({ interests: ["Internal Medicine"] }));
    expect(await screen.findByText("Needs saved.")).toBeInTheDocument();
  });

  it("requires the free-text location when 'Other' is selected", async () => {
    renderTab(base());
    await userEvent.click(screen.getByRole("button", { name: /Specialty locations/ }));
    await userEvent.click(screen.getByRole("checkbox", { name: "Other" }));
    // Free-text appears; saving without it is blocked.
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(await screen.findByText(/Enter the specialty location/)).toBeInTheDocument();
    expect(h.updateStudentNeeds).not.toHaveBeenCalled();

    await userEvent.type(screen.getByLabelText("Enter Specialty Location"), "Remote City");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(h.updateStudentNeeds).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ specialtyLocations: ["Other"], customSpecialtyLocation: "Remote City" })
    );
  });

  it("populates 'add from the list' from the specialty catalog for medical students", async () => {
    renderTab(base());
    expect(await screen.findByRole("option", { name: "Cardiology" })).toBeInTheDocument();
    expect(screen.getByText("What are most important to you when finding a clinical rotation?")).toBeInTheDocument();
  });

  it("shows the dental interest set and hides priorities for dental students", () => {
    renderTab(base({ academicStatus: "DentalStudent" }));
    expect(screen.getByRole("button", { name: "General Dentist" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Internal Medicine" })).not.toBeInTheDocument();
    // Priorities section is hidden for the dental track.
    expect(screen.queryByText(/What are most important/)).not.toBeInTheDocument();
    // The catalog isn't fetched for dental (uses the dental list).
    expect(h.getSpecialties).not.toHaveBeenCalled();
  });
});

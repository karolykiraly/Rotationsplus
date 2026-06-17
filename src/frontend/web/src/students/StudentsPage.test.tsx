import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getStudents: vi.fn(),
  getStudent: vi.fn(),
  createStudent: vi.fn(),
  updateStudent: vi.fn(),
  deleteStudent: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getStudents: (params: unknown) => h.getStudents(params),
  getStudent: (id: string) => h.getStudent(id),
  createStudent: (input: unknown) => h.createStudent(input),
  updateStudent: (id: string, input: unknown) => h.updateStudent(id, input),
  deleteStudent: (id: string) => h.deleteStudent(id),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { StudentsPage } from "./StudentsPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const STUDENT_ROW = {
  id: "s1",
  fullName: "Sam Rivera",
  email: "sam@x.com",
  mobilePhone: "+1 555 0100",
  academicStatus: "InternationalMedicalGraduate",
  visaStatus: "NeedsVisaHelp",
  city: "Chicago",
  state: "IL",
  status: "MemberActivated"
};
const STUDENT_DETAIL = {
  id: "s1",
  firstName: "Sam",
  lastName: "Rivera",
  email: "sam@x.com",
  mobilePhone: "+1 555 0100",
  academicStatus: "InternationalMedicalGraduate",
  visaStatus: "NeedsVisaHelp",
  medicalSchool: "Caribbean Med",
  medicalSchoolCountry: "Grenada",
  city: "Chicago",
  state: "IL",
  status: "MemberActivated",
  studentOid: "ciam-oid-1"
};

function newClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } }
  });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <StudentsPage />
    </QueryClientProvider>
  );
}

describe("StudentsPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getStudents.mockResolvedValue([STUDENT_ROW]);
    h.getStudent.mockResolvedValue(STUDENT_DETAIL);
  });

  it("lists students with academic/visa/status labels", async () => {
    renderPage();
    expect(await screen.findByText("Sam Rivera")).toBeInTheDocument();
    expect(screen.getByText("sam@x.com")).toBeInTheDocument();
    expect(screen.getByText("International Medical Graduate")).toBeInTheDocument();
    expect(screen.getByText("Needs help with visa")).toBeInTheDocument();
    // "Activated" renders as the status badge (scoped: it's also a filter-dropdown option).
    expect(screen.getByText("Activated", { selector: ".badge" })).toBeInTheDocument();
  });

  it("renders an em-dash for a student with no visa status or location", async () => {
    h.getStudents.mockResolvedValue([
      { ...STUDENT_ROW, id: "s3", fullName: "No Visa", visaStatus: null, city: null, state: null }
    ]);
    renderPage();

    const row = (await screen.findByText("No Visa")).closest("tr")!;
    // Both the visa cell and the location cell fall back to "—".
    expect(within(row).getAllByText("—")).toHaveLength(2);
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add student" })).not.toBeInTheDocument();
  });

  it("creates a student from the form", async () => {
    h.createStudent.mockResolvedValue({ ...STUDENT_DETAIL, id: "s2" });
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add student" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("First name"), "Pat");
    await userEvent.type(within(dialog).getByLabelText("Last name"), "Morgan");
    await userEvent.type(within(dialog).getByLabelText("Email"), "pat@x.com");
    await userEvent.selectOptions(within(dialog).getByLabelText("Academic status"), "DoStudent");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createStudent).toHaveBeenCalledWith(
      expect.objectContaining({
        firstName: "Pat",
        lastName: "Morgan",
        email: "pat@x.com",
        academicStatus: "DoStudent",
        visaStatus: null,
        status: "Registered"
      })
    );
    expect(await screen.findByText("Added Pat Morgan.")).toBeInTheDocument();
  });

  it("requires first name/email before calling the API", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add student" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("Enter a first name.")).toBeInTheDocument();
    expect(h.createStudent).not.toHaveBeenCalled();
  });

  it("surfaces a server duplicate-email error in the form", async () => {
    h.createStudent.mockRejectedValue(new ApiError(409, "A student with email 'x' already exists."));
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add student" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("First name"), "Dup");
    await userEvent.type(within(dialog).getByLabelText("Last name"), "Licate");
    await userEvent.type(within(dialog).getByLabelText("Email"), "dup@x.com");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText(/already exists/)).toBeInTheDocument();
  });

  it("loads detail and pre-fills the edit form, then updates", async () => {
    h.updateStudent.mockResolvedValue(STUDENT_DETAIL);
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));

    expect(await screen.findByLabelText("First name")).toHaveValue("Sam");
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByLabelText("Academic status")).toHaveValue("InternationalMedicalGraduate");
    expect(within(dialog).getByLabelText(/Visa status/)).toHaveValue("NeedsVisaHelp");
    expect(within(dialog).getByLabelText(/object id/i)).toHaveValue("ciam-oid-1");
    expect(h.getStudent).toHaveBeenCalledWith("s1");

    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));
    expect(h.updateStudent).toHaveBeenCalledWith(
      "s1",
      expect.objectContaining({ firstName: "Sam", academicStatus: "InternationalMedicalGraduate", visaStatus: "NeedsVisaHelp", studentOid: "ciam-oid-1" })
    );
    expect(await screen.findByText("Student updated.")).toBeInTheDocument();
  });

  it("deletes a student after confirmation", async () => {
    h.deleteStudent.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(h.deleteStudent).toHaveBeenCalledWith("s1");
    expect(await screen.findByText(/Deleted/)).toBeInTheDocument();
  });

  it("filters the list by status and re-renders the filtered rows", async () => {
    h.getStudents.mockImplementation((params?: { status?: string }) =>
      Promise.resolve(
        params?.status === "TurnedIntoContact"
          ? [{ ...STUDENT_ROW, id: "s9", fullName: "Dana Cole", status: "TurnedIntoContact" }]
          : [STUDENT_ROW]
      )
    );
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.selectOptions(screen.getByLabelText("Status"), "TurnedIntoContact");

    expect(h.getStudents).toHaveBeenLastCalledWith({ status: "TurnedIntoContact" });
    expect(await screen.findByText("Dana Cole")).toBeInTheDocument();
    expect(screen.queryByText("Sam Rivera")).not.toBeInTheDocument();
  });

  it("shows an error modal when the student detail fails to load", async () => {
    h.getStudent.mockRejectedValue(new ApiError(500, "boom"));
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));

    expect(await screen.findByText(/Couldn.t load student: boom/)).toBeInTheDocument();
  });

  it("shows a page banner when a delete fails", async () => {
    h.deleteStudent.mockRejectedValue(new ApiError(409, "Student is referenced."));
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(await screen.findByText("Student is referenced.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

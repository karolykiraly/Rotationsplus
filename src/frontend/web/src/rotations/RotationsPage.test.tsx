import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

/** Wraps rows in the server's PagedResponse envelope (single page). */
const paged = <T,>(items: T[]) => ({ items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 });

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getRotations: vi.fn(),
  getRotation: vi.fn(),
  createRotation: vi.fn(),
  updateRotation: vi.fn(),
  getProgramCatalog: vi.fn(),
  getStudentOptions: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getRotations: (params: unknown) => h.getRotations(params),
  getRotation: (id: string) => h.getRotation(id),
  createRotation: (input: unknown) => h.createRotation(input),
  updateRotation: (id: string, input: unknown) => h.updateRotation(id, input),
  getProgramCatalog: () => h.getProgramCatalog(),
  getStudentOptions: () => h.getStudentOptions(),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { RotationsPage } from "./RotationsPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const ROTATION_ROW = {
  id: "r1",
  rotationNumber: 1001,
  studentName: "Sam Rivera",
  studentEmail: "sam@x.com",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  preceptorName: "Jane Carter",
  startDate: "2026-07-06",
  endDate: "2026-08-03",
  weeks: 4,
  status: "Active",
  retailAmount: 6000,
  needsVisa: true
};
const ROTATION_DETAIL = {
  id: "r1",
  rotationNumber: 1001,
  programId: "prog1",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  preceptorName: "Jane Carter",
  studentId: "stud1",
  studentName: "Sam Rivera",
  studentEmail: "sam@x.com",
  studentOid: "ciam-oid-1",
  startDate: "2026-07-06",
  endDate: "2026-08-03",
  weeks: 4,
  status: "Active",
  programNumber: 1042,
  retailAmount: 6000,
  paidAmount: 500,
  allowedNextStatuses: ["ToBeEvaluated", "Completed", "Abandoned", "Cancelled"]
};
const PROGRAM = {
  id: "prog1",
  programNumber: 1042,
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  maxStudentsPerRotation: 2,
  minWeeksPerRotation: 4,
  retailAmountPerWeek: 1500,
  preceptorName: "Jane Carter"
};
const STUDENTS = [
  { id: "stud1", fullName: "Sam Rivera", email: "sam@x.com", academicStatus: "InternationalMedicalGraduate", status: "MemberActivated" },
  { id: "stud2", fullName: "Dana Cole", email: "dana@x.com", academicStatus: "MdStudent", status: "Registered" }
];

/** Default: the row lives in the Current section; Historical is empty (so "Sam Rivera" is unambiguous). */
const currentOnly = (params?: { scope?: string }) =>
  Promise.resolve(paged(params?.scope === "current" ? [ROTATION_ROW] : []));

function newClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } }
  });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <RotationsPage />
    </QueryClientProvider>
  );
}

describe("RotationsPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getRotations.mockImplementation(currentOnly);
    h.getRotation.mockResolvedValue(ROTATION_DETAIL);
    h.getProgramCatalog.mockResolvedValue([PROGRAM]);
    h.getStudentOptions.mockResolvedValue(STUDENTS);
  });

  it("renders two sections and requests both scopes", async () => {
    renderPage();
    expect(await screen.findByText("Current Rotations")).toBeInTheDocument();
    expect(screen.getByText("Historical Rotations")).toBeInTheDocument();
    await waitFor(() => {
      const scopes = h.getRotations.mock.calls.map((c) => c[0]?.scope);
      expect(scopes).toContain("current");
      expect(scopes).toContain("historical");
    });
  });

  it("shows the production columns on a Current row (no Specialty/Type/Weeks, no inline Edit/Delete/Refund)", async () => {
    renderPage();
    expect(await screen.findByText("Sam Rivera")).toBeInTheDocument();
    expect(screen.getByText("R1001")).toBeInTheDocument();
    expect(screen.getByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("$6,000")).toBeInTheDocument(); // Retail Amount column
    // Needs Visa checkbox reflects the flag.
    expect(screen.getByLabelText("R1001 needs visa")).toBeChecked();
    // Status text is colour-coded (Active → green ok-text).
    expect(screen.getByText("Active")).toHaveClass("ok-text");
    // Production replaces inline actions with a single View; no Edit/Delete/Refund, no status filter, no Type/Weeks.
    expect(screen.getByRole("button", { name: "View" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Refund" })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Status")).not.toBeInTheDocument();
    expect(screen.queryByText("In person")).not.toBeInTheDocument();
  });

  it("leaves the Needs Visa box unchecked when the student doesn't need a visa", async () => {
    h.getRotations.mockImplementation((params?: { scope?: string }) =>
      Promise.resolve(paged(params?.scope === "current" ? [{ ...ROTATION_ROW, needsVisa: false }] : []))
    );
    renderPage();
    await screen.findByText("Sam Rivera");
    expect(screen.getByLabelText("R1001 needs visa")).not.toBeChecked();
  });

  it("searches a section server-side via its own debounced box (carrying the scope)", async () => {
    h.getRotations.mockImplementation((params?: { scope?: string; q?: string }) =>
      Promise.resolve(paged(
        params?.scope === "current" && !params?.q ? [ROTATION_ROW, { ...ROTATION_ROW, id: "r2", rotationNumber: 2002, studentName: "Dana Cole" }]
          : params?.scope === "current" ? [ROTATION_ROW] : []
      ))
    );
    renderPage();
    await screen.findByText("Dana Cole");

    await userEvent.type(screen.getByLabelText("Search current rotations"), "R1001");

    await waitFor(() => expect(h.getRotations).toHaveBeenLastCalledWith(
      expect.objectContaining({ scope: "current", q: "R1001" })));
    await waitFor(() => expect(screen.queryByText("Dana Cole")).not.toBeInTheDocument());
  });

  it("opens the Selected Rotation panel on View with the money + program fields", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "View" }));

    const panel = await screen.findByLabelText("Selected rotation");
    expect(h.getRotation).toHaveBeenCalledWith("r1");
    expect(within(panel).getByText("Paid Amount")).toBeInTheDocument();
    expect(within(panel).getByText("$500")).toBeInTheDocument();      // paid amount
    expect(within(panel).getByText("Rotation Cost")).toBeInTheDocument();
    expect(within(panel).getByText("$6,000")).toBeInTheDocument();    // rotation cost
    expect(within(panel).getByText("IP1042")).toBeInTheDocument();    // Program ID (program code)
    expect(within(panel).getByLabelText("Current status")).toHaveValue("Active");
  });

  it("limits the panel status dropdown to the current status plus allowed transitions (no Refunded)", async () => {
    h.getRotation.mockResolvedValue({ ...ROTATION_DETAIL, allowedNextStatuses: ["ToBeEvaluated", "Completed", "Refunded"] });
    renderPage();
    await screen.findByText("Sam Rivera");
    await userEvent.click(screen.getByRole("button", { name: "View" }));

    const panel = await screen.findByLabelText("Selected rotation");
    const options = within(within(panel).getByLabelText("Current status") as HTMLSelectElement)
      .getAllByRole("option").map((o) => o.textContent);
    expect(options).toEqual(["Active", "To be evaluated", "Completed"]);
    expect(options).not.toContain("Refunded");
  });

  it("reveals the program picker on Replace and the date inputs on Change", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");
    await userEvent.click(screen.getByRole("button", { name: "View" }));
    const panel = await screen.findByLabelText("Selected rotation");

    await userEvent.click(within(panel).getByRole("button", { name: "Replace" }));
    expect(within(panel).getByLabelText("Replace program")).toBeInTheDocument();

    await userEvent.click(within(panel).getByRole("button", { name: "Change" }));
    expect(within(panel).getByLabelText("Start date")).toHaveValue("2026-07-06");
    expect(within(panel).getByLabelText("End date")).toHaveValue("2026-08-03");
  });

  it("saves the panel via the update endpoint and shows a banner", async () => {
    h.updateRotation.mockResolvedValue(ROTATION_DETAIL);
    renderPage();
    await screen.findByText("Sam Rivera");
    await userEvent.click(screen.getByRole("button", { name: "View" }));
    const panel = await screen.findByLabelText("Selected rotation");

    await userEvent.selectOptions(within(panel).getByLabelText("Current status"), "Completed");
    await userEvent.click(within(panel).getByRole("button", { name: "Save" }));

    expect(h.updateRotation).toHaveBeenCalledWith("r1", {
      programId: "prog1",
      studentId: "stud1",
      startDate: "2026-07-06",
      endDate: "2026-08-03",
      status: "Completed"
    });
    expect(await screen.findByText("Rotation updated.")).toBeInTheDocument();
  });

  it("surfaces a server error from the panel save without closing it", async () => {
    h.updateRotation.mockRejectedValue(new ApiError(400, "Can't change a rotation from Active to Pending."));
    renderPage();
    await screen.findByText("Sam Rivera");
    await userEvent.click(screen.getByRole("button", { name: "View" }));
    const panel = await screen.findByLabelText("Selected rotation");

    await userEvent.click(within(panel).getByRole("button", { name: "Save" }));

    expect(await within(panel).findByText(/Can't change a rotation/)).toBeInTheDocument();
  });

  it("shows a loading/error state for the panel while detail loads / fails", async () => {
    h.getRotation.mockRejectedValue(new ApiError(500, "boom"));
    renderPage();
    await screen.findByText("Sam Rivera");
    await userEvent.click(screen.getByRole("button", { name: "View" }));
    expect(await screen.findByText(/Couldn.t load rotation: boom/)).toBeInTheDocument();
  });

  it("adds a rotation via the Add New Rotation modal", async () => {
    h.createRotation.mockResolvedValue({ ...ROTATION_DETAIL, id: "r2" });
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add New Rotation" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Program"), "prog1");
    await userEvent.selectOptions(within(dialog).getByLabelText("Student"), "stud2");
    await userEvent.type(within(dialog).getByLabelText("Start date"), "2026-09-07");
    await userEvent.type(within(dialog).getByLabelText("End date"), "2026-10-05");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createRotation).toHaveBeenCalledWith({
      programId: "prog1",
      studentId: "stud2",
      startDate: "2026-09-07",
      endDate: "2026-10-05",
      status: "Pending"
    });
    expect(await screen.findByText(/Booked/)).toBeInTheDocument();
  });

  it("shows the empty state in both sections when there are no rows", async () => {
    h.getRotations.mockResolvedValue(paged([]));
    renderPage();
    await waitFor(() => expect(screen.getAllByText("There is no data available.")).toHaveLength(2));
  });

  it("applies the Filter modal to both sections (status + needs-visa + rotation number) and shows the count", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Filter rotations" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Status"), "Completed");
    await userEvent.click(within(dialog).getByLabelText("Needs Visa"));
    await userEvent.type(within(dialog).getByLabelText("Rotation Number"), "1001");
    await userEvent.click(within(dialog).getByRole("button", { name: "Apply filters" }));

    await waitFor(() => expect(h.getRotations).toHaveBeenCalledWith(
      expect.objectContaining({ scope: "current", status: "Completed", needsVisa: true, rotationNumber: 1001 })));
    // The historical section gets the same filter.
    expect(h.getRotations).toHaveBeenCalledWith(
      expect.objectContaining({ scope: "historical", status: "Completed", needsVisa: true }));
    // The filter-count badge reflects the 3 active filters.
    expect(screen.getByText("3")).toBeInTheDocument();
  });

  it("clears the rotation filter back to an unfiltered query", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");
    await userEvent.click(screen.getByRole("button", { name: "Filter rotations" }));
    let dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByLabelText("Needs Visa"));
    await userEvent.click(within(dialog).getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(h.getRotations).toHaveBeenCalledWith(expect.objectContaining({ needsVisa: true })));

    await userEvent.click(screen.getByRole("button", { name: "Filter rotations" }));
    dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Clear filters" }));

    // After clearing, the most recent query for each section carries no needs-visa filter (ordering of the
    // two sections' refetches isn't deterministic, so find the latest current-scope call rather than assume).
    await waitFor(() => {
      const latestCurrent = [...h.getRotations.mock.calls].reverse().find((c) => c[0]?.scope === "current");
      expect(latestCurrent?.[0]?.needsVisa).toBeUndefined();
    });
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add New Rotation" })).not.toBeInTheDocument();
  });
});

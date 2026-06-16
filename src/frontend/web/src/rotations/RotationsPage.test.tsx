import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getRotations: vi.fn(),
  getRotation: vi.fn(),
  createRotation: vi.fn(),
  updateRotation: vi.fn(),
  deleteRotation: vi.fn(),
  getPrograms: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getRotations: (params: unknown) => h.getRotations(params),
  getRotation: (id: string) => h.getRotation(id),
  createRotation: (input: unknown) => h.createRotation(input),
  updateRotation: (id: string, input: unknown) => h.updateRotation(id, input),
  deleteRotation: (id: string) => h.deleteRotation(id),
  getPrograms: () => h.getPrograms(),
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
  studentName: "Sam Rivera",
  studentEmail: "sam@x.com",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  preceptorName: "Jane Carter",
  startDate: "2026-07-06",
  endDate: "2026-08-03",
  weeks: 4,
  status: "Active"
};
const ROTATION_DETAIL = {
  id: "r1",
  programId: "prog1",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  preceptorName: "Jane Carter",
  studentName: "Sam Rivera",
  studentEmail: "sam@x.com",
  studentOid: "ciam-oid-1",
  startDate: "2026-07-06",
  endDate: "2026-08-03",
  weeks: 4,
  status: "Active"
};
const PROGRAM = {
  id: "prog1",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  maxStudentsPerRotation: 2,
  minWeeksPerRotation: 4,
  retailAmountPerWeek: 1500,
  preceptorName: "Jane Carter"
};

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
    h.getRotations.mockResolvedValue([ROTATION_ROW]);
    h.getRotation.mockResolvedValue(ROTATION_DETAIL);
    h.getPrograms.mockResolvedValue([PROGRAM]);
  });

  it("lists rotations with student, type label, weeks and status", async () => {
    renderPage();
    expect(await screen.findByText("Sam Rivera")).toBeInTheDocument();
    expect(screen.getByText("sam@x.com")).toBeInTheDocument();
    expect(screen.getByText("In person")).toBeInTheDocument();
    expect(screen.getByText("Jane Carter")).toBeInTheDocument();
    // Status "Active" renders as its badge (scoped: "Active" is also a filter-dropdown option).
    expect(screen.getByText("Active", { selector: ".badge" })).toBeInTheDocument();
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add rotation" })).not.toBeInTheDocument();
  });

  it("creates a rotation from the form", async () => {
    h.createRotation.mockResolvedValue({ ...ROTATION_DETAIL, id: "r2" });
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add rotation" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Program"), "prog1");
    await userEvent.type(within(dialog).getByLabelText("Student name"), "Pat Morgan");
    await userEvent.type(within(dialog).getByLabelText("Student email"), "pat@x.com");
    await userEvent.type(within(dialog).getByLabelText("Start date"), "2026-09-07");
    await userEvent.type(within(dialog).getByLabelText("End date"), "2026-10-05");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createRotation).toHaveBeenCalledWith(
      expect.objectContaining({
        programId: "prog1",
        studentName: "Pat Morgan",
        studentEmail: "pat@x.com",
        studentOid: null,
        startDate: "2026-09-07",
        endDate: "2026-10-05",
        status: "Pending"
      })
    );
    expect(await screen.findByText("Booked Pat Morgan.")).toBeInTheDocument();
  });

  it("requires a program before calling the API", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add rotation" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Student name"), "Pat Morgan");
    await userEvent.type(within(dialog).getByLabelText("Student email"), "pat@x.com");
    await userEvent.type(within(dialog).getByLabelText("Start date"), "2026-09-07");
    await userEvent.type(within(dialog).getByLabelText("End date"), "2026-10-05");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("Select a program.")).toBeInTheDocument();
    expect(h.createRotation).not.toHaveBeenCalled();
  });

  it("rejects an end date on or before the start date (no API call)", async () => {
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add rotation" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Program"), "prog1");
    await userEvent.type(within(dialog).getByLabelText("Student name"), "Pat Morgan");
    await userEvent.type(within(dialog).getByLabelText("Student email"), "pat@x.com");
    await userEvent.type(within(dialog).getByLabelText("Start date"), "2026-09-07");
    await userEvent.type(within(dialog).getByLabelText("End date"), "2026-09-07");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("End date must be after the start date.")).toBeInTheDocument();
    expect(h.createRotation).not.toHaveBeenCalled();
  });

  it("surfaces a server validation error in the form", async () => {
    h.createRotation.mockRejectedValue(new ApiError(400, "Program does not exist."));
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.click(screen.getByRole("button", { name: "Add rotation" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Program"), "prog1");
    await userEvent.type(within(dialog).getByLabelText("Student name"), "Pat Morgan");
    await userEvent.type(within(dialog).getByLabelText("Student email"), "pat@x.com");
    await userEvent.type(within(dialog).getByLabelText("Start date"), "2026-09-07");
    await userEvent.type(within(dialog).getByLabelText("End date"), "2026-10-05");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("Program does not exist.")).toBeInTheDocument();
  });

  it("loads detail and pre-fills the edit form, then updates", async () => {
    h.updateRotation.mockResolvedValue(ROTATION_DETAIL);
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));

    expect(await screen.findByLabelText("Program")).toHaveValue("prog1");
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByLabelText("Student name")).toHaveValue("Sam Rivera");
    expect(within(dialog).getByLabelText("Start date")).toHaveValue("2026-07-06");
    expect(within(dialog).getByLabelText("Status")).toHaveValue("Active");
    expect(within(dialog).getByLabelText(/object id/i)).toHaveValue("ciam-oid-1");
    expect(h.getRotation).toHaveBeenCalledWith("r1");

    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));
    expect(h.updateRotation).toHaveBeenCalledWith(
      "r1",
      expect.objectContaining({ programId: "prog1", studentName: "Sam Rivera", status: "Active", studentOid: "ciam-oid-1" })
    );
    expect(await screen.findByText("Rotation updated.")).toBeInTheDocument();
  });

  it("deletes a rotation after confirmation", async () => {
    h.deleteRotation.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(h.deleteRotation).toHaveBeenCalledWith("r1");
    expect(await screen.findByText(/Deleted/)).toBeInTheDocument();
  });

  it("filters the list by status and re-renders the filtered rows", async () => {
    // The Completed filter returns a different student, so we assert the rendered list actually swaps
    // (not just that the request carried the right status).
    h.getRotations.mockImplementation((params?: { status?: string }) =>
      Promise.resolve(
        params?.status === "Completed"
          ? [{ ...ROTATION_ROW, id: "r9", studentName: "Dana Cole", status: "Completed" }]
          : [ROTATION_ROW]
      )
    );
    renderPage();
    await screen.findByText("Sam Rivera");

    await userEvent.selectOptions(screen.getByLabelText("Status"), "Completed");

    expect(h.getRotations).toHaveBeenLastCalledWith({ status: "Completed" });
    expect(await screen.findByText("Dana Cole")).toBeInTheDocument();
    expect(screen.queryByText("Sam Rivera")).not.toBeInTheDocument();
  });

  it("shows an error modal when the rotation detail fails to load", async () => {
    h.getRotation.mockRejectedValue(new ApiError(500, "boom"));
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));

    expect(await screen.findByText(/Couldn.t load rotation: boom/)).toBeInTheDocument();
  });

  it("shows a page banner when a delete fails", async () => {
    h.deleteRotation.mockRejectedValue(new ApiError(409, "Rotation is locked."));
    renderPage();
    await screen.findByText("Sam Rivera");

    const row = screen.getByText("Sam Rivera").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(await screen.findByText("Rotation is locked.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

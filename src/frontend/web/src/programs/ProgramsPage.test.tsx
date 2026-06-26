import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getPrograms: vi.fn(),
  getProgram: vi.fn(),
  createProgram: vi.fn(),
  updateProgram: vi.fn(),
  deleteProgram: vi.fn(),
  getSpecialties: vi.fn(),
  getPreceptorOptions: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getPrograms: () => h.getPrograms(),
  getProgram: (id: string) => h.getProgram(id),
  createProgram: (input: unknown) => h.createProgram(input),
  updateProgram: (id: string, input: unknown) => h.updateProgram(id, input),
  deleteProgram: (id: string) => h.deleteProgram(id),
  getSpecialties: () => h.getSpecialties(),
  getPreceptorOptions: () => h.getPreceptorOptions(),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { ProgramsPage } from "./ProgramsPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const PROGRAM_ROW = {
  id: "p1",
  programNumber: 1001,
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  maxStudentsPerRotation: 2,
  minWeeksPerRotation: 4,
  retailAmountPerWeek: 1500,
  preceptorName: "Jane Carter",
  city: "Los Angeles",
  state: "CA",
  isOpen: false,
  tags: ["Inpatient"]
};
const PROGRAM_DETAIL = {
  id: "p1",
  specialtyId: "s1",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  maxStudentsPerRotation: 2,
  minWeeksPerRotation: 4,
  retailAmountPerWeek: 1500,
  weeklyHonorarium: 500,
  description: "Hands-on rotation.",
  preceptorId: "d1",
  preceptorName: "Jane Carter",
  isOpen: false,
  programNumber: 1001,
  city: "Los Angeles",
  state: "CA",
  tags: ["Inpatient"]
};

function newClient() {
  // retryOnMount:false keeps a just-written detail cache stable on re-edit (stale-cache regression test).
  return new QueryClient({
    defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } }
  });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <ProgramsPage />
    </QueryClientProvider>
  );
}

describe("ProgramsPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getPrograms.mockResolvedValue([PROGRAM_ROW]);
    h.getProgram.mockResolvedValue(PROGRAM_DETAIL);
    h.getSpecialties.mockResolvedValue([
      { id: "s1", name: "Internal Medicine" },
      { id: "s2", name: "Pediatrics" }
    ]);
    h.getPreceptorOptions.mockResolvedValue([{ id: "d1", fullName: "Jane Carter", email: "j@x", primarySpecialtyName: "IM", status: "MemberActivated" }]);
  });

  it("lists programs in the active type tab with the retail amount", async () => {
    renderPage();
    // Default tab is InPerson; the row's labelled fields + retail render (name/ID/location are placeholders).
    expect(await screen.findByText("Program ID")).toBeInTheDocument();
    expect(screen.getByText("$1,500")).toBeInTheDocument();
    expect(screen.getAllByText("Internal Medicine").length).toBeGreaterThan(0);
  });

  it("filters to an empty tab and by search text", async () => {
    renderPage();
    await screen.findByText("Program ID");
    // Consultation tab has no matching program -> empty state.
    await userEvent.click(screen.getByRole("tab", { name: "Consultation" }));
    expect(await screen.findByText("There is no data available.")).toBeInTheDocument();
    // Back to InPerson; a non-matching search clears the row.
    await userEvent.click(screen.getByRole("tab", { name: "InPerson" }));
    await screen.findByText("Program ID");
    await userEvent.type(screen.getByLabelText("Search for programs"), "zzzzz");
    expect(await screen.findByText("There is no data available.")).toBeInTheDocument();
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add program" })).not.toBeInTheDocument();
  });

  it("creates a program from the form defaults plus a chosen specialty", async () => {
    h.createProgram.mockResolvedValue({ ...PROGRAM_DETAIL, id: "p2" });
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Add program" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Specialty"), "s2");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createProgram).toHaveBeenCalledWith(
      expect.objectContaining({
        specialtyId: "s2",
        programType: "InPerson",
        maxStudentsPerRotation: 1,
        minWeeksPerRotation: 4,
        retailAmountPerWeek: 0,
        weeklyHonorarium: 0,
        description: null,
        preceptorId: null
      })
    );
    expect(await screen.findByText("Program created.")).toBeInTheDocument();
  });

  it("requires a specialty before calling the API", async () => {
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Add program" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("Select a specialty.")).toBeInTheDocument();
    expect(h.createProgram).not.toHaveBeenCalled();
  });

  it("surfaces a server validation error in the form", async () => {
    h.createProgram.mockRejectedValue(new ApiError(400, "Specialty does not exist."));
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Add program" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Specialty"), "s2");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    // Scoped to the dialog: the conflict must surface in the form banner, keeping the modal open.
    expect(await within(dialog).findByText("Specialty does not exist.")).toBeInTheDocument();
  });

  it("loads detail and pre-fills the edit form, then updates", async () => {
    h.updateProgram.mockResolvedValue(PROGRAM_DETAIL);
    renderPage();
    await screen.findByText("Program ID");

    const row = screen.getAllByText("Internal Medicine")[0].closest("tr")!;
    await userEvent.click(row);

    // Detail is fetched and every field pre-fills (incl. the null-coalesced preceptor + description).
    expect(await screen.findByLabelText("Specialty")).toHaveValue("s1");
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByLabelText("Max students / rotation")).toHaveValue(2);
    expect(within(dialog).getByLabelText("Retail / week ($)")).toHaveValue(1500);
    expect(within(dialog).getByLabelText(/Preceptor/)).toHaveValue("d1");
    expect(within(dialog).getByLabelText(/Description/)).toHaveValue("Hands-on rotation.");
    expect(h.getProgram).toHaveBeenCalledWith("p1");

    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));
    expect(h.updateProgram).toHaveBeenCalledWith(
      "p1",
      expect.objectContaining({ specialtyId: "s1", weeklyHonorarium: 500, preceptorId: "d1", description: "Hands-on rotation." })
    );
    expect(await screen.findByText("Program updated.")).toBeInTheDocument();
  });

  it("deletes a program after confirmation", async () => {
    h.deleteProgram.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText("Program ID");

    const row = screen.getAllByText("Internal Medicine")[0].closest("tr")!;
    await userEvent.click(row); // row opens the edit modal
    await userEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete" }));
    // The edit modal's Delete hands off to the confirm dialog.
    await userEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete" }));

    expect(h.deleteProgram).toHaveBeenCalledWith("p1");
    expect(await screen.findByText(/Deleted/)).toBeInTheDocument();
  });

  it("rejects a sub-cent money amount with the zod message (no API call)", async () => {
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Add program" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Specialty"), "s2");
    const retail = within(dialog).getByLabelText("Retail / week ($)");
    await userEvent.clear(retail);
    await userEvent.type(retail, "10.999");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("At most 2 decimal places.")).toBeInTheDocument();
    expect(h.createProgram).not.toHaveBeenCalled();
  });

  it("shows an error modal when the program detail fails to load", async () => {
    h.getProgram.mockRejectedValue(new ApiError(500, "boom"));
    renderPage();
    await screen.findByText("Program ID");

    const row = screen.getAllByText("Internal Medicine")[0].closest("tr")!;
    await userEvent.click(row);

    expect(await screen.findByText(/Couldn.t load program: boom/)).toBeInTheDocument();
  });

  it("shows a page banner when a delete fails", async () => {
    h.deleteProgram.mockRejectedValue(new ApiError(409, "Program is in use."));
    renderPage();
    await screen.findByText("Program ID");

    const row = screen.getAllByText("Internal Medicine")[0].closest("tr")!;
    await userEvent.click(row); // row opens the edit modal
    await userEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete" }));
    await userEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Delete" }));

    expect(await screen.findByText("Program is in use.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("re-edits with fresh detail after an update (no stale cache)", async () => {
    h.updateProgram.mockResolvedValue({ ...PROGRAM_DETAIL, maxStudentsPerRotation: 9 });
    renderPage();
    await screen.findByText("Program ID");

    // First edit + save.
    let row = screen.getAllByText("Internal Medicine")[0].closest("tr")!;
    await userEvent.click(row);
    expect(await screen.findByLabelText("Max students / rotation")).toHaveValue(2);
    await userEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Save" }));
    await screen.findByText("Program updated.");

    // Re-open edit — must show the server's updated value (9), not the cached pre-save 2.
    row = screen.getAllByText("Internal Medicine")[0].closest("tr")!;
    await userEvent.click(row);
    expect(await screen.findByLabelText("Max students / rotation")).toHaveValue(9);
  });
});

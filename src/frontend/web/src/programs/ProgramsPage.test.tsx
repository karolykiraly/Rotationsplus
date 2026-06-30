import { describe, it, expect, beforeEach, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const paged = <T,>(items: T[]) => ({ items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 });

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getPrograms: vi.fn(),
  getProgram: vi.fn(),
  createProgram: vi.fn(),
  updateProgram: vi.fn(),
  deleteProgram: vi.fn(),
  getSpecialties: vi.fn(),
  getPreceptorOptions: vi.fn(),
  getProgramLocations: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getPrograms: (params: unknown) => h.getPrograms(params),
  getProgram: (id: string) => h.getProgram(id),
  createProgram: (input: unknown) => h.createProgram(input),
  updateProgram: (id: string, input: unknown) => h.updateProgram(id, input),
  deleteProgram: (id: string) => h.deleteProgram(id),
  getSpecialties: () => h.getSpecialties(),
  getPreceptorOptions: () => h.getPreceptorOptions(),
  getProgramLocations: () => h.getProgramLocations(),
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
  weeklyHonorarium: 500,
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
    h.getPrograms.mockResolvedValue(paged([PROGRAM_ROW]));
    h.getProgram.mockResolvedValue(PROGRAM_DETAIL);
    h.getSpecialties.mockResolvedValue([
      { id: "s1", name: "Internal Medicine" },
      { id: "s2", name: "Pediatrics" }
    ]);
    h.getPreceptorOptions.mockResolvedValue([{ id: "d1", fullName: "Jane Carter", email: "j@x", primarySpecialtyName: "IM", status: "MemberActivated" }]);
    h.getProgramLocations.mockResolvedValue(["Houston, TX", "Los Angeles, CA"]);
  });

  it("lists programs with a derived name, a distinct specialty, and the honorarium under Retail Amount", async () => {
    renderPage();
    await screen.findByText("Program ID");
    // Program Name column derives "{Specialty} Physician"; the Specialty column shows the bare specialty
    // (no longer a duplicate). The "Retail Amount" column shows the weekly honorarium (matching production).
    expect(screen.getByText("Internal Medicine Physician")).toBeInTheDocument();
    expect(screen.getByText("Internal Medicine")).toBeInTheDocument();
    expect(screen.getByText("$500")).toBeInTheDocument();
    expect(screen.queryByText("$1,500")).not.toBeInTheDocument(); // retail value is no longer the column shown
  });

  it("shows the program's own name when set (not the derived default), and pre-fills it for edit", async () => {
    h.getPrograms.mockResolvedValue(paged([{ ...PROGRAM_ROW, programName: "Hospitalist Internal Medicine" }]));
    h.getProgram.mockResolvedValue({ ...PROGRAM_DETAIL, programName: "Hospitalist Internal Medicine" });
    renderPage();
    await screen.findByText("Program ID");

    // The Program Name column shows the explicit name; the derived "{Specialty} Physician" is NOT used.
    expect(screen.getByText("Hospitalist Internal Medicine")).toBeInTheDocument();
    expect(screen.queryByText("Internal Medicine Physician")).not.toBeInTheDocument();

    // Opening the row pre-fills the Program name field with the stored value.
    await userEvent.click(screen.getByText("Hospitalist Internal Medicine").closest("tr")!);
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByLabelText(/Program name/)).toHaveValue("Hospitalist Internal Medicine");
  });

  it("sends the trimmed program name on create (null when blank)", async () => {
    h.createProgram.mockResolvedValue({ ...PROGRAM_DETAIL, id: "p2" });
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Add program" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.selectOptions(within(dialog).getByLabelText("Specialty"), "s2");
    await userEvent.type(within(dialog).getByLabelText(/Program name/), "  Peds Hospitalist  ");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createProgram).toHaveBeenCalledWith(expect.objectContaining({ programName: "Peds Hospitalist" }));
  });

  it("renders a dash under Retail Amount when the honorarium is absent (e.g. a customer-stripped row)", async () => {
    h.getPrograms.mockResolvedValue(paged([{ ...PROGRAM_ROW, weeklyHonorarium: null }]));
    renderPage();
    await screen.findByText("Program ID");
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("drives the program-type tab and search server-side", async () => {
    // The server answers per params: the InPerson tab (default) returns the row; the Consultation tab
    // (Consultation + ConsultationSub) and any q return empty here.
    h.getPrograms.mockImplementation((params?: { programType?: string[]; q?: string }) => {
      const onInPerson = !params?.programType || params.programType.includes("InPerson");
      const noSearch = !params?.q;
      return Promise.resolve(paged(onInPerson && noSearch ? [PROGRAM_ROW] : []));
    });
    renderPage();
    await screen.findByText("Program ID");

    // Consultation tab requests both Consultation variants and shows the empty state.
    await userEvent.click(screen.getByRole("tab", { name: "Consultation" }));
    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(
      expect.objectContaining({ programType: ["Consultation", "ConsultationSub"] })));
    expect(await screen.findByText("There is no data available.")).toBeInTheDocument();

    // Back to InPerson; a (debounced) non-matching search requests q and clears the row.
    await userEvent.click(screen.getByRole("tab", { name: "InPerson" }));
    await screen.findByText("Program ID");
    await userEvent.type(screen.getByLabelText("Search for programs"), "zzzzz");
    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(expect.objectContaining({ q: "zzzzz" })));
    expect(await screen.findByText("There is no data available.")).toBeInTheDocument();
  });

  it("applies the Filter modal (specialty + instant approval + a tag) to the program query and shows the count", async () => {
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Filter programs" }));
    const dialog = await screen.findByRole("dialog");
    // The production-faithful modal uses checkbox lists, not selects: a single-select Specialty list,
    // an Approval Type group, and the "Clinical Needs" tag grid.
    await userEvent.click(within(dialog).getByRole("checkbox", { name: "Pediatrics" }));
    await userEvent.click(within(dialog).getByRole("checkbox", { name: "Yes (Instant Approval)" }));
    await userEvent.click(within(dialog).getByRole("checkbox", { name: "Research" }));
    await userEvent.click(within(dialog).getByRole("button", { name: "Apply filters" }));

    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(
      expect.objectContaining({ specialtyId: "s2", instantApproval: true, tags: ["Research"] })));
    expect(screen.getByText("3")).toBeInTheDocument(); // filter-count badge (specialty + approval + tag = 3)
  });

  it("keeps filters independent per tab (legacy programFilter0..4 parity)", async () => {
    renderPage();
    await screen.findByText("Program ID");

    // Apply a specialty filter on the InPerson tab.
    await userEvent.click(screen.getByRole("button", { name: "Filter programs" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("checkbox", { name: "Pediatrics" }));
    await userEvent.click(within(dialog).getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(expect.objectContaining({ specialtyId: "s2" })));

    // Switch to TeleRotation — its filter is independent, so the InPerson specialty does NOT carry over.
    await userEvent.click(screen.getByRole("tab", { name: "TeleRotation" }));
    await waitFor(() => {
      const last = h.getPrograms.mock.calls.at(-1)![0];
      expect(last.programType).toEqual(["TeleRotation"]);
      expect(last.specialtyId).toBeUndefined();
    });

    // Back to InPerson — its filter is remembered.
    await userEvent.click(screen.getByRole("tab", { name: "InPerson" }));
    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(expect.objectContaining({ specialtyId: "s2" })));
  });

  it("sends honorarium bounds only when the amount range narrows from the full 0–15000", async () => {
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Filter programs" }));
    const dialog = await screen.findByRole("dialog");

    // Narrowing only the max sends honorariumMax but NOT honorariumMin (lower bound still 0).
    // fireEvent.change drives the controlled+clamped number input deterministically.
    fireEvent.change(within(dialog).getByRole("spinbutton", { name: "Retail amount maximum" }), { target: { value: "8000" } });
    await userEvent.click(within(dialog).getByRole("button", { name: "Apply filters" }));
    await waitFor(() => {
      const last = h.getPrograms.mock.calls.at(-1)![0];
      expect(last.honorariumMax).toBe(8000);
      expect(last.honorariumMin).toBeUndefined();
    });
    // Lower bound at 0 → the amount filter does NOT count toward the badge (legacy getFilterCount rule).
    expect(document.querySelector(".filter-count")).toBeNull();

    // Raising the min too sends both bounds and now counts.
    await userEvent.click(screen.getByRole("button", { name: "Filter programs" }));
    const dialog2 = await screen.findByRole("dialog");
    await userEvent.clear(within(dialog2).getByRole("spinbutton", { name: "Retail amount minimum" }));
    await userEvent.type(within(dialog2).getByRole("spinbutton", { name: "Retail amount minimum" }), "1000");
    await userEvent.click(within(dialog2).getByRole("button", { name: "Apply filters" }));
    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(
      expect.objectContaining({ honorariumMin: 1000, honorariumMax: 8000 })));
    expect(screen.getByText("1", { selector: ".filter-count" })).toBeInTheDocument(); // amount filter now badged
  });

  it("renders uppercase YES / NO for Instant Approval (production casing)", async () => {
    h.getPrograms.mockResolvedValue(paged([{ ...PROGRAM_ROW, isOpen: true }]));
    renderPage();
    await screen.findByText("Program ID");
    expect(screen.getByText("YES")).toBeInTheDocument();
    expect(screen.queryByText("Yes")).not.toBeInTheDocument();
  });

  it("shows the location dropdown only on the InPerson tabs and applies a chosen city", async () => {
    h.getPrograms.mockImplementation((params?: { city?: string }) => Promise.resolve(paged(params?.city ? [] : [PROGRAM_ROW])));
    renderPage();
    await screen.findByText("Program ID");

    await userEvent.click(screen.getByRole("button", { name: "Filter programs" }));
    const dialog = await screen.findByRole("dialog");
    // Location dropdown is present on InPerson (tab 0) and lists the distinct "City, State" values.
    await userEvent.selectOptions(within(dialog).getByLabelText("Location: City/State"), "Houston, TX");
    await userEvent.click(within(dialog).getByRole("button", { name: "Apply filters" }));

    await waitFor(() => expect(h.getPrograms).toHaveBeenLastCalledWith(
      expect.objectContaining({ city: "Houston, TX" })));

    // On the Consultation tab the legacy modal hides the location filter.
    await userEvent.click(screen.getByRole("tab", { name: "Consultation" }));
    await userEvent.click(screen.getByRole("button", { name: "Filter programs" }));
    const dialog2 = await screen.findByRole("dialog");
    expect(within(dialog2).queryByLabelText("Location: City/State")).not.toBeInTheDocument();
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

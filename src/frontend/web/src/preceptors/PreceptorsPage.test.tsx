import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getPreceptors: vi.fn(),
  getPreceptor: vi.fn(),
  createPreceptor: vi.fn(),
  updatePreceptor: vi.fn(),
  deletePreceptor: vi.fn(),
  getSpecialties: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getPreceptors: () => h.getPreceptors(),
  getPreceptor: (id: string) => h.getPreceptor(id),
  createPreceptor: (input: unknown) => h.createPreceptor(input),
  updatePreceptor: (id: string, input: unknown) => h.updatePreceptor(id, input),
  deletePreceptor: (id: string) => h.deletePreceptor(id),
  getSpecialties: () => h.getSpecialties(),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { PreceptorsPage } from "./PreceptorsPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const ROW = {
  id: "pr1",
  fullName: "Jane Carter",
  email: "jane@x.com",
  primarySpecialtyName: "Internal Medicine",
  city: "Chicago",
  state: "IL",
  status: "MemberActivated"
};
const DETAIL = {
  id: "pr1",
  firstName: "Jane",
  lastName: "Carter",
  email: "jane@x.com",
  primarySpecialtyId: "s1",
  primarySpecialtyName: "Internal Medicine",
  medicalLicenseNumber: "MD123",
  licenseState: "IL",
  city: "Chicago",
  state: "IL",
  status: "MemberActivated",
  bio: "Experienced."
};

function newClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } }
  });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <PreceptorsPage />
    </QueryClientProvider>
  );
}

async function fillNew(dialog: HTMLElement) {
  await userEvent.type(within(dialog).getByLabelText("First name"), "Omar");
  await userEvent.type(within(dialog).getByLabelText("Last name"), "Reyes");
  await userEvent.type(within(dialog).getByLabelText("Email"), "omar@x.com");
  await userEvent.selectOptions(within(dialog).getByLabelText("Primary specialty"), "s2");
}

describe("PreceptorsPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getPreceptors.mockResolvedValue([ROW]);
    h.getPreceptor.mockResolvedValue(DETAIL);
    h.getSpecialties.mockResolvedValue([
      { id: "s1", name: "Internal Medicine" },
      { id: "s2", name: "Pediatrics" }
    ]);
  });

  it("lists preceptors with status label and location", async () => {
    renderPage();
    expect(await screen.findByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("jane@x.com")).toBeInTheDocument();
    expect(screen.getByText("Activated")).toBeInTheDocument();
    expect(screen.getByText("Chicago, IL")).toBeInTheDocument();
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add preceptor" })).not.toBeInTheDocument();
  });

  it("creates a preceptor, mapping blank optionals to null", async () => {
    h.createPreceptor.mockResolvedValue({ ...DETAIL, id: "pr2" });
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Add preceptor" }));
    const dialog = await screen.findByRole("dialog");
    await fillNew(dialog);
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createPreceptor).toHaveBeenCalledWith({
      firstName: "Omar",
      lastName: "Reyes",
      email: "omar@x.com",
      primarySpecialtyId: "s2",
      status: "Registered",
      medicalLicenseNumber: null,
      licenseState: null,
      city: null,
      state: null,
      bio: null
    });
    expect(await screen.findByText(/Added Omar Reyes/)).toBeInTheDocument();
  });

  it("validates required fields before calling the API", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Add preceptor" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("First name is required.")).toBeInTheDocument();
    expect(h.createPreceptor).not.toHaveBeenCalled();
  });

  it("rejects an invalid email with the zod message", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Add preceptor" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("First name"), "Omar");
    await userEvent.type(within(dialog).getByLabelText("Last name"), "Reyes");
    await userEvent.type(within(dialog).getByLabelText("Email"), "not-an-email");
    await userEvent.selectOptions(within(dialog).getByLabelText("Primary specialty"), "s2");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("Enter a valid email.")).toBeInTheDocument();
    expect(h.createPreceptor).not.toHaveBeenCalled();
  });

  it("surfaces a duplicate-email conflict in the form", async () => {
    h.createPreceptor.mockRejectedValue(new ApiError(409, "A preceptor with email 'omar@x.com' already exists."));
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Add preceptor" }));
    const dialog = await screen.findByRole("dialog");
    await fillNew(dialog);
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText(/already exists/)).toBeInTheDocument();
  });

  it("loads detail and pre-fills the edit form, then updates", async () => {
    h.updatePreceptor.mockResolvedValue(DETAIL);
    renderPage();
    await screen.findByText("Jane Carter");

    const row = screen.getByText("Jane Carter").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));

    expect(await screen.findByLabelText("First name")).toHaveValue("Jane");
    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByLabelText("Email")).toHaveValue("jane@x.com");
    expect(within(dialog).getByLabelText("Primary specialty")).toHaveValue("s1");
    expect(within(dialog).getByLabelText("Status")).toHaveValue("MemberActivated");
    expect(within(dialog).getByLabelText(/License #/)).toHaveValue("MD123");
    expect(within(dialog).getByLabelText(/Bio/)).toHaveValue("Experienced.");
    expect(h.getPreceptor).toHaveBeenCalledWith("pr1");

    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));
    expect(h.updatePreceptor).toHaveBeenCalledWith(
      "pr1",
      expect.objectContaining({ email: "jane@x.com", primarySpecialtyId: "s1", medicalLicenseNumber: "MD123", bio: "Experienced." })
    );
    expect(await screen.findByText("Preceptor updated.")).toBeInTheDocument();
  });

  it("shows an error modal when the preceptor detail fails to load", async () => {
    h.getPreceptor.mockRejectedValue(new ApiError(500, "boom"));
    renderPage();
    await screen.findByText("Jane Carter");

    const row = screen.getByText("Jane Carter").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));

    expect(await screen.findByText(/Couldn.t load preceptor: boom/)).toBeInTheDocument();
  });

  it("deletes a preceptor after confirmation", async () => {
    h.deletePreceptor.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText("Jane Carter");

    const row = screen.getByText("Jane Carter").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(h.deletePreceptor).toHaveBeenCalledWith("pr1");
    expect(await screen.findByText(/Deleted Jane Carter/)).toBeInTheDocument();
  });

  it("shows a page banner when a delete fails", async () => {
    h.deletePreceptor.mockRejectedValue(new ApiError(409, "Preceptor is referenced."));
    renderPage();
    await screen.findByText("Jane Carter");

    const row = screen.getByText("Jane Carter").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(await screen.findByText("Preceptor is referenced.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("re-edits with fresh detail after an update (no stale cache)", async () => {
    h.updatePreceptor.mockResolvedValue({ ...DETAIL, firstName: "Janet" });
    renderPage();
    await screen.findByText("Jane Carter");

    let row = screen.getByText("Jane Carter").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));
    expect(await screen.findByLabelText("First name")).toHaveValue("Jane");
    await userEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Save" }));
    await screen.findByText("Preceptor updated.");

    row = screen.getByText("Jane Carter").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));
    expect(await screen.findByLabelText("First name")).toHaveValue("Janet");
  });
});

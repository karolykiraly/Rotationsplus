import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getSpecialties: vi.fn(),
  createSpecialty: vi.fn(),
  updateSpecialty: vi.fn(),
  deleteSpecialty: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getSpecialties: () => h.getSpecialties(),
  createSpecialty: (name: string) => h.createSpecialty(name),
  updateSpecialty: (id: string, name: string) => h.updateSpecialty(id, name),
  deleteSpecialty: (id: string) => h.deleteSpecialty(id),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { SpecialtiesPage } from "./SpecialtiesPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada Admin", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };

function renderPage() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  });
  return render(
    <QueryClientProvider client={qc}>
      <SpecialtiesPage />
    </QueryClientProvider>
  );
}

describe("SpecialtiesPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getSpecialties.mockResolvedValue([
      { id: "s1", name: "Internal Medicine" },
      { id: "s2", name: "Pediatrics" }
    ]);
  });

  it("lists specialties", async () => {
    renderPage();
    expect(await screen.findByText("Internal Medicine")).toBeInTheDocument();
    expect(screen.getByText("Pediatrics")).toBeInTheDocument();
  });

  it("blocks non-admins from the management UI", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    // The table / add button must not render for a non-admin (the API also rejects writes).
    expect(screen.queryByRole("button", { name: "Add specialty" })).not.toBeInTheDocument();
  });

  it("creates a specialty", async () => {
    h.createSpecialty.mockResolvedValue({ id: "s3", name: "Cardiology" });
    renderPage();
    await screen.findByText("Internal Medicine");

    await userEvent.click(screen.getByRole("button", { name: "Add specialty" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Name"), "Cardiology");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.createSpecialty).toHaveBeenCalledWith("Cardiology");
    expect(await screen.findByText(/Added/)).toBeInTheDocument();
  });

  it("validates the name client-side before calling the API", async () => {
    renderPage();
    await screen.findByText("Internal Medicine");

    await userEvent.click(screen.getByRole("button", { name: "Add specialty" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByText("Name is required.")).toBeInTheDocument();
    expect(h.createSpecialty).not.toHaveBeenCalled();
  });

  it("surfaces a server conflict in the form", async () => {
    h.createSpecialty.mockRejectedValue(new ApiError(409, "A specialty named 'Pediatrics' already exists."));
    renderPage();
    await screen.findByText("Internal Medicine");

    await userEvent.click(screen.getByRole("button", { name: "Add specialty" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Name"), "Pediatrics");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await screen.findByText(/already exists/)).toBeInTheDocument();
    expect(h.createSpecialty).toHaveBeenCalled();
  });

  it("edits a specialty", async () => {
    h.updateSpecialty.mockResolvedValue({ id: "s1", name: "Internal Med" });
    renderPage();
    await screen.findByText("Internal Medicine");

    const row = screen.getByText("Internal Medicine").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Edit" }));
    const dialog = await screen.findByRole("dialog");
    const input = within(dialog).getByLabelText("Name");
    expect(input).toHaveValue("Internal Medicine"); // pre-filled from the row
    await userEvent.clear(input);
    await userEvent.type(input, "Internal Med");
    await userEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(h.updateSpecialty).toHaveBeenCalledWith("s1", "Internal Med");
  });

  it("deletes a specialty after confirmation", async () => {
    h.deleteSpecialty.mockResolvedValue(undefined);
    renderPage();
    await screen.findByText("Pediatrics");

    const row = screen.getByText("Pediatrics").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(h.deleteSpecialty).toHaveBeenCalledWith("s2");
    expect(await screen.findByText(/Deleted/)).toBeInTheDocument();
  });

  it("shows a page banner when a delete fails", async () => {
    h.deleteSpecialty.mockRejectedValue(new ApiError(409, "Specialty is in use."));
    renderPage();
    await screen.findByText("Pediatrics");

    const row = screen.getByText("Pediatrics").closest("tr")!;
    await userEvent.click(within(row).getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    // The confirm dialog closes and the failure surfaces as a page banner.
    expect(await screen.findByText("Specialty is in use.")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

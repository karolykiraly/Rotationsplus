import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getProgramRequiredDocuments: vi.fn(),
  setProgramRequiredDocuments: vi.fn(),
  createDocumentType: vi.fn()
}));
vi.mock("../api", () => ({
  getProgramRequiredDocuments: (id: string) => h.getProgramRequiredDocuments(id),
  setProgramRequiredDocuments: (id: string, due: number, ids: string[]) =>
    h.setProgramRequiredDocuments(id, due, ids),
  createDocumentType: (name: string, category: string) => h.createDocumentType(name, category)
}));

import { ProgramDocumentsModal } from "./ProgramDocumentsModal";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

const config = {
  documentDueDays: 14,
  requiredDocumentTypeIds: ["t-cv"],
  catalog: [
    { id: "t-cv", name: "Curriculum Vitae (CV)", category: "Professional" },
    { id: "t-covid", name: "COVID-19 Vaccine", category: "Immunization" },
    { id: "t-id", name: "Proof of Identity", category: "Identity" }
  ]
};

function renderModal(onClose = () => {}) {
  return render(
    <QueryClientProvider client={newClient()}>
      <ProgramDocumentsModal programId="p1" programLabel="Cardiology · IP-1001" onClose={onClose} />
    </QueryClientProvider>
  );
}

describe("ProgramDocumentsModal", () => {
  beforeEach(() => {
    h.getProgramRequiredDocuments.mockReset().mockResolvedValue(config);
    h.setProgramRequiredDocuments.mockReset().mockResolvedValue(config);
    h.createDocumentType.mockReset().mockResolvedValue({ id: "t-new", name: "Hospital Orientation", category: "Other" });
  });

  it("renders the catalog grouped by category with the configured selection + due-days", async () => {
    renderModal();
    expect(await screen.findByText("Curriculum Vitae (CV)")).toBeInTheDocument();
    expect(screen.getByText("COVID-19 Vaccine")).toBeInTheDocument();
    // The configured type is checked, an unconfigured one is not.
    expect((screen.getByLabelText("Curriculum Vitae (CV)") as HTMLInputElement).checked).toBe(true);
    expect((screen.getByLabelText("COVID-19 Vaccine") as HTMLInputElement).checked).toBe(false);
    expect((screen.getByLabelText("Documents due (days before start)") as HTMLInputElement).value).toBe("14");
  });

  it("toggles a type and saves the full selection + due-days", async () => {
    renderModal();
    await screen.findByText("COVID-19 Vaccine");

    fireEvent.click(screen.getByLabelText("COVID-19 Vaccine")); // add it
    fireEvent.change(screen.getByLabelText("Documents due (days before start)"), { target: { value: "7" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(h.setProgramRequiredDocuments).toHaveBeenCalledWith(
        "p1",
        7,
        expect.arrayContaining(["t-cv", "t-covid"])
      )
    );
  });

  it("adds a custom document type, surfaces it as a checked box, and preserves in-progress edits", async () => {
    renderModal();
    await screen.findByText("Curriculum Vitae (CV)");

    // Admin edits in-progress: check another type and change due-days BEFORE adding a custom type.
    fireEvent.click(screen.getByLabelText("COVID-19 Vaccine"));
    fireEvent.change(screen.getByLabelText("Documents due (days before start)"), { target: { value: "5" } });

    fireEvent.change(screen.getByPlaceholderText(/Hospital Orientation/), { target: { value: "Hospital Orientation" } });
    fireEvent.click(screen.getByRole("button", { name: "Add" }));

    await waitFor(() => expect(h.createDocumentType).toHaveBeenCalledWith("Hospital Orientation", "Other"));
    // New type appears in the catalog (cache-appended, NOT refetched) and is auto-selected…
    const newBox = (await screen.findByLabelText("Hospital Orientation")) as HTMLInputElement;
    expect(newBox.checked).toBe(true);
    expect(h.getProgramRequiredDocuments).toHaveBeenCalledTimes(1); // no refetch
    // …and the admin's prior edits survive (regression: the seeding effect must not re-fire).
    expect((screen.getByLabelText("COVID-19 Vaccine") as HTMLInputElement).checked).toBe(true);
    expect((screen.getByLabelText("Documents due (days before start)") as HTMLInputElement).value).toBe("5");
  });

  it("shows an error when the configuration fails to load", async () => {
    h.getProgramRequiredDocuments.mockRejectedValue(new Error("down"));
    renderModal();
    expect(await screen.findByText(/Couldn.t load the configuration: down/)).toBeInTheDocument();
  });
});

import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ browsePrograms: vi.fn(), getCustomerSpecialties: vi.fn() }));
vi.mock("./customerApi", () => ({
  browsePrograms: (qs: string) => h.browsePrograms(qs),
  getCustomerSpecialties: () => h.getCustomerSpecialties()
}));

import { BrowsePage } from "./BrowsePage";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderBrowse(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <BrowsePage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("BrowsePage", () => {
  beforeEach(() => {
    h.browsePrograms.mockReset().mockResolvedValue([
      {
        id: "p1",
        specialtyName: "Internal Medicine",
        programType: "InPerson",
        maxStudentsPerRotation: 2,
        minWeeksPerRotation: 4,
        retailAmountPerWeek: 1500,
        preceptorName: "Jane Carter"
      }
    ]);
    h.getCustomerSpecialties.mockReset().mockResolvedValue([
      { id: "s1", name: "Internal Medicine" },
      { id: "s2", name: "Pediatrics" }
    ]);
  });

  it("renders program cards from the catalog", async () => {
    renderBrowse();
    // "with Jane Carter" + the price are card-only (the specialty/type also appear in the filters).
    expect(await screen.findByText("with Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("$1,500.00/wk")).toBeInTheDocument();
    expect(screen.getByText("Internal Medicine", { selector: ".pc-specialty" })).toBeInTheDocument();
    expect(screen.getByText("In person", { selector: ".pc-type" })).toBeInTheDocument();
  });

  it("re-queries with the selected specialty filter", async () => {
    renderBrowse();
    await screen.findByText("with Jane Carter");

    await userEvent.selectOptions(screen.getByLabelText("Specialty"), "s2");

    // The latest browse call carries the chosen specialty in the query string.
    const lastCall = h.browsePrograms.mock.calls.at(-1)?.[0] as string;
    expect(lastCall).toContain("specialtyId=s2");
  });

  it("re-queries with the free-text search", async () => {
    renderBrowse();
    await screen.findByText("with Jane Carter");

    await userEvent.type(screen.getByLabelText("Search"), "peds");

    const lastCall = h.browsePrograms.mock.calls.at(-1)?.[0] as string;
    expect(lastCall).toContain("q=peds");
  });

  it("re-queries with the max-price filter", async () => {
    renderBrowse();
    await screen.findByText("with Jane Carter");

    await userEvent.type(screen.getByLabelText("Max price per week"), "1200");

    const lastCall = h.browsePrograms.mock.calls.at(-1)?.[0] as string;
    expect(lastCall).toContain("maxRetailPerWeek=1200");
  });

  it("shows an empty state when no programs match", async () => {
    h.browsePrograms.mockResolvedValue([]);
    renderBrowse();
    expect(await screen.findByText("No programs match your filters.")).toBeInTheDocument();
  });

  it("shows an error state when the catalog fails to load", async () => {
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["portal-programs", ""], queryFn: () => Promise.reject(new Error("down")) });
    renderBrowse(qc);
    expect(await screen.findByText(/Couldn.t load programs: down/)).toBeInTheDocument();
  });
});

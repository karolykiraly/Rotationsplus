import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getCustomerProgram: vi.fn() }));
vi.mock("./customerApi", () => ({ getCustomerProgram: (id: string) => h.getCustomerProgram(id) }));

import { ProgramDetailPage } from "./ProgramDetailPage";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderDetail(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={["/portal/programs/p1"]}>
        <Routes>
          <Route path="/portal/programs/:id" element={<ProgramDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("ProgramDetailPage", () => {
  beforeEach(() => h.getCustomerProgram.mockReset());

  it("renders the customer-facing program detail (no honorarium)", async () => {
    h.getCustomerProgram.mockResolvedValue({
      id: "p1",
      specialtyId: "s1",
      specialtyName: "Internal Medicine",
      programType: "InPerson",
      maxStudentsPerRotation: 2,
      minWeeksPerRotation: 4,
      retailAmountPerWeek: 1500,
      weeklyHonorarium: null,
      description: "Hands-on inpatient rotation.",
      preceptorId: "d1",
      preceptorName: "Jane Carter"
    });

    renderDetail();

    expect(await screen.findByRole("heading", { name: "Internal Medicine" })).toBeInTheDocument();
    expect(screen.getByText("$1,500.00 / week")).toBeInTheDocument();
    expect(screen.getByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("Hands-on inpatient rotation.")).toBeInTheDocument();
    expect(h.getCustomerProgram).toHaveBeenCalledWith("p1");
    // Honorarium must never appear on the customer view.
    expect(screen.queryByText(/honorarium/i)).not.toBeInTheDocument();
  });

  it("shows an error when the program can't be loaded", async () => {
    // Seed the cache into an error state (prefetch swallows the rejection) so the test exercises the
    // error UI without a live mount-time query rejection floating as unhandled in RQ v5 + vitest.
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["portal-program", "p1"], queryFn: () => Promise.reject(new Error("nope")) });
    renderDetail(qc);
    expect(await screen.findByText(/Couldn.t load this program: nope/)).toBeInTheDocument();
  });
});

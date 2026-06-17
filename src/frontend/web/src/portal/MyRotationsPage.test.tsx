import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getCustomerRotations: vi.fn() }));
vi.mock("./customerApi", () => ({ getCustomerRotations: () => h.getCustomerRotations() }));

import { MyRotationsPage } from "./MyRotationsPage";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <MyRotationsPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("MyRotationsPage", () => {
  beforeEach(() => {
    h.getCustomerRotations.mockReset().mockResolvedValue([
      {
        id: "r1",
        specialtyName: "Internal Medicine",
        programType: "InPerson",
        preceptorName: "Jane Carter",
        startDate: "2026-07-06",
        endDate: "2026-08-03",
        weeks: 4,
        status: "Active"
      },
      {
        id: "r2",
        specialtyName: "Pediatrics",
        programType: "TeleRotation",
        preceptorName: null,
        startDate: "2026-09-01",
        endDate: "2026-09-29",
        weeks: 4,
        status: "NotStarted"
      }
    ]);
  });

  it("renders the student's rotation cards with preceptor and status", async () => {
    renderPage();
    expect(await screen.findByText("Internal Medicine", { selector: ".pc-specialty" })).toBeInTheDocument();
    expect(screen.getByText("with Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("Active", { selector: ".badge" })).toBeInTheDocument();
    // NotStarted is surfaced to the student as "Approved".
    expect(screen.getByText("Approved", { selector: ".badge" })).toBeInTheDocument();
  });

  it("shows an empty state when the student has no rotations", async () => {
    h.getCustomerRotations.mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText("You don’t have any rotations yet.")).toBeInTheDocument();
  });

  it("shows an error state when the tracker fails to load", async () => {
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["customer-rotations"], queryFn: () => Promise.reject(new Error("down")) });
    renderPage(qc);
    expect(await screen.findByText(/Couldn.t load your rotations: down/)).toBeInTheDocument();
  });
});

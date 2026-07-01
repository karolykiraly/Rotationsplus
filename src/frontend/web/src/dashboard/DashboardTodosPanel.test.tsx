import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getDashboardTodos: vi.fn() }));
vi.mock("../api", () => ({ getDashboardTodos: () => h.getDashboardTodos() }));

import { DashboardTodosPanel } from "./DashboardTodosPanel";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPanel() {
  return render(
    <QueryClientProvider client={newClient()}>
      <MemoryRouter>
        <DashboardTodosPanel />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

const todos = {
  documentsToReview: {
    count: 3,
    items: [
      { documentId: "d1", rotationId: "r1", rotationNumber: 1001, studentId: "s1", studentName: "Jane Doe", documentTypeName: "COVID-19 Vaccine", dueDate: "2026-08-01", submittedAtUtc: "2026-07-20T00:00:00Z" },
      { documentId: "d2", rotationId: "r2", rotationNumber: 1002, studentId: "s2", studentName: "John Roe", documentTypeName: "Proof of Identity", dueDate: "2026-08-05", submittedAtUtc: "2026-07-21T00:00:00Z" }
    ]
  },
  awaitingPayment: {
    count: 1,
    items: [
      { rotationId: "r3", rotationNumber: 1003, studentName: "Sam Rivera", specialtyName: "Cardiology", startDate: "2026-09-01" }
    ]
  },
  preceptorApprovals: { count: 0, items: [] }
};

describe("DashboardTodosPanel", () => {
  beforeEach(() => {
    h.getDashboardTodos.mockReset().mockResolvedValue(todos);
  });

  it("renders each queue with its count and items", async () => {
    renderPanel();
    expect(await screen.findByText("Documents to review")).toBeInTheDocument();
    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("COVID-19 Vaccine · R1001")).toBeInTheDocument();
    expect(screen.getByText("Sam Rivera")).toBeInTheDocument();
    expect(screen.getByText(/Cardiology · R1003 · starts/)).toBeInTheDocument();
  });

  it("shows '+N more' linking into the owning screen when the count exceeds the preview", async () => {
    renderPanel();
    // documentsToReview: count 3, 2 items shown → "+1 more"
    const more = await screen.findByText(/\+1 more — review documents/);
    expect(more.closest("a")).toHaveAttribute("href", "/admin/contacts");
  });

  it("shows an all-clear empty state for a queue with no items", async () => {
    renderPanel();
    await screen.findByText("Preceptor approvals");
    expect(screen.getByText("All clear — nothing waiting.")).toBeInTheDocument();
  });

  it("shows an error state when the to-do's fail to load", async () => {
    h.getDashboardTodos.mockRejectedValue(new Error("down"));
    renderPanel();
    expect(await screen.findByText(/Couldn.t load your to-do's: down/)).toBeInTheDocument();
  });
});

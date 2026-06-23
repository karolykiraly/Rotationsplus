import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getDashboardRevenue: vi.fn() }));
vi.mock("../api", () => ({ getDashboardRevenue: () => h.getDashboardRevenue() }));

import { DashboardRevenuePanel } from "./DashboardRevenuePanel";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPanel() {
  return render(
    <QueryClientProvider client={newClient()}>
      <DashboardRevenuePanel />
    </QueryClientProvider>
  );
}

const revenue = {
  currency: "USD",
  collected: 12600,
  refunded: 600,
  outstandingReceivable: 54000,
  collectedThisMonth: 1200,
  byProgramType: [
    { type: "InPerson", amount: 9000 },
    { type: "TeleRotation", amount: 3600 }
  ],
  monthlyTrend: [
    { year: 2026, month: 1, amount: 0 },
    { year: 2026, month: 2, amount: 2000 },
    { year: 2026, month: 3, amount: 1400 },
    { year: 2026, month: 4, amount: 4000 },
    { year: 2026, month: 5, amount: 4000 },
    { year: 2026, month: 6, amount: 1200 }
  ]
};

describe("DashboardRevenuePanel", () => {
  beforeEach(() => {
    h.getDashboardRevenue.mockReset().mockResolvedValue(revenue);
  });

  it("renders the headline figures as formatted currency", async () => {
    renderPanel();
    expect(await screen.findByText("Collected (net of refunds)")).toBeInTheDocument();
    expect(screen.getByText("$12,600.00")).toBeInTheDocument(); // collected
    expect(screen.getByText("$54,000.00")).toBeInTheDocument(); // outstanding
    expect(screen.getByText("$600.00")).toBeInTheDocument(); // refunded
  });

  it("renders the per-type breakdown sorted by amount", async () => {
    renderPanel();
    await screen.findByText("By program type");
    expect(screen.getByText("In person")).toBeInTheDocument();
    expect(screen.getByText("$9,000.00")).toBeInTheDocument();
    expect(screen.getByText("Tele-rotation")).toBeInTheDocument();
  });

  it("renders the six-month trend with month labels", async () => {
    renderPanel();
    await screen.findByText("Collected — last 6 months");
    expect(screen.getByText("Jun 2026")).toBeInTheDocument();
    expect(screen.getByText("Jan 2026")).toBeInTheDocument();
  });

  it("shows an error state when revenue fails to load", async () => {
    h.getDashboardRevenue.mockRejectedValue(new Error("down"));
    renderPanel();
    expect(await screen.findByText(/Couldn.t load revenue: down/)).toBeInTheDocument();
  });
});

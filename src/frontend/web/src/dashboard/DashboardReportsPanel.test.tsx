import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getDashboardReports: vi.fn() }));
vi.mock("../api", () => ({ getDashboardReports: () => h.getDashboardReports() }));

import { DashboardReportsPanel } from "./DashboardReportsPanel";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPanel() {
  return render(
    <QueryClientProvider client={newClient()}>
      <DashboardReportsPanel />
    </QueryClientProvider>
  );
}

const reports = {
  totalStudents: 40,
  studentsWithBooking: 10,
  totalRotations: 14,
  registrations: [
    { year: 2026, month: 1, students: 3, preceptors: 1 },
    { year: 2026, month: 2, students: 5, preceptors: 0 },
    { year: 2026, month: 3, students: 2, preceptors: 2 },
    { year: 2026, month: 4, students: 8, preceptors: 1 },
    { year: 2026, month: 5, students: 6, preceptors: 3 },
    { year: 2026, month: 6, students: 4, preceptors: 1 }
  ],
  topSpecialties: [
    { specialtyName: "Internal Medicine", rotationCount: 8 },
    { specialtyName: "Cardiology", rotationCount: 4 }
  ]
};

describe("DashboardReportsPanel", () => {
  beforeEach(() => {
    h.getDashboardReports.mockReset().mockResolvedValue(reports);
  });

  it("renders the conversion funnel figures", async () => {
    renderPanel();
    expect(await screen.findByText("Total students")).toBeInTheDocument();
    expect(screen.getByText("40")).toBeInTheDocument();
    // 10 / 40 = 25%
    expect(screen.getByText("10 (25%)")).toBeInTheDocument();
    expect(screen.getByText("14")).toBeInTheDocument();
  });

  it("renders the registration trend and busiest specialties", async () => {
    renderPanel();
    await screen.findByText("Registrations — last 6 months");
    expect(screen.getByText("Jun 2026")).toBeInTheDocument();
    expect(screen.getByText("4s · 1p")).toBeInTheDocument();
    expect(screen.getByText("Internal Medicine")).toBeInTheDocument();
    expect(screen.getByText("Cardiology")).toBeInTheDocument();
  });

  it("guards conversion against zero students", async () => {
    h.getDashboardReports.mockResolvedValue({ ...reports, totalStudents: 0, studentsWithBooking: 0 });
    renderPanel();
    expect(await screen.findByText("0 (0%)")).toBeInTheDocument();
  });

  it("shows an error state when reports fail to load", async () => {
    h.getDashboardReports.mockRejectedValue(new Error("down"));
    renderPanel();
    expect(await screen.findByText(/Couldn.t load reports: down/)).toBeInTheDocument();
  });
});

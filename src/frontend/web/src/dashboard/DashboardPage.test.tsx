import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({ getMe: vi.fn(), getDashboard: vi.fn() }));
vi.mock("../api", () => ({ getMe: () => h.getMe(), getDashboard: () => h.getDashboard() }));

import { DashboardPage } from "./DashboardPage";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const DASH = {
  students: 2,
  programs: 4,
  preceptors: 2,
  specialties: 15,
  rotations: 3,
  rotationsByStatus: [
    { status: "Active", count: 2 },
    { status: "NotStarted", count: 1 }
  ],
  upcomingStarts: [
    // Status "Pending" so its badge doesn't collide with the by-status panel's Active/Approved badges.
    { id: "r1", studentName: "Sam Rivera", specialtyName: "Internal Medicine", startDate: "2026-07-06", status: "Pending" }
  ]
};

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <DashboardPage />
    </QueryClientProvider>
  );
}

describe("DashboardPage", () => {
  beforeEach(() => {
    h.getMe.mockReset().mockResolvedValue(ADMIN);
    h.getDashboard.mockReset().mockResolvedValue(DASH);
  });

  it("renders the domain totals", async () => {
    renderPage();
    const students = (await screen.findByText("Students")).closest(".stat-card") as HTMLElement;
    expect(within(students).getByText("2")).toBeInTheDocument();
    const specialties = screen.getByText("Specialties").closest(".stat-card") as HTMLElement;
    expect(within(specialties).getByText("15")).toBeInTheDocument();
  });

  it("renders the rotations-by-status pipeline with labels and counts", async () => {
    renderPage();
    await screen.findByText("Rotations by status");
    // NotStarted is displayed as "Approved".
    expect(screen.getByText("Approved", { selector: ".badge" })).toBeInTheDocument();
    expect(screen.getByText("Active", { selector: ".badge" })).toBeInTheDocument();
  });

  it("renders the upcoming starts", async () => {
    renderPage();
    expect(await screen.findByText("Sam Rivera")).toBeInTheDocument();
    expect(screen.getByText("Internal Medicine")).toBeInTheDocument();
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByText("Rotations by status")).not.toBeInTheDocument();
  });

  it("shows an error state when the dashboard fails to load", async () => {
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["dashboard"], queryFn: () => Promise.reject(new Error("down")) });
    renderPage(qc);
    expect(await screen.findByText(/Couldn.t load the dashboard: down/)).toBeInTheDocument();
  });
});

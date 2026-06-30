import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getDashboard: vi.fn(),
  getCampaigns: vi.fn(),
  setRotationConfirmations: vi.fn()
}));
vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getDashboard: () => h.getDashboard(),
  getCampaigns: () => h.getCampaigns(),
  setRotationConfirmations: (id: string, flags: unknown) => h.setRotationConfirmations(id, flags)
}));

import { DashboardPage } from "./DashboardPage";

// The Upcoming-Starts table defaults to today's rotations, so the seed row must start "today".
const pad = (n: number) => String(n).padStart(2, "0");
const T = new Date();
const TODAY = `${T.getFullYear()}-${pad(T.getMonth() + 1)}-${pad(T.getDate())}`;

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const DASH = {
  students: 2,
  programs: 4,
  preceptors: 2,
  specialties: 15,
  rotations: 3,
  // 4 programs: 2 InPerson family (InPerson + InPersonResearch), 1 Consultation, 1 TeleRotation.
  programsByType: [
    { type: "InPerson", count: 1 },
    { type: "InPersonResearch", count: 1 },
    { type: "Consultation", count: 1 },
    { type: "TeleRotation", count: 1 }
  ],
  rotationsByStatus: [
    { status: "Active", count: 2 },
    { status: "NotStarted", count: 1 }
  ],
  upcomingStarts: [
    { id: "r1", startDate: TODAY, preceptorName: "Jane Carter", studentName: "Sam Rivera",
      documentsApproved: false, preceptorConfirmed: false, needsVisa: true }
  ],
  today: {
    newPrograms: 3,
    newProgramsByType: [
      { type: "InPerson", count: 2 },
      { type: "Consultation", count: 1 }
    ],
    newStudents: 5,
    newPreceptors: 4,
    issuesReported: 0,
    rotationsStarting: 1,
    rotationsInProgress: 2,
    rotationsCompleting: 3,
    rotationsCancelled: 1
  }
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
    h.getCampaigns.mockReset().mockResolvedValue({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 1 });
    h.setRotationConfirmations.mockReset().mockResolvedValue(undefined);
  });

  it("renders the LiveScore totals in production's ALL-CAPS labels (no Total Specialties)", async () => {
    renderPage();
    const programs = (await screen.findByText("TOTAL PROGRAMS")).closest(".score-metric") as HTMLElement;
    expect(within(programs).getByText("4")).toBeInTheDocument(); // the big circle
    const students = screen.getByText("TOTAL STUDENTS").closest(".score-pill-row") as HTMLElement;
    expect(within(students).getByText("2")).toBeInTheDocument();
    expect(screen.getByText("TOTAL PRECEPTORS")).toBeInTheDocument();
    // Total Specialties was dropped to match production (only Students + Preceptors).
    expect(screen.queryByText(/total specialties/i)).not.toBeInTheDocument();
  });

  it("renders the TOTAL PROGRAMS per-type breakdown (families summed)", async () => {
    renderPage();
    const programs = (await screen.findByText("TOTAL PROGRAMS")).closest(".score-metric") as HTMLElement;
    // InPerson family = InPerson(1) + InPersonResearch(1) = 2; Consultation = 1; TeleRotation = 1.
    expect(within(programs).getByText("InPerson").closest("li")).toHaveTextContent("2");
    expect(within(programs).getByText("Consultation").closest("li")).toHaveTextContent("1");
    expect(within(programs).getByText("TeleRotation").closest("li")).toHaveTextContent("1");
  });

  it("renders Today's LiveScore movement (new counts + rotation cycle)", async () => {
    renderPage();
    const newProg = (await screen.findByText("New Programs Added")).closest(".score-metric") as HTMLElement;
    expect(within(newProg).getByText("3")).toBeInTheDocument(); // circle = newPrograms
    expect(within(newProg).getByText("InPerson").closest("li")).toHaveTextContent("2");
    expect(within(newProg).getByText("Consultation").closest("li")).toHaveTextContent("1");
    expect(within(newProg).getByText("TeleRotation").closest("li")).toHaveTextContent("0");

    const students = screen.getByText("New Students Registered").closest(".score-pill-row") as HTMLElement;
    expect(within(students).getByText("5")).toBeInTheDocument();
    const preceptors = screen.getByText("New Preceptors Approved").closest(".score-pill-row") as HTMLElement;
    expect(within(preceptors).getByText("4")).toBeInTheDocument();

    const cycle = screen.getByText("Rotations Cycle").closest(".score-metric") as HTMLElement;
    // Circle = starting(1) + inProgress(2) + completing(3) + cancelled(1) = 7.
    expect(within(cycle).getByText("7")).toBeInTheDocument();
    expect(within(cycle).getByText("Starting").closest("li")).toHaveTextContent("1");
    expect(within(cycle).getByText("In Progress").closest("li")).toHaveTextContent("2");
    expect(within(cycle).getByText("Completing").closest("li")).toHaveTextContent("3");
    expect(within(cycle).getByText("Canceled").closest("li")).toHaveTextContent("1");
  });

  it("renders the rotations breakdown from the by-status counts", async () => {
    renderPage();
    const rotations = (await screen.findByText("TOTAL ROTATIONS")).closest(".score-metric") as HTMLElement;
    // Active=2, NotStarted(=In progress)=1 from the mock; Completed defaults to 0.
    expect(within(rotations).getByText("Active").closest("li")).toHaveTextContent("2");
    expect(within(rotations).getByText("In progress").closest("li")).toHaveTextContent("1");
  });

  it("renders the Upcoming Starts calendar and the selected day's production day-table", async () => {
    renderPage();
    expect(await screen.findByText("Upcoming Starts")).toBeInTheDocument();
    // Production columns: Preceptor · Student · Documents Approved · Preceptor Confirmed · Needs Visa.
    expect(screen.getByText("Documents Approved")).toBeInTheDocument();
    expect(screen.getByText("Preceptor Confirmed")).toBeInTheDocument();
    // The selected day (today) shows its rotation's preceptor + student.
    expect(screen.getByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("Sam Rivera")).toBeInTheDocument();
    // Needs-Visa is a read-only reflection of the student's status.
    expect(screen.getByLabelText("Needs visa for Sam Rivera")).toBeChecked();
  });

  it("toggles a rotation's Documents Approved flag via the confirmations endpoint", async () => {
    renderPage();
    const box = await screen.findByLabelText("Documents approved for Sam Rivera");
    expect(box).not.toBeChecked();

    await userEvent.click(box);

    // Both flags are sent; toggling docs on carries the current (unchanged) preceptor-confirmed value.
    expect(h.setRotationConfirmations).toHaveBeenCalledWith("r1", { documentsApproved: true, preceptorConfirmed: false });
  });

  it("switches to the Campaign tab and renders its panel", async () => {
    renderPage();
    await screen.findByText("Today's LiveScore");
    await userEvent.click(screen.getByRole("tab", { name: "Campaign" }));
    // The Campaign compose form replaces the Results panel.
    expect(await screen.findByText("Save draft")).toBeInTheDocument();
    expect(screen.queryByText("Today's LiveScore")).not.toBeInTheDocument();
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByText("LiveScore")).not.toBeInTheDocument();
  });

  it("shows an error state when the dashboard fails to load", async () => {
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["dashboard"], queryFn: () => Promise.reject(new Error("down")) });
    renderPage(qc);
    expect(await screen.findByText(/Couldn.t load the dashboard: down/)).toBeInTheDocument();
  });
});

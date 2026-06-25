import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getCampaigns: vi.fn(),
  createCampaign: vi.fn(),
  sendCampaign: vi.fn()
}));
vi.mock("../api", () => ({
  getCampaigns: () => h.getCampaigns(),
  createCampaign: (s: string, b: string, a: string) => h.createCampaign(s, b, a),
  sendCampaign: (id: string) => h.sendCampaign(id)
}));

import { DashboardCampaignPanel } from "./DashboardCampaignPanel";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPanel() {
  return render(
    <QueryClientProvider client={newClient()}>
      <DashboardCampaignPanel />
    </QueryClientProvider>
  );
}

const draft = {
  id: "c1",
  subject: "Spring rotations are open",
  audience: "AllStudents",
  status: "Draft",
  recipientCount: 0,
  sentCount: 0,
  failedCount: 0,
  createdAtUtc: "2026-06-24T00:00:00Z",
  sentAtUtc: null
};
const sent = {
  ...draft,
  id: "c2",
  subject: "Welcome aboard",
  status: "Sent",
  recipientCount: 12,
  sentCount: 12,
  failedCount: 0,
  sentAtUtc: "2026-06-24T10:00:00Z"
};

describe("DashboardCampaignPanel", () => {
  beforeEach(() => {
    h.getCampaigns.mockReset().mockResolvedValue([draft, sent]);
    h.createCampaign.mockReset().mockResolvedValue({ ...draft, body: "x" });
    h.sendCampaign.mockReset().mockResolvedValue({ ...draft, status: "Queued" });
  });

  it("lists campaigns with status and tallies; only drafts have a Send button", async () => {
    renderPanel();
    expect(await screen.findByText("Spring rotations are open")).toBeInTheDocument();
    expect(screen.getByText("Welcome aboard")).toBeInTheDocument();
    expect(screen.getByText("12 / 0")).toBeInTheDocument(); // sent campaign's tally
    // One Send button (the draft); the Sent campaign has none.
    expect(screen.getAllByRole("button", { name: "Send" })).toHaveLength(1);
  });

  it("disables Save draft until subject and message are filled, then creates", async () => {
    renderPanel();
    await screen.findByText("Spring rotations are open");

    const save = screen.getByRole("button", { name: "Save draft" });
    expect(save).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Subject"), { target: { value: "Hello" } });
    fireEvent.change(screen.getByLabelText("Message"), { target: { value: "Body text" } });
    fireEvent.change(screen.getByLabelText("Audience"), { target: { value: "AllPreceptors" } });
    expect(save).toBeEnabled();

    fireEvent.click(save);
    await waitFor(() => expect(h.createCampaign).toHaveBeenCalledWith("Hello", "Body text", "AllPreceptors"));
  });

  it("sends a draft and refetches the list", async () => {
    renderPanel();
    await screen.findByText("Spring rotations are open");

    fireEvent.click(screen.getByRole("button", { name: "Send" }));
    await waitFor(() => expect(h.sendCampaign).toHaveBeenCalledWith("c1"));
    await waitFor(() => expect(h.getCampaigns.mock.calls.length).toBeGreaterThanOrEqual(2));
  });

  it("shows an error state when campaigns fail to load", async () => {
    h.getCampaigns.mockRejectedValue(new Error("down"));
    renderPanel();
    expect(await screen.findByText(/Couldn.t load campaigns: down/)).toBeInTheDocument();
  });
});

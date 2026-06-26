import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const paged = <T,>(items: T[], totalCount = items.length) =>
  ({ items, page: 1, pageSize: 10, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 10)) });

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getPreceptors: vi.fn(),
  approvePreceptor: vi.fn(),
  rejectPreceptor: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getPreceptors: (params: unknown) => h.getPreceptors(params),
  approvePreceptor: (id: string) => h.approvePreceptor(id),
  rejectPreceptor: (id: string, reason: string) => h.rejectPreceptor(id, reason),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { PermissionPage } from "./PermissionPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const ROW = {
  id: "pr1",
  fullName: "Jane Carter",
  email: "jane@x.com",
  primarySpecialtyName: "Internal Medicine",
  city: "Chicago",
  state: "IL",
  status: "Pending"
};

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } } });
}

function renderPage() {
  return render(
    <QueryClientProvider client={newClient()}>
      <PermissionPage />
    </QueryClientProvider>
  );
}

describe("PermissionPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getPreceptors.mockResolvedValue(paged([ROW]));
    h.approvePreceptor.mockResolvedValue({ ...ROW, status: "MemberActivated" });
    h.rejectPreceptor.mockResolvedValue({ ...ROW, status: "Rejected" });
  });

  it("requests the Pending queue and lists awaiting preceptors", async () => {
    renderPage();
    expect(await screen.findByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("Chicago, IL")).toBeInTheDocument();
    expect(h.getPreceptors).toHaveBeenCalledWith(expect.objectContaining({ status: "Pending" }));
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByText("Jane Carter")).not.toBeInTheDocument();
  });

  it("approves a preceptor and shows a confirmation", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Approve" }));

    expect(h.approvePreceptor).toHaveBeenCalledWith("pr1");
    expect(await screen.findByText(/Approved Jane Carter/)).toBeInTheDocument();
  });

  it("requires a reason before rejecting, then rejects", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Reject" }));
    const dialog = await screen.findByRole("dialog");

    // Empty reason → blocked, no API call.
    await userEvent.click(within(dialog).getByRole("button", { name: "Reject" }));
    expect(await within(dialog).findByText("A rejection reason is required.")).toBeInTheDocument();
    expect(h.rejectPreceptor).not.toHaveBeenCalled();

    // With a reason → rejects.
    await userEvent.type(within(dialog).getByLabelText(/Reason/), "License unverifiable");
    await userEvent.click(within(dialog).getByRole("button", { name: "Reject" }));

    await waitFor(() => expect(h.rejectPreceptor).toHaveBeenCalledWith("pr1", "License unverifiable"));
    expect(await screen.findByText(/Rejected Jane Carter/)).toBeInTheDocument();
  });

  it("surfaces a server error from approve in a banner", async () => {
    h.approvePreceptor.mockRejectedValue(new ApiError(409, "Only a pending preceptor can be approved."));
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Approve" }));

    expect(await screen.findByText(/Only a pending preceptor can be approved/)).toBeInTheDocument();
  });

  it("steps back a page when approving the last row on the last page shrinks the queue", async () => {
    // Two pages initially; after the approve-triggered refetch the total drops to one page → clamp to 1.
    let total = 11;
    h.getPreceptors.mockImplementation((params?: { page?: number }) =>
      Promise.resolve(paged([{ ...ROW, id: `pg${params?.page ?? 1}` }], total)));
    renderPage();
    await screen.findByText("Jane Carter");
    await userEvent.click(screen.getByRole("button", { name: "Next" })); // → page 2
    await waitFor(() => expect(h.getPreceptors).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 })));

    // Approve the only row on page 2; the queue drops to one page and the clamp steps back to page 1.
    total = 1;
    await userEvent.click(screen.getByRole("button", { name: "Approve" }));
    await waitFor(() => expect(h.getPreceptors).toHaveBeenLastCalledWith(expect.objectContaining({ page: 1 })));
  });

  it("shows the empty state when nothing awaits approval", async () => {
    h.getPreceptors.mockResolvedValue(paged([]));
    renderPage();
    expect(await screen.findByText("No preceptors are awaiting approval.")).toBeInTheDocument();
  });
});

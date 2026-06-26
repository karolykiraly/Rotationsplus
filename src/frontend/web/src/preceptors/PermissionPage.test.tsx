import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const paged = <T,>(items: T[], totalCount = items.length) =>
  ({ items, page: 1, pageSize: 10, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 10)) });

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getPreceptors: vi.fn(),
  savePreceptorPermissions: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getPreceptors: (params: unknown) => h.getPreceptors(params),
  savePreceptorPermissions: (activateIds: string[], rejectIds: string[]) =>
    h.savePreceptorPermissions(activateIds, rejectIds),
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
  mobilePhone: "+1 312-555-0101",
  callScheduled: false,
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
    h.savePreceptorPermissions.mockResolvedValue({ activated: 1, rejected: 0 });
  });

  it("requests the Pending queue and shows the production columns", async () => {
    renderPage();
    expect(await screen.findByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("Internal Medicine")).toBeInTheDocument();
    expect(screen.getByText("+1 312-555-0101")).toBeInTheDocument();
    expect(screen.getByText("No")).toBeInTheDocument(); // Scheduled
    expect(h.getPreceptors).toHaveBeenCalledWith(expect.objectContaining({ status: "Pending" }));
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByText("Jane Carter")).not.toBeInTheDocument();
  });

  it("activates a checked preceptor on Save and confirms", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("checkbox", { name: /Activate Jane Carter/i }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(h.savePreceptorPermissions).toHaveBeenCalledWith(["pr1"], []));
    expect(await screen.findByText(/activated 1, rejected 0/i)).toBeInTheDocument();
  });

  it("rejects a checked preceptor on Save", async () => {
    h.savePreceptorPermissions.mockResolvedValue({ activated: 0, rejected: 1 });
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("checkbox", { name: /Reject Jane Carter/i }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(h.savePreceptorPermissions).toHaveBeenCalledWith([], ["pr1"]));
  });

  it("makes Activated and Reject mutually exclusive per row", async () => {
    renderPage();
    await screen.findByText("Jane Carter");
    const activate = screen.getByRole("checkbox", { name: /Activate Jane Carter/i });
    const reject = screen.getByRole("checkbox", { name: /Reject Jane Carter/i });

    await userEvent.click(activate);
    expect(activate).toBeChecked();
    await userEvent.click(reject); // checking Reject clears Activate
    expect(reject).toBeChecked();
    expect(activate).not.toBeChecked();
  });

  it("warns when Save is clicked with nothing checked (no API call)", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(h.savePreceptorPermissions).not.toHaveBeenCalled();
    expect(await screen.findByText(/Check Activated or Reject/i)).toBeInTheDocument();
  });

  it("surfaces a server error from Save in a banner", async () => {
    h.savePreceptorPermissions.mockRejectedValue(new ApiError(400, "A preceptor can't be both activated and rejected."));
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("checkbox", { name: /Activate Jane Carter/i }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText(/can't be both activated and rejected/i)).toBeInTheDocument();
  });

  it("shows the empty state when nothing awaits approval", async () => {
    h.getPreceptors.mockResolvedValue(paged([]));
    renderPage();
    expect(await screen.findByText("No preceptors are awaiting approval.")).toBeInTheDocument();
  });
});

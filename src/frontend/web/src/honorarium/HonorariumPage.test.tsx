import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const paged = <T,>(items: T[], totalCount = items.length) => ({
  items,
  page: 1,
  pageSize: 10,
  totalCount,
  totalPages: Math.max(1, Math.ceil(totalCount / 10))
});

const h = vi.hoisted(() => ({
  getMe: vi.fn(),
  getHonorariums: vi.fn(),
  payHonorarium: vi.fn(),
  setHonorariumRefund: vi.fn(),
  deleteHonorarium: vi.fn()
}));

vi.mock("../api", () => ({
  getMe: () => h.getMe(),
  getHonorariums: (params: unknown) => h.getHonorariums(params),
  payHonorarium: (id: string) => h.payHonorarium(id),
  setHonorariumRefund: (id: string, refunded: boolean) => h.setHonorariumRefund(id, refunded),
  deleteHonorarium: (id: string) => h.deleteHonorarium(id),
  ApiError: class ApiError extends Error {
    constructor(public status: number, message: string) {
      super(message);
    }
  }
}));

import { HonorariumPage } from "./HonorariumPage";
import { ApiError } from "../api";

const ADMIN = { objectId: "o", name: "Ada", username: "ada@x", roles: ["Admin"], isStaff: true, profileId: "p" };
const ROW = {
  id: "hon1",
  rotationId: "rot1",
  rotationNumber: 42,
  preceptorId: "pre1",
  preceptorName: "Jane Carter",
  studentName: "Sam Lee",
  stage: "Deposit" as const,
  amount: 500,
  currency: "USD",
  status: "Pending" as const,
  refunded: false,
  rotationStartDate: "2026-09-07",
  paidAtUtc: null
};

function newClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } }
  });
}

function renderPage() {
  return render(
    <QueryClientProvider client={newClient()}>
      <HonorariumPage />
    </QueryClientProvider>
  );
}

describe("HonorariumPage", () => {
  beforeEach(() => {
    Object.values(h).forEach((m) => m.mockReset());
    h.getMe.mockResolvedValue(ADMIN);
    h.getHonorariums.mockResolvedValue(paged([ROW]));
    h.payHonorarium.mockResolvedValue({ ...ROW, status: "Paid" });
    h.setHonorariumRefund.mockResolvedValue({ ...ROW, refunded: true });
    h.deleteHonorarium.mockResolvedValue(undefined);
  });

  it("requests the Deposit stage first and lists payout rows", async () => {
    renderPage();
    expect(await screen.findByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("R42")).toBeInTheDocument();
    expect(screen.getByText("$500")).toBeInTheDocument();
    expect(h.getHonorariums).toHaveBeenCalledWith(expect.objectContaining({ stage: "Deposit" }));
  });

  it("blocks non-admins", async () => {
    h.getMe.mockResolvedValue({ ...ADMIN, roles: ["Coordinator"] });
    renderPage();
    expect(await screen.findByText(/need the Admin role/i)).toBeInTheDocument();
    expect(screen.queryByText("Jane Carter")).not.toBeInTheDocument();
  });

  it("switches to the Start stage tab and requests it", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByText("Honorarium Start"));

    await waitFor(() =>
      expect(h.getHonorariums).toHaveBeenLastCalledWith(expect.objectContaining({ stage: "Start", page: 1 }))
    );
  });

  it("pays a stage and shows a confirmation banner", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Pay" }));

    expect(h.payHonorarium).toHaveBeenCalledWith("hon1");
    expect(await screen.findByText(/Marked the deposit honorarium for R42 paid/)).toBeInTheDocument();
  });

  it("surfaces a server error from pay (out-of-order stage) in a banner", async () => {
    h.payHonorarium.mockRejectedValue(new ApiError(409, "The previous honorarium stage must be paid first."));
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Pay" }));

    expect(await screen.findByText(/previous honorarium stage must be paid first/)).toBeInTheDocument();
  });

  it("toggles the refunded flag on the Deposit tab", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("checkbox", { name: /deposit refunded/i }));

    await waitFor(() => expect(h.setHonorariumRefund).toHaveBeenCalledWith("hon1", true));
  });

  it("shows a Paid pill instead of the Pay button for a paid row", async () => {
    h.getHonorariums.mockResolvedValue(paged([{ ...ROW, status: "Paid", paidAtUtc: "2026-09-10T00:00:00Z" }]));
    renderPage();
    await screen.findByText("Jane Carter");

    expect(screen.getByText("Paid")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pay" })).not.toBeInTheDocument();
  });

  it("deletes a deposit honorarium after confirming", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    await waitFor(() => expect(h.deleteHonorarium).toHaveBeenCalledWith("hon1"));
    expect(await screen.findByText(/Deleted the deposit honorarium for R42/)).toBeInTheDocument();
  });

  it("surfaces a 409 from deleting a paid honorarium in a banner", async () => {
    h.deleteHonorarium.mockRejectedValue(new ApiError(409, "A paid honorarium can't be deleted."));
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Delete" }));

    expect(await screen.findByText(/A paid honorarium can't be deleted/)).toBeInTheDocument();
  });

  it("shows no Delete button outside the Deposit tab", async () => {
    renderPage();
    await screen.findByText("Jane Carter");

    await userEvent.click(screen.getByText("Honorarium Start"));
    await waitFor(() =>
      expect(h.getHonorariums).toHaveBeenLastCalledWith(expect.objectContaining({ stage: "Start" }))
    );

    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });

  it("shows the empty state when a stage has no rows", async () => {
    h.getHonorariums.mockResolvedValue(paged([]));
    renderPage();
    expect(await screen.findByText("No honorariums in this stage.")).toBeInTheDocument();
  });
});

import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiError } from "../api";

const h = vi.hoisted(() => ({ openDepositIntent: vi.fn(), simulateDeposit: vi.fn() }));
vi.mock("./customerApi", () => ({
  openDepositIntent: (id: string) => h.openDepositIntent(id),
  simulateDeposit: (paymentId: string, outcome: string) => h.simulateDeposit(paymentId, outcome)
}));

import { PaymentModal } from "./PaymentModal";

const intent = {
  paymentId: "p1",
  clientSecret: "cs_test",
  amount: 600,
  totalAmount: 6000,
  outstandingAmount: 5400,
  currency: "USD",
  status: "Pending" as const
};

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
}

function renderModal(onPaid = vi.fn(), onClose = vi.fn(), qc = newClient()) {
  render(
    <QueryClientProvider client={qc}>
      <PaymentModal rotationId="r1" onClose={onClose} onPaid={onPaid} />
    </QueryClientProvider>
  );
  return { onPaid, onClose, qc };
}

describe("PaymentModal", () => {
  beforeEach(() => {
    h.openDepositIntent.mockReset().mockResolvedValue(intent);
    h.simulateDeposit.mockReset().mockResolvedValue({ paymentId: "p1", status: "Succeeded" });
  });

  it("opens the intent on mount and shows the deposit breakdown", async () => {
    renderModal();
    expect(await screen.findByText("$600.00", { selector: ".pay-amount" })).toBeInTheDocument();
    expect(screen.getByText("$6,000.00")).toBeInTheDocument(); // total
    expect(screen.getByText("$5,400.00")).toBeInTheDocument(); // outstanding
    expect(h.openDepositIntent).toHaveBeenCalledWith("r1");
    // DEV test-mode affordances are shown (no real Stripe key configured).
    expect(screen.getByText(/Test mode/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Pay $600.00" })).toBeInTheDocument();
  });

  it("pays the deposit, refreshes the rotations list, and signals success", async () => {
    const qc = newClient();
    const invalidate = vi.spyOn(qc, "invalidateQueries");
    const { onPaid } = renderModal(vi.fn(), vi.fn(), qc);
    fireEvent.click(await screen.findByRole("button", { name: "Pay $600.00" }));

    await waitFor(() => expect(onPaid).toHaveBeenCalledTimes(1));
    expect(h.simulateDeposit).toHaveBeenCalledWith("p1", "succeeded");
    // The rotations tracker is invalidated with the exact key useCustomerRotations reads, so the card
    // flips from Pending to Approved (a key typo here would otherwise silently leave stale UI).
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ["customer-rotations"] });
  });

  it("shows a declined message and does not signal success when the payment fails", async () => {
    h.simulateDeposit.mockResolvedValue({ paymentId: "p1", status: "Failed" });
    const { onPaid } = renderModal();
    fireEvent.click(await screen.findByRole("button", { name: "Simulate decline" }));

    expect(await screen.findByText(/payment was declined/)).toBeInTheDocument();
    expect(onPaid).not.toHaveBeenCalled();
  });

  it("explains when the rotation is no longer awaiting a deposit (409)", async () => {
    h.openDepositIntent.mockRejectedValue(new ApiError(409, "conflict"));
    renderModal();
    expect(await screen.findByText(/isn.t awaiting a deposit/)).toBeInTheDocument();
  });
});

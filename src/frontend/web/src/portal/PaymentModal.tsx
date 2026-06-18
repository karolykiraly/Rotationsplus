import { useEffect, useRef } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Modal } from "../components/Modal";
import { openDepositIntent, simulateDeposit } from "./customerApi";
import { stripePublishableKey } from "../authConfig";
import { ApiError } from "../api";

interface PaymentModalProps {
  rotationId: string;
  onClose: () => void;
  /** Called once the deposit has succeeded (the rotations list is already invalidated). */
  onPaid: () => void;
}

/** Test mode = no Stripe publishable key configured (DEV today). The deposit round-trip is completed via
 *  the DEV simulate endpoint against the fake gateway; when a real key lands, the Stripe Elements card
 *  flow replaces this (its own slice — see Docs/Vendor_Sandboxes.md). */
const TEST_MODE = stripePublishableKey === "";

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

/** The deposit checkout dialog: opens (or re-offers) the payment intent for the student's rotation, shows
 *  the deposit/total/outstanding breakdown, and completes the payment. Fulfilment is webhook-driven on the
 *  server (or the DEV simulate analog here), so a success here means the booking has been approved. */
export function PaymentModal({ rotationId, onClose, onPaid }: PaymentModalProps) {
  const queryClient = useQueryClient();

  const openIntent = useMutation({ mutationFn: () => openDepositIntent(rotationId) });
  // Open the intent exactly once when the dialog mounts. The ref guard stops React 18 StrictMode's
  // double effect-invoke from firing a second, redundant POST. (The server is idempotent on a pending
  // deposit regardless — it re-offers the same intent, never a second charge — but we avoid the wasted
  // round-trip rather than leaning on that.)
  const opened = useRef(false);
  useEffect(() => {
    if (opened.current) return;
    opened.current = true;
    openIntent.mutate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const pay = useMutation({
    mutationFn: (outcome: "succeeded" | "failed") => simulateDeposit(openIntent.data!.paymentId, outcome),
    onSuccess: (result) => {
      if (result.status === "Succeeded") {
        // The booking has been approved server-side; refresh the tracker so the status flips.
        void queryClient.invalidateQueries({ queryKey: ["customer-rotations"] });
        onPaid();
      }
    }
  });

  const intent = openIntent.data;
  const wasDeclined = pay.data?.status === "Failed";
  const openError = openIntent.error as ApiError | null;

  return (
    <Modal title="Pay your deposit" onClose={onClose}>
      <div className="modal-body">
        {openIntent.isPending && <div className="state">Preparing your deposit…</div>}

        {openError && (
          <div className="banner error" role="alert">
            {openError.status === 409
              ? "This rotation isn’t awaiting a deposit right now."
              : `Couldn’t start the payment: ${openError.message}`}
          </div>
        )}

        {intent && (
          <>
            <dl className="pay-breakdown">
              <div>
                <dt>Deposit due now</dt>
                <dd className="pay-amount">{formatMoney(intent.amount, intent.currency)}</dd>
              </div>
              <div>
                <dt>Total program cost</dt>
                <dd>{formatMoney(intent.totalAmount, intent.currency)}</dd>
              </div>
              <div>
                <dt>Outstanding after deposit</dt>
                <dd>{formatMoney(intent.outstandingAmount, intent.currency)}</dd>
              </div>
            </dl>

            {wasDeclined && (
              <div className="banner error" role="alert">The payment was declined. Please try again.</div>
            )}
            {pay.isError && <div className="banner error" role="alert">{(pay.error as Error).message}</div>}
            {TEST_MODE && <p className="pay-test-note">Test mode — no real card is charged.</p>}
          </>
        )}
      </div>

      <div className="modal-foot">
        <button type="button" className="btn btn-ghost" onClick={onClose} disabled={pay.isPending}>
          Cancel
        </button>

        {intent && TEST_MODE && (
          <>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => pay.mutate("failed")}
              disabled={pay.isPending}
            >
              Simulate decline
            </button>
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => pay.mutate("succeeded")}
              disabled={pay.isPending}
            >
              {pay.isPending ? "Processing…" : `Pay ${formatMoney(intent.amount, intent.currency)}`}
            </button>
          </>
        )}

        {intent && !TEST_MODE && (
          <button
            type="button"
            className="btn btn-primary"
            disabled
            title="Card payment arrives with the Stripe integration"
          >
            Pay {formatMoney(intent.amount, intent.currency)}
          </button>
        )}
      </div>
    </Modal>
  );
}

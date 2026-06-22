import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getCustomerRotations: vi.fn(),
  openDepositIntent: vi.fn(),
  simulateDeposit: vi.fn(),
  getRotationDocuments: vi.fn(),
  uploadRotationDocument: vi.fn()
}));
vi.mock("./customerApi", () => ({
  getCustomerRotations: () => h.getCustomerRotations(),
  openDepositIntent: (id: string) => h.openDepositIntent(id),
  simulateDeposit: (paymentId: string, outcome: string) => h.simulateDeposit(paymentId, outcome),
  getRotationDocuments: (id: string) => h.getRotationDocuments(id),
  uploadRotationDocument: (rid: string, did: string, file: File) => h.uploadRotationDocument(rid, did, file)
}));

import { MyRotationsPage } from "./MyRotationsPage";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderPage(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <MyRotationsPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("MyRotationsPage", () => {
  beforeEach(() => {
    h.getCustomerRotations.mockReset().mockResolvedValue([
      {
        id: "r1",
        rotationNumber: 1001,
        specialtyName: "Internal Medicine",
        programType: "InPerson",
        preceptorName: "Jane Carter",
        startDate: "2026-07-06",
        endDate: "2026-08-03",
        weeks: 4,
        status: "Active",
        documentsState: "Missing"
      },
      {
        id: "r2",
        rotationNumber: 1002,
        specialtyName: "Pediatrics",
        programType: "TeleRotation",
        preceptorName: null,
        startDate: "2026-09-01",
        endDate: "2026-09-29",
        weeks: 4,
        status: "NotStarted",
        documentsState: "Complete"
      },
      {
        id: "r3",
        rotationNumber: 1003,
        specialtyName: "Cardiology",
        programType: "InPerson",
        preceptorName: null,
        startDate: "2026-10-05",
        endDate: "2026-11-02",
        weeks: 4,
        status: "Pending",
        documentsState: "NotRequired"
      }
    ]);
    h.getRotationDocuments.mockReset().mockResolvedValue([
      {
        id: "d1",
        documentTypeName: "Curriculum Vitae (CV)",
        category: "Professional",
        status: "UploadNeeded",
        dueDate: "2026-06-22",
        fileName: null,
        fileUrl: null,
        submittedAtUtc: null,
        rejectionReason: null
      }
    ]);
    h.openDepositIntent.mockResolvedValue({
      paymentId: "p1",
      clientSecret: "cs",
      amount: 600,
      totalAmount: 6000,
      outstandingAmount: 5400,
      currency: "USD",
      status: "Pending"
    });
  });

  it("renders the student's rotation cards with preceptor and status", async () => {
    renderPage();
    expect(await screen.findByText("Internal Medicine", { selector: ".pc-specialty" })).toBeInTheDocument();
    // The rotation number renders as "R{number}".
    expect(screen.getByText("R1001")).toBeInTheDocument();
    // Preceptor now shows under a "Preceptor" column label (live tracker style), just the name.
    expect(screen.getByText("Jane Carter")).toBeInTheDocument();
    expect(screen.getByText("Active", { selector: ".badge" })).toBeInTheDocument();
    // NotStarted is surfaced to the student as "Approved".
    expect(screen.getByText("Approved", { selector: ".badge" })).toBeInTheDocument();
  });

  it("renders the Documents column per state and opens the documents checklist", async () => {
    renderPage();
    // Missing → a "Documents Missing" link; Complete → "All Documents Uploaded"; NotRequired → "—".
    const missing = await screen.findByRole("button", { name: "Documents Missing" });
    expect(missing).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "All Documents Uploaded" })).toBeInTheDocument();

    fireEvent.click(missing);

    // The checklist dialog opens and pulls that rotation's documents.
    expect(await screen.findByRole("dialog", { name: /Documents — R1001/ })).toBeInTheDocument();
    expect(h.getRotationDocuments).toHaveBeenCalledWith("r1");
    expect(await screen.findByText("Curriculum Vitae (CV)")).toBeInTheDocument();
  });

  it("offers Pay deposit only on a Pending rotation and opens the payment dialog", async () => {
    renderPage();
    // One Pay-deposit button: only the Pending (Cardiology) rotation, not Active/Approved ones.
    const payButtons = await screen.findAllByRole("button", { name: "Pay deposit" });
    expect(payButtons).toHaveLength(1);

    fireEvent.click(payButtons[0]);

    // The dialog opens and pulls the deposit intent for that rotation.
    expect(await screen.findByRole("dialog", { name: "Pay your deposit" })).toBeInTheDocument();
    expect(h.openDepositIntent).toHaveBeenCalledWith("r3");
  });

  it("shows an empty state when the student has no rotations", async () => {
    h.getCustomerRotations.mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText("You don’t have any rotations yet.")).toBeInTheDocument();
  });

  it("shows an error state when the tracker fails to load", async () => {
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["customer-rotations"], queryFn: () => Promise.reject(new Error("down")) });
    renderPage(qc);
    expect(await screen.findByText(/Couldn.t load your rotations: down/)).toBeInTheDocument();
  });
});

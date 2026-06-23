import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getStudentDocuments: vi.fn(),
  setDocumentStatus: vi.fn(),
  uploadDocumentFile: vi.fn(),
  clearDocumentFile: vi.fn()
}));
vi.mock("../api", () => ({
  getStudentDocuments: (id: string) => h.getStudentDocuments(id),
  setDocumentStatus: (id: string, s: string, r: string | null) => h.setDocumentStatus(id, s, r),
  uploadDocumentFile: (id: string, f: File) => h.uploadDocumentFile(id, f),
  clearDocumentFile: (id: string) => h.clearDocumentFile(id)
}));

import { StudentDocumentsModal } from "./StudentDocumentsModal";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderModal() {
  return render(
    <QueryClientProvider client={newClient()}>
      <StudentDocumentsModal studentId="s1" studentName="Jane Doe" onClose={() => {}} />
    </QueryClientProvider>
  );
}

const docs = [
  {
    id: "d1",
    rotationId: "r1",
    rotationNumber: 1001,
    documentTypeName: "COVID-19 Vaccine",
    category: "Immunization",
    status: "Submitted",
    dueDate: "2026-08-01",
    fileName: "covid.pdf",
    fileUrl: "https://documents.local/d1/abc",
    uploadedAtUtc: "2026-07-20T00:00:00Z",
    reviewedAtUtc: null,
    rejectionReason: null
  },
  {
    id: "d2",
    rotationId: "r2",
    rotationNumber: 1002,
    documentTypeName: "Proof of Identity",
    category: "Identity",
    status: "UploadNeeded",
    dueDate: "2026-09-01",
    fileName: null,
    fileUrl: null,
    uploadedAtUtc: null,
    reviewedAtUtc: null,
    rejectionReason: null
  }
];

describe("StudentDocumentsModal", () => {
  beforeEach(() => {
    h.getStudentDocuments.mockReset().mockResolvedValue(docs);
    h.setDocumentStatus.mockReset().mockResolvedValue({ ...docs[0], status: "Approved" });
    h.uploadDocumentFile.mockReset().mockResolvedValue({ ...docs[1], status: "Submitted", fileName: "id.pdf" });
    h.clearDocumentFile.mockReset().mockResolvedValue({ ...docs[0], status: "UploadNeeded", fileName: null });
  });

  it("lists every document with rotation context and the file link", async () => {
    renderModal();
    expect(await screen.findByText("COVID-19 Vaccine")).toBeInTheDocument();
    expect(screen.getByText("Proof of Identity")).toBeInTheDocument();
    expect(screen.getByText(/R1001 · Due/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "covid.pdf" })).toBeInTheDocument();
  });

  it("approves a document immediately on selecting a non-rejected status", async () => {
    renderModal();
    await screen.findByText("COVID-19 Vaccine");

    fireEvent.change(screen.getByLabelText("Set status for COVID-19 Vaccine (R1001)"), { target: { value: "Approved" } });

    await waitFor(() => expect(h.setDocumentStatus).toHaveBeenCalledWith("d1", "Approved", null));
    // The list is refetched after the change (initial + refetch).
    await waitFor(() => expect(h.getStudentDocuments.mock.calls.length).toBeGreaterThanOrEqual(2));
  });

  it("requires a reason before rejecting (reason input + Save)", async () => {
    renderModal();
    await screen.findByText("COVID-19 Vaccine");

    fireEvent.change(screen.getByLabelText("Set status for COVID-19 Vaccine (R1001)"), { target: { value: "Rejected" } });
    // Selecting Rejected does NOT immediately call the API — it reveals a reason input.
    expect(h.setDocumentStatus).not.toHaveBeenCalled();

    const reason = screen.getByLabelText("Rejection reason for COVID-19 Vaccine (R1001)");
    fireEvent.change(reason, { target: { value: "Illegible scan" } });
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(h.setDocumentStatus).toHaveBeenCalledWith("d1", "Rejected", "Illegible scan"));
  });

  it("disambiguates labels by rotation when the same document type spans two rotations", async () => {
    h.getStudentDocuments.mockResolvedValue([
      { ...docs[0], id: "a", rotationNumber: 1001, documentTypeName: "COVID-19 Vaccine" },
      { ...docs[0], id: "b", rotationNumber: 1002, documentTypeName: "COVID-19 Vaccine" }
    ]);
    renderModal();
    // Both rows are addressable by distinct accessible names (no getByLabelText throw).
    expect(await screen.findByLabelText("Set status for COVID-19 Vaccine (R1001)")).toBeInTheDocument();
    expect(screen.getByLabelText("Set status for COVID-19 Vaccine (R1002)")).toBeInTheDocument();
  });

  it("uploads a file on behalf of the student", async () => {
    renderModal();
    await screen.findByText("Proof of Identity");

    const input = screen.getByLabelText("Upload Proof of Identity (R1002) on behalf") as HTMLInputElement;
    const file = new File([new Uint8Array([0x25, 0x50, 0x44, 0x46])], "id.pdf", { type: "application/pdf" });
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => expect(h.uploadDocumentFile).toHaveBeenCalledWith("d2", file));
  });

  it("filters the table by rotation number", async () => {
    renderModal();
    await screen.findByText("COVID-19 Vaccine");

    fireEvent.change(screen.getByLabelText("Filter by rotation"), { target: { value: "1002" } });

    expect(screen.queryByText("COVID-19 Vaccine")).not.toBeInTheDocument();
    expect(screen.getByText("Proof of Identity")).toBeInTheDocument();
  });

  it("shows an error when documents fail to load", async () => {
    h.getStudentDocuments.mockRejectedValue(new Error("down"));
    renderModal();
    expect(await screen.findByText(/Couldn.t load documents: down/)).toBeInTheDocument();
  });
});

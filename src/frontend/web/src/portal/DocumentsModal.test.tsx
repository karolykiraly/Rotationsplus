import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getRotationDocuments: vi.fn(),
  uploadRotationDocument: vi.fn()
}));
vi.mock("./customerApi", () => ({
  getRotationDocuments: (id: string) => h.getRotationDocuments(id),
  uploadRotationDocument: (rid: string, did: string, file: File) => h.uploadRotationDocument(rid, did, file)
}));

import { DocumentsModal } from "./DocumentsModal";

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false } } });
}

function renderModal() {
  return render(
    <QueryClientProvider client={newClient()}>
      <DocumentsModal rotationId="r1" rotationLabel="R1001" onClose={() => {}} />
    </QueryClientProvider>
  );
}

const docs = [
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
  },
  {
    id: "d2",
    documentTypeName: "COVID-19 Vaccine",
    category: "Immunization",
    status: "Approved",
    dueDate: "2026-06-22",
    fileName: "covid.pdf",
    fileUrl: "https://documents.local/d2/abc",
    submittedAtUtc: "2026-06-21T00:00:00Z",
    rejectionReason: null
  },
  {
    id: "d3",
    documentTypeName: "Proof of Identity",
    category: "Identity",
    status: "Rejected",
    dueDate: "2026-06-22",
    fileName: "id.pdf",
    fileUrl: "https://documents.local/d3/abc",
    submittedAtUtc: "2026-06-21T00:00:00Z",
    rejectionReason: "Blurry — please re-scan"
  }
];

describe("DocumentsModal", () => {
  beforeEach(() => {
    h.getRotationDocuments.mockReset().mockResolvedValue(docs);
    h.uploadRotationDocument.mockReset().mockResolvedValue({ ...docs[0], status: "Submitted", fileName: "cv.pdf" });
  });

  it("lists the checklist with statuses, file links, and a rejection reason", async () => {
    renderModal();
    expect(await screen.findByText("Curriculum Vitae (CV)")).toBeInTheDocument();
    expect(screen.getByText("COVID-19 Vaccine")).toBeInTheDocument();
    // Approved doc shows its file link and is locked (no upload control).
    expect(screen.getByRole("link", { name: "covid.pdf" })).toBeInTheDocument();
    // Rejected doc surfaces the reason.
    expect(screen.getByText(/Blurry — please re-scan/)).toBeInTheDocument();
  });

  it("locks an approved document (no upload control)", async () => {
    renderModal();
    await screen.findByText("COVID-19 Vaccine");
    // The approved row has no Upload/Replace button — only UploadNeeded/Rejected ones do.
    expect(screen.queryByRole("button", { name: "Upload COVID-19 Vaccine" })).not.toBeInTheDocument();
  });

  it("uploads a chosen file and re-pulls the checklist", async () => {
    renderModal();
    await screen.findByText("Curriculum Vitae (CV)");

    const input = screen.getByLabelText("Upload Curriculum Vitae (CV)") as HTMLInputElement;
    const file = new File([new Uint8Array([0x25, 0x50, 0x44, 0x46])], "cv.pdf", { type: "application/pdf" });
    fireEvent.change(input, { target: { files: [file] } });

    await waitFor(() => expect(h.uploadRotationDocument).toHaveBeenCalledWith("r1", "d1", file));
    // The checklist is refetched after a successful upload (initial load + refetch).
    await waitFor(() => expect(h.getRotationDocuments.mock.calls.length).toBeGreaterThanOrEqual(2));
  });

  it("shows an error when the documents fail to load", async () => {
    h.getRotationDocuments.mockRejectedValue(new Error("down"));
    renderModal();
    expect(await screen.findByText(/Couldn.t load your documents: down/)).toBeInTheDocument();
  });
});

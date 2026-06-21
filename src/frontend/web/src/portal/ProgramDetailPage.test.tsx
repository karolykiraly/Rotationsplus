import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

const h = vi.hoisted(() => ({
  getCustomerProgram: vi.fn(),
  getProgramQuote: vi.fn(),
  bookRotation: vi.fn()
}));
vi.mock("./customerApi", () => ({
  getCustomerProgram: (id: string) => h.getCustomerProgram(id),
  getProgramQuote: (id: string, weeks: number) => h.getProgramQuote(id, weeks),
  bookRotation: (id: string, startDate: string, weeks: number) => h.bookRotation(id, startDate, weeks)
}));

const navigate = vi.fn();
vi.mock("react-router-dom", async (importOriginal) => ({
  ...(await importOriginal<typeof import("react-router-dom")>()),
  useNavigate: () => navigate
}));

import { ProgramDetailPage } from "./ProgramDetailPage";

const program = {
  id: "p1",
  specialtyId: "s1",
  specialtyName: "Internal Medicine",
  programType: "InPerson",
  maxStudentsPerRotation: 2,
  minWeeksPerRotation: 4,
  retailAmountPerWeek: 1500,
  weeklyHonorarium: null,
  description: "Hands-on inpatient rotation.",
  preceptorId: "d1",
  preceptorName: "Jane Carter",
  isOpen: false,
  programNumber: 1001,
  city: "Los Angeles",
  state: "CA",
  tags: ["Hospital Letterhead LOR"],
  imageUrl: "https://blob/hospital.jpg?sas"
};

const quote = {
  programId: "p1",
  weeks: 4,
  currency: "USD",
  retailAmountPerWeek: 1500,
  totalAmount: 6000,
  depositAmount: 600,
  outstandingAmount: 5400,
  depositPercent: 0.1,
  isOpen: false
};

function newClient() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, retryOnMount: false }, mutations: { retry: false } } });
}

function renderDetail(qc = newClient()) {
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={["/portal/programs/p1"]}>
        <Routes>
          <Route path="/portal/programs/:id" element={<ProgramDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("ProgramDetailPage", () => {
  beforeEach(() => {
    h.getCustomerProgram.mockReset().mockResolvedValue(program);
    h.getProgramQuote.mockReset().mockResolvedValue(quote);
    h.bookRotation.mockReset().mockResolvedValue({ id: "r1", status: "Pending" });
    navigate.mockReset();
  });

  it("renders the customer-facing program detail (no honorarium)", async () => {
    renderDetail();

    expect(await screen.findByRole("heading", { name: "Internal Medicine" })).toBeInTheDocument();
    // Header price is the whole minimum stay ($1,500/wk × 4 wks), shown in the title row.
    expect(screen.getByText("$6,000.00", { selector: ".pd-price" })).toBeInTheDocument();
    expect(screen.getByText("For 4 weeks minimum")).toBeInTheDocument();
    expect(screen.getByText("Jane Carter")).toBeInTheDocument();
    // Real catalog fields: typed code, location, seats, and a tag chip.
    expect(screen.getByText("Program IP1001")).toBeInTheDocument();
    expect(screen.getByText("Los Angeles, CA")).toBeInTheDocument();
    expect(screen.getByText("2 seats available")).toBeInTheDocument();
    expect(screen.getByText("Hospital Letterhead LOR", { selector: ".tag-chip" })).toBeInTheDocument();
    expect(screen.getByText("Hands-on inpatient rotation.")).toBeInTheDocument();
    // The hospital image renders from the program's read URL.
    expect(screen.getByRole("img", { name: "Internal Medicine program" })).toHaveAttribute("src", "https://blob/hospital.jpg?sas");
    expect(h.getCustomerProgram).toHaveBeenCalledWith("p1");
    // Honorarium must never appear on the customer view.
    expect(screen.queryByText(/honorarium/i)).not.toBeInTheDocument();
  });

  it("shows the server-computed quote and books a rotation, then navigates to the tracker", async () => {
    renderDetail();
    // The quote loads for the default duration (the program minimum, 4 weeks).
    expect(await screen.findByText("$600.00", { selector: ".pay-amount" })).toBeInTheDocument();
    expect(h.getProgramQuote).toHaveBeenCalledWith("p1", 4);

    // Booking is gated until a start date is chosen.
    const bookBtn = screen.getByRole("button", { name: "Book this rotation" });
    expect(bookBtn).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Start date"), { target: { value: "2026-11-02" } });
    expect(bookBtn).toBeEnabled();

    fireEvent.click(bookBtn);
    await waitFor(() => expect(navigate).toHaveBeenCalledWith("/portal/rotations"));
    expect(h.bookRotation).toHaveBeenCalledWith("p1", "2026-11-02", 4);
  });

  it("re-quotes when the duration changes and books the chosen weeks (no stale quote)", async () => {
    h.getProgramQuote.mockImplementation((_id: string, weeks: number) =>
      Promise.resolve({ ...quote, weeks, totalAmount: 1500 * weeks, depositAmount: 150 * weeks, outstandingAmount: 1350 * weeks }));
    renderDetail();

    // Default 4 weeks quotes $600 deposit…
    expect(await screen.findByText("$600.00", { selector: ".pay-amount" })).toBeInTheDocument();
    // …change to 6 weeks → the quote refetches for the new key and the booking uses 6, not the stale 4.
    fireEvent.change(screen.getByLabelText("Weeks"), { target: { value: "6" } });
    expect(await screen.findByText("$900.00", { selector: ".pay-amount" })).toBeInTheDocument();
    expect(h.getProgramQuote).toHaveBeenCalledWith("p1", 6);

    fireEvent.change(screen.getByLabelText("Start date"), { target: { value: "2026-11-02" } });
    fireEvent.click(screen.getByRole("button", { name: "Book this rotation" }));
    await waitFor(() => expect(h.bookRotation).toHaveBeenCalledWith("p1", "2026-11-02", 6));
  });

  it("disables booking and the quote when the duration is below the program minimum", async () => {
    renderDetail();
    await screen.findByText("$600.00", { selector: ".pay-amount" });
    h.getProgramQuote.mockClear();

    fireEvent.change(screen.getByLabelText("Weeks"), { target: { value: "2" } }); // below the 4-week minimum
    fireEvent.change(screen.getByLabelText("Start date"), { target: { value: "2026-11-02" } });

    expect(screen.getByText(/requires 4–520 weeks/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Book this rotation" })).toBeDisabled();
    expect(h.getProgramQuote).not.toHaveBeenCalled(); // no quote requested for an invalid duration
  });

  it("surfaces a booking error without navigating", async () => {
    h.bookRotation.mockRejectedValue(new Error("That program is no longer available."));
    renderDetail();
    fireEvent.change(await screen.findByLabelText("Start date"), { target: { value: "2026-11-02" } });
    fireEvent.click(screen.getByRole("button", { name: "Book this rotation" }));

    expect(await screen.findByText(/no longer available/)).toBeInTheDocument();
    expect(navigate).not.toHaveBeenCalled();
  });

  it("prices a ConsultationSub program hourly (no '/ week', no seats pill)", async () => {
    h.getCustomerProgram.mockResolvedValue({ ...program, programType: "ConsultationSub", retailAmountPerWeek: 250 });
    renderDetail();
    // Hourly programs show the rate as-is, not a per-week price, and omit the seats pill.
    expect(await screen.findByText("$250.00")).toBeInTheDocument();
    expect(screen.queryByText("$250.00 / week")).not.toBeInTheDocument();
    expect(screen.queryByText(/seats available/)).not.toBeInTheDocument();
  });

  it("shows an error when the program can't be loaded", async () => {
    // Seed the cache into an error state (prefetch swallows the rejection) so the test exercises the
    // error UI without a live mount-time query rejection floating as unhandled in RQ v5 + vitest.
    const qc = newClient();
    await qc.prefetchQuery({ queryKey: ["portal-program", "p1"], queryFn: () => Promise.reject(new Error("nope")) });
    renderDetail(qc);
    expect(await screen.findByText(/Couldn.t load this program: nope/)).toBeInTheDocument();
  });
});

import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import type { ReactNode } from "react";
import type { PublicProgram } from "./publicApi";

const h = vi.hoisted(() => ({ navigate: vi.fn(), getPublicPrograms: vi.fn() }));

vi.mock("react-leaflet", () => ({
  MapContainer: ({ children }: { children: ReactNode }) => <div data-testid="map">{children}</div>,
  TileLayer: () => null,
  Marker: ({ children }: { children: ReactNode }) => <div>{children}</div>,
  Popup: ({ children }: { children: ReactNode }) => <div>{children}</div>
}));
vi.mock("react-leaflet-cluster", () => ({ default: ({ children }: { children: ReactNode }) => <div>{children}</div> }));
vi.mock("./publicApi", () => ({ getPublicPrograms: () => h.getPublicPrograms() }));
vi.mock("react-router-dom", async (orig) => ({
  ...(await orig<typeof import("react-router-dom")>()),
  useNavigate: () => h.navigate
}));

import { HeroSearch } from "./HeroSearch";

const PROGRAMS: PublicProgram[] = [
  { id: "1", programNumber: 18, specialtyName: "Internal Medicine", programType: "InPerson", city: "Irvine", state: "California", retailAmountPerWeek: 499, minWeeksPerRotation: 2, instantApproval: true, imageUrl: null },
  { id: "2", programNumber: 17, specialtyName: "General Surgery", programType: "InPerson", city: "Englewood", state: "New Jersey", retailAmountPerWeek: 499, minWeeksPerRotation: 4, instantApproval: false, imageUrl: null }
];

function renderHero() {
  return render(<MemoryRouter><HeroSearch /></MemoryRouter>);
}

describe("HeroSearch", () => {
  beforeEach(() => {
    h.navigate.mockClear();
    h.getPublicPrograms.mockReset().mockResolvedValue(PROGRAMS);
  });

  it("renders the search bar, filters and the program results from the public feed", async () => {
    renderHero();
    expect(screen.getByPlaceholderText("What are you searching for?")).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Specialties" })).toBeInTheDocument();
    // Results load from the (mocked) public feed — scope to the result card (the specialty name also
    // appears as a dropdown <option>).
    expect(await screen.findByText("Internal Medicine", { selector: ".result-spec" })).toBeInTheDocument();
    expect(screen.getByText("General Surgery", { selector: ".result-spec" })).toBeInTheDocument();
    // Total price = retail/week × min weeks (499 × 2 = 998).
    expect(screen.getByText("$998")).toBeInTheDocument();
  });

  it("filters the results by specialty", async () => {
    renderHero();
    await screen.findByText("Internal Medicine", { selector: ".result-spec" });
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Specialties" }), "General Surgery");
    expect(screen.queryByText("Internal Medicine", { selector: ".result-spec" })).not.toBeInTheDocument();
    expect(screen.getByText("General Surgery", { selector: ".result-spec" })).toBeInTheDocument();
  });

  it("narrows by price band, duration and term — and shows the empty state", async () => {
    renderHero();
    await screen.findByText("Internal Medicine", { selector: ".result-spec" });

    // Pricing band $1001-$2000: IM total = 998 (out), GS total = 1996 (in).
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Pricing" }), "$1001 - $2000");
    expect(screen.queryByText("Internal Medicine", { selector: ".result-spec" })).not.toBeInTheDocument();
    expect(screen.getByText("General Surgery", { selector: ".result-spec" })).toBeInTheDocument();

    // A non-matching free-text term empties the list.
    await userEvent.type(screen.getByPlaceholderText("What are you searching for?"), "zzzznomatch");
    expect(screen.getByText(/No programs match/)).toBeInTheDocument();
  });

  it("prompts sign-in (portal) when an anonymous visitor searches or opens a program", async () => {
    renderHero();
    await screen.findByText("Internal Medicine", { selector: ".result-spec" });
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(h.navigate).toHaveBeenCalledWith("/portal");

    h.navigate.mockClear();
    await userEvent.click(screen.getAllByRole("button", { name: /View program/ })[0]);
    expect(h.navigate).toHaveBeenCalledWith("/portal");
  });

  it("renders the Clinical Needs, Ratings and Sort controls and gates them behind sign-in", async () => {
    renderHero();
    await screen.findByText("Internal Medicine", { selector: ".result-spec" });

    // Clinical Needs (program tags) — present, login-gated.
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Clinical Needs" }), "Most Popular");
    expect(h.navigate).toHaveBeenCalledWith("/portal");

    h.navigate.mockClear();
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Ratings" }), "5");
    expect(h.navigate).toHaveBeenCalledWith("/portal");

    h.navigate.mockClear();
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Sort by" }), "Price (Low to High)");
    expect(h.navigate).toHaveBeenCalledWith("/portal");
  });

  it("shows the Reset filters control once a filter is active and clears it", async () => {
    renderHero();
    await screen.findByText("Internal Medicine", { selector: ".result-spec" });

    expect(screen.queryByRole("button", { name: "Reset filters" })).not.toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Specialties" }), "General Surgery");
    expect(screen.queryByText("Internal Medicine", { selector: ".result-spec" })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Reset filters" }));
    // Both programs are back after the reset.
    expect(screen.getByText("Internal Medicine", { selector: ".result-spec" })).toBeInTheDocument();
    expect(screen.getByText("General Surgery", { selector: ".result-spec" })).toBeInTheDocument();
  });
});

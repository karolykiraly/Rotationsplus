import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import type { PublicProgram } from "./publicApi";

const h = vi.hoisted(() => ({ navigate: vi.fn(), getPublicPrograms: vi.fn() }));

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

  it("renders the hero headline, subtitle, search bar and the eight filter dropdowns", async () => {
    renderHero();
    expect(screen.getByText("Find Your Perfect")).toBeInTheDocument();
    expect(screen.getByText(/Gain Valuable Clinical Experience/)).toBeInTheDocument();
    expect(screen.getByPlaceholderText("What are you searching for?")).toBeInTheDocument();
    for (const name of ["Specialties", "Program Type", "Clinical Needs", "Ratings", "City", "State", "Duration", "Pricing"]) {
      expect(screen.getByRole("combobox", { name })).toBeInTheDocument();
    }
    // Dropdown options are populated from the anonymous public catalog feed.
    expect(await screen.findByRole("option", { name: "Internal Medicine" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Irvine" })).toBeInTheDocument();
  });

  it("does not render the results/map for an anonymous visitor — results are gated behind login", () => {
    renderHero();
    expect(document.querySelector(".leaflet-container")).toBeNull();
    expect(screen.queryByRole("button", { name: /View program/ })).not.toBeInTheDocument();
  });

  it("routes an anonymous visitor to the customer sign-in (portal) when they run a search", async () => {
    renderHero();
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(h.navigate).toHaveBeenCalledWith("/portal");
  });
});

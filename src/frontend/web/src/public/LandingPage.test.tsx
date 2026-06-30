import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

// The hero pulls dropdown options from the public catalog feed; stub it for a headless render.
vi.mock("./publicApi", () => ({ getPublicPrograms: vi.fn().mockResolvedValue([]) }));

import { LandingPage } from "./LandingPage";

function renderLanding() {
  return render(
    <MemoryRouter>
      <LandingPage />
    </MemoryRouter>
  );
}

describe("LandingPage", () => {
  it("renders the hero search, reviews and all marketing sections", () => {
    renderLanding();
    expect(screen.getByText("Clinical Experience")).toBeInTheDocument(); // hero accent
    expect(screen.getByPlaceholderText("What are you searching for?")).toBeInTheDocument(); // hero search
    expect(screen.getByText("What Our Customers Say")).toBeInTheDocument(); // reviews
    expect(screen.getByText("Our Benefits")).toBeInTheDocument();
    expect(screen.getByText("How it works")).toBeInTheDocument();
    expect(screen.getByText("Testimonials")).toBeInTheDocument();
    expect(screen.getByText("Our Partners")).toBeInTheDocument();
    expect(screen.getByText("Fernando O.")).toBeInTheDocument();
  });

  it("keeps the Search Programs CTAs pointing at the customer sign-up (portal) and partners out", () => {
    renderLanding();
    const search = screen.getAllByRole("link", { name: "Search Programs" });
    expect(search.length).toBeGreaterThan(0);
    expect(search[0]).toHaveAttribute("href", "/portal");
    expect(screen.getByRole("link", { name: "Learn More" })).toHaveAttribute("href", "/for-preceptors");
    expect(screen.getByRole("link", { name: /ArcherReview/ })).toHaveAttribute("href", "https://www.archerreview.com/");
  });
});

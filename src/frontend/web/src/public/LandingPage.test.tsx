import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { LandingPage } from "./LandingPage";

function renderLanding() {
  return render(
    <MemoryRouter>
      <LandingPage />
    </MemoryRouter>
  );
}

describe("LandingPage", () => {
  it("renders the hero and all marketing sections", () => {
    renderLanding();
    expect(screen.getByText("Clinical Experience")).toBeInTheDocument(); // hero accent
    expect(screen.getByText("Our Benefits")).toBeInTheDocument();
    expect(screen.getByText("How it works")).toBeInTheDocument();
    expect(screen.getByText("Testimonials")).toBeInTheDocument();
    expect(screen.getByText("Our Partners")).toBeInTheDocument();
    expect(screen.getByText("Fernando O.")).toBeInTheDocument();
  });

  it("sends the primary CTA into the customer sign-up (portal) and partners out", () => {
    renderLanding();
    const search = screen.getAllByRole("link", { name: "Search Programs" });
    expect(search.length).toBeGreaterThan(0);
    expect(search[0]).toHaveAttribute("href", "/portal");
    expect(screen.getByRole("link", { name: "For Preceptors" })).toHaveAttribute("href", "/for-preceptors");
    expect(screen.getByRole("link", { name: /ArcherReview/ })).toHaveAttribute("href", "https://www.archerreview.com/");
  });
});

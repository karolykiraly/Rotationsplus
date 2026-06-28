import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AboutPage } from "./AboutPage";

describe("AboutPage", () => {
  it("renders the mission, values and links to the team page", () => {
    render(
      <MemoryRouter>
        <AboutPage />
      </MemoryRouter>
    );
    expect(screen.getByRole("heading", { name: "About RotationsPlus" })).toBeInTheDocument();
    expect(screen.getByText("Our Mission")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Trust" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Search Programs" })).toHaveAttribute("href", "/portal");
    expect(screen.getByRole("link", { name: "Meet Our Team" })).toHaveAttribute("href", "/our-team");
  });
});

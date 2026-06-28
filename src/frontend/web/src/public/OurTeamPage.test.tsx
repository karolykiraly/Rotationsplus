import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { OurTeamPage } from "./OurTeamPage";

describe("OurTeamPage", () => {
  it("renders the team members and the sign-up CTA", () => {
    render(
      <MemoryRouter>
        <OurTeamPage />
      </MemoryRouter>
    );
    expect(screen.getByRole("heading", { name: "About Us" })).toBeInTheDocument();
    expect(screen.getByText("Omer Malik")).toBeInTheDocument();
    expect(screen.getByText("Charles Kiraly")).toBeInTheDocument();
    expect(screen.getByText("Meet Our Team")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Sign Up" })).toHaveAttribute("href", "/portal");
  });
});

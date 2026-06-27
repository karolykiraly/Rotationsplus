import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { Footer } from "./Footer";

describe("Footer", () => {
  it("renders the marketing nav links, legal links and contact", () => {
    render(
      <MemoryRouter>
        <Footer />
      </MemoryRouter>
    );

    expect(screen.getByRole("link", { name: "For Preceptors" })).toHaveAttribute("href", "/for-preceptors");
    expect(screen.getByRole("link", { name: "Privacy Policy" })).toHaveAttribute("href", "/privacy-policy");
    expect(screen.getByRole("link", { name: "Terms of Service" })).toHaveAttribute("href", "/terms");
    expect(screen.getByText(/info@rotationsplus\.com/)).toBeInTheDocument();
    expect(screen.getByText(/RotationsPlus LLC/)).toBeInTheDocument();
  });
});

import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PublicComingSoon } from "./PublicComingSoon";

describe("PublicComingSoon", () => {
  it("renders the page title and a link home", () => {
    render(
      <MemoryRouter>
        <PublicComingSoon title="About" />
      </MemoryRouter>
    );
    expect(screen.getByRole("heading", { name: "About" })).toBeInTheDocument();
    expect(screen.getByText(/coming soon/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /back to home/i })).toHaveAttribute("href", "/");
  });
});

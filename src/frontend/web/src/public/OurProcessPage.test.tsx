import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { OurProcessPage } from "./OurProcessPage";

describe("OurProcessPage", () => {
  it("renders the six steps with CTAs to sign-up", () => {
    render(
      <MemoryRouter>
        <OurProcessPage />
      </MemoryRouter>
    );
    expect(screen.getByRole("heading", { name: "Our Process" })).toBeInTheDocument();
    expect(screen.getByText("Create Your Login")).toBeInTheDocument();
    expect(screen.getByText("It's Rotation Time")).toBeInTheDocument();
    const ctas = screen.getAllByRole("link", { name: "Search Programs" });
    expect(ctas.length).toBeGreaterThan(0);
    ctas.forEach((c) => expect(c).toHaveAttribute("href", "/portal"));
  });
});

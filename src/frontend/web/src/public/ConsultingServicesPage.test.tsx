import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ConsultingServicesPage } from "./ConsultingServicesPage";

describe("ConsultingServicesPage", () => {
  it("renders the consulting hero, benefits and process with CTAs to sign-up", () => {
    render(
      <MemoryRouter>
        <ConsultingServicesPage />
      </MemoryRouter>
    );
    expect(
      screen.getByRole("heading", { name: /Get the Support you Need from Top Tier Physician Consultants/ })
    ).toBeInTheDocument();
    expect(screen.getByText("ERAS Application")).toBeInTheDocument();
    expect(screen.getByText(/Don't Settle for Outsourced Editors/)).toBeInTheDocument();
    screen.getAllByRole("link").forEach((c) => expect(c).toHaveAttribute("href", "/portal"));
  });
});

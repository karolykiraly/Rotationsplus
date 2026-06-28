import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ForPreceptorsPage } from "./ForPreceptorsPage";

function renderPage() {
  return render(
    <MemoryRouter>
      <ForPreceptorsPage />
    </MemoryRouter>
  );
}

describe("ForPreceptorsPage", () => {
  it("renders the preceptor hero, benefits, process and FAQ with CTAs to sign-up", () => {
    renderPage();
    expect(screen.getByRole("heading", { name: /Take the Next Step as a Clinical Preceptor/ })).toBeInTheDocument();
    expect(screen.getByText("On Time Payments")).toBeInTheDocument();
    expect(screen.getByText("The Process to Onboard")).toBeInTheDocument();
    // Every CTA routes into the customer (CIAM) sign-up.
    const ctas = screen.getAllByRole("link");
    expect(ctas.length).toBeGreaterThan(0);
    ctas.forEach((c) => expect(c).toHaveAttribute("href", "/portal"));
  });

  it("expands an FAQ answer on click", async () => {
    renderPage();
    expect(screen.queryByText(/upload their CV/)).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /How do you screen your students/ }));
    expect(screen.getByText(/upload their CV/)).toBeInTheDocument();
  });
});

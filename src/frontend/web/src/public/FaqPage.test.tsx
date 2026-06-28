import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { FaqPage } from "./FaqPage";

describe("FaqPage", () => {
  it("shows the first category by default and switches categories via tabs", async () => {
    render(<FaqPage />);
    expect(screen.getByRole("heading", { name: "FAQ" })).toBeInTheDocument();

    // Requirements (default tab) question is present; a Payment question is not yet.
    expect(screen.getByRole("button", { name: /Who is eligible to apply/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /What modes of payment/ })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("tab", { name: "Payment" }));
    expect(screen.getByRole("button", { name: /What modes of payment/ })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Who is eligible to apply/ })).not.toBeInTheDocument();
  });

  it("expands a multi-bullet answer", async () => {
    render(<FaqPage />);
    await userEvent.click(screen.getByRole("button", { name: /Who is eligible to apply/ }));
    expect(screen.getByText("International Medical Graduates (IMG)")).toBeInTheDocument();
  });
});

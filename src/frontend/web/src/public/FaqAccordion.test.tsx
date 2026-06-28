import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { FaqAccordion } from "./FaqAccordion";

const ITEMS = [
  { q: "First question?", a: "First answer." },
  { q: "Second question?", a: "Second answer." }
];

describe("FaqAccordion", () => {
  it("hides answers until a question is expanded, and is exclusive", async () => {
    render(<FaqAccordion items={ITEMS} />);
    expect(screen.queryByText("First answer.")).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /First question/ }));
    expect(screen.getByText("First answer.")).toBeInTheDocument();

    // Opening the second collapses the first (single-open accordion).
    await userEvent.click(screen.getByRole("button", { name: /Second question/ }));
    expect(screen.getByText("Second answer.")).toBeInTheDocument();
    expect(screen.queryByText("First answer.")).not.toBeInTheDocument();

    // Clicking an open item closes it.
    await userEvent.click(screen.getByRole("button", { name: /Second question/ }));
    expect(screen.queryByText("Second answer.")).not.toBeInTheDocument();
  });
});

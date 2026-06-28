import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { CustomerReviews } from "./CustomerReviews";

describe("CustomerReviews", () => {
  it("renders the Google-reviews header and the first review (static, no external embed)", () => {
    render(<CustomerReviews />);
    expect(screen.getByRole("heading", { name: "What Our Customers Say" })).toBeInTheDocument();
    expect(screen.getByText("4.9")).toBeInTheDocument();
    // Carousel starts on the first review.
    expect(screen.getByText("Roopesh Reddy")).toBeInTheDocument();
    expect(screen.queryByText("Alexander Vega Real")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Review us on Google/ })).toHaveAttribute(
      "href",
      "https://www.google.com/search?q=RotationsPlus+reviews"
    );
  });

  it("advances to the next review via the arrow control", async () => {
    render(<CustomerReviews />);
    await userEvent.click(screen.getByRole("button", { name: "Next review" }));
    expect(screen.getByText("Alexander Vega Real")).toBeInTheDocument();
    expect(screen.queryByText("Roopesh Reddy")).not.toBeInTheDocument();
  });

  it("renders a dot per review and jumps to the selected one", async () => {
    render(<CustomerReviews />);
    const dots = screen.getAllByRole("button", { name: /^Show review from / });
    expect(dots).toHaveLength(4);
    await userEvent.click(screen.getByRole("button", { name: "Show review from Kinda Ghaffari" }));
    expect(screen.getByText("Kinda Ghaffari")).toBeInTheDocument();
  });

  it("wraps from the first review to the last when going previous", async () => {
    render(<CustomerReviews />);
    await userEvent.click(screen.getByRole("button", { name: "Previous review" }));
    expect(screen.getByText("Kinda Ghaffari")).toBeInTheDocument();
  });
});

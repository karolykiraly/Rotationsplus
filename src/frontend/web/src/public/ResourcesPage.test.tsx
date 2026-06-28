import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { ResourcesPage } from "./ResourcesPage";

describe("ResourcesPage", () => {
  it("renders the five resource articles", () => {
    render(<ResourcesPage />);
    expect(screen.getByRole("heading", { name: "Resources" })).toBeInTheDocument();
    expect(
      screen.getByText("How Clinical Rotations Help with Med School Admission")
    ).toBeInTheDocument();
    expect(screen.getByText("Help Me Choose My Medical Specialty")).toBeInTheDocument();
    // The placeholder 6th legacy article is intentionally dropped (production defect).
    expect(screen.queryByText(/don't have a 6th article/i)).not.toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 2 })).toHaveLength(5);
  });

  it("renders the content-type filter chips (Webinar active) as on the live site", () => {
    render(<ResourcesPage />);
    expect(screen.getByText("Content types")).toBeInTheDocument();
    for (const type of ["Webinar", "Articles", "Video", "Guide", "Case Study", "Report"]) {
      expect(screen.getByRole("button", { name: type })).toBeInTheDocument();
    }
    // Webinar is the active (filled) chip.
    expect(screen.getByRole("button", { name: "Webinar" })).toHaveClass("btn-primary");
    expect(screen.getByRole("button", { name: "Articles" })).toHaveClass("btn-outline");
  });

  it("renders a Learn more button on each article and hides Show more under the page size", () => {
    render(<ResourcesPage />);
    expect(screen.getAllByRole("button", { name: "Learn more" })).toHaveLength(5);
    // With 5 articles (< the 10 page size) the Show more control stays hidden, matching production.
    expect(screen.queryByRole("button", { name: "Show more" })).not.toBeInTheDocument();
  });
});

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
    // The placeholder 6th legacy article is intentionally dropped.
    expect(screen.queryByText(/don't have a 6th article/i)).not.toBeInTheDocument();
    expect(screen.getAllByRole("heading", { level: 2 })).toHaveLength(5);
  });
});

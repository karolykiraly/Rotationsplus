import { describe, it, expect } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ProgramImage } from "./ProgramImage";

describe("ProgramImage", () => {
  it("renders the image when a url is given", () => {
    render(<ProgramImage url="https://blob/x.jpg?sas" className="rcard-photo" alt="" />);
    const img = document.querySelector<HTMLImageElement>("img.rcard-photo");
    expect(img).not.toBeNull();
    expect(img!.src).toContain("https://blob/x.jpg");
  });

  it("renders nothing (placeholder shows through) when there is no url", () => {
    render(<ProgramImage url={null} className="rcard-photo" alt="" />);
    expect(document.querySelector("img.rcard-photo")).toBeNull();
  });

  it("hides the image on load error so the placeholder shows", () => {
    render(<ProgramImage url="https://blob/broken.jpg?sas" className="pd-photo" alt="X program" />);
    const img = screen.getByRole("img", { name: "X program" });
    fireEvent.error(img);
    expect(document.querySelector("img.pd-photo")).toBeNull();
  });

  it("revives the image when the url changes after a failure (fresh SAS on refetch)", () => {
    // The regression: a stale failure must not suppress a new, valid URL for the same slot.
    const { rerender } = render(<ProgramImage url="https://blob/expired.jpg?old" className="rcard-photo" alt="" />);
    fireEvent.error(document.querySelector("img.rcard-photo")!);
    expect(document.querySelector("img.rcard-photo")).toBeNull();

    rerender(<ProgramImage url="https://blob/fresh.jpg?new" className="rcard-photo" alt="" />);
    const img = document.querySelector<HTMLImageElement>("img.rcard-photo");
    expect(img).not.toBeNull();
    expect(img!.src).toContain("https://blob/fresh.jpg");
  });
});

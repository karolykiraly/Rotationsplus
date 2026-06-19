import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Pagination } from "./Pagination";

describe("Pagination", () => {
  it("renders nothing for a single page", () => {
    const { container } = render(<Pagination page={1} pageSize={10} totalItems={5} onChange={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it("disables First/Previous on page 1 and navigates", async () => {
    const onChange = vi.fn();
    render(<Pagination page={1} pageSize={10} totalItems={50} onChange={onChange} />);
    expect(screen.getByRole("button", { name: "First" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(onChange).toHaveBeenCalledWith(2);
    await userEvent.click(screen.getByRole("button", { name: "Last" }));
    expect(onChange).toHaveBeenCalledWith(5);
    await userEvent.click(screen.getByRole("button", { name: "3" }));
    expect(onChange).toHaveBeenCalledWith(3);
  });

  it("marks the active page and disables Next/Last on the final page", () => {
    render(<Pagination page={5} pageSize={10} totalItems={50} onChange={() => {}} />);
    expect(screen.getByRole("button", { name: "5" })).toHaveClass("active");
    expect(screen.getByRole("button", { name: "Next" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Last" })).toBeDisabled();
  });
});

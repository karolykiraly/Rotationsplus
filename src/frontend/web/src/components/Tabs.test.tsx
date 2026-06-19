import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Tabs } from "./Tabs";

describe("Tabs", () => {
  it("marks the active tab and reports clicks", async () => {
    const onChange = vi.fn();
    render(<Tabs labels={["A", "B", "C"]} active={0} onChange={onChange} />);
    expect(screen.getByRole("tab", { name: "A" })).toHaveClass("active");
    expect(screen.getByRole("tab", { name: "B" })).not.toHaveClass("active");
    await userEvent.click(screen.getByRole("tab", { name: "B" }));
    expect(onChange).toHaveBeenCalledWith(1);
  });

  it("activates on Enter", async () => {
    const onChange = vi.fn();
    render(<Tabs labels={["A", "B"]} active={0} onChange={onChange} />);
    screen.getByRole("tab", { name: "B" }).focus();
    await userEvent.keyboard("{Enter}");
    expect(onChange).toHaveBeenCalledWith(1);
  });
});

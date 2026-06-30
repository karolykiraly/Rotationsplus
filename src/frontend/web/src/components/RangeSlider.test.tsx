import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { RangeSlider } from "./RangeSlider";

describe("RangeSlider", () => {
  it("renders two thumbs and reports a new low/high while keeping them from crossing", () => {
    const onChange = vi.fn();
    const { rerender } = render(
      <RangeSlider min={0} max={15000} value={[0, 15000]} onChange={onChange}
        minLabel="Min amount" maxLabel="Max amount" />
    );

    const lo = screen.getByLabelText("Min amount");
    const hi = screen.getByLabelText("Max amount");
    expect(lo).toHaveValue("0");
    expect(hi).toHaveValue("15000");

    // Dragging the low thumb up reports [5000, 15000].
    fireEvent.change(lo, { target: { value: "5000" } });
    expect(onChange).toHaveBeenLastCalledWith([5000, 15000]);

    // With the low thumb at 5000, pushing the high thumb below it clamps to the low value (no crossing).
    rerender(
      <RangeSlider min={0} max={15000} value={[5000, 15000]} onChange={onChange}
        minLabel="Min amount" maxLabel="Max amount" />
    );
    fireEvent.change(screen.getByLabelText("Max amount"), { target: { value: "2000" } });
    expect(onChange).toHaveBeenLastCalledWith([5000, 5000]);
  });
});

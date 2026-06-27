import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useDebouncedValue } from "./useDebouncedValue";

/** Advance fake timers inside act() so React flushes the debounced state update. */
const advance = (ms: number) => act(() => { vi.advanceTimersByTime(ms); });

describe("useDebouncedValue", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it("returns the initial value immediately", () => {
    const { result } = renderHook(() => useDebouncedValue("a", 300));
    expect(result.current).toBe("a");
  });

  it("only updates after the value has been stable for the delay", () => {
    const { result, rerender } = renderHook(({ v }) => useDebouncedValue(v, 300), {
      initialProps: { v: "a" }
    });

    rerender({ v: "ab" });
    rerender({ v: "abc" });
    // Still the old value before the delay elapses.
    expect(result.current).toBe("a");

    advance(300);
    expect(result.current).toBe("abc"); // settles on the latest, not the intermediate
  });

  it("resets the timer on each change (no early emit)", () => {
    const { result, rerender } = renderHook(({ v }) => useDebouncedValue(v, 300), {
      initialProps: { v: "x" }
    });

    rerender({ v: "xy" });
    advance(200); // not yet
    expect(result.current).toBe("x");
    rerender({ v: "xyz" });
    advance(200); // 200 since the last change — still not 300
    expect(result.current).toBe("x");
    advance(100); // now 300 since the last change
    expect(result.current).toBe("xyz");
  });
});

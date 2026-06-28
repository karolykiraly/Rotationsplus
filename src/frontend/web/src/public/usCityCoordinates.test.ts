import { describe, it, expect } from "vitest";
import { cityCoordinates, US_CENTER, US_ZOOM } from "./usCityCoordinates";

describe("cityCoordinates", () => {
  it("resolves known cities case-insensitively", () => {
    expect(cityCoordinates("Irvine")).toEqual([33.6846, -117.8265]);
    expect(cityCoordinates("  new york ")).toEqual([40.7128, -74.006]);
  });

  it("returns null for unknown or empty cities", () => {
    expect(cityCoordinates("Nowhereville")).toBeNull();
    expect(cityCoordinates(null)).toBeNull();
    expect(cityCoordinates("")).toBeNull();
  });

  it("exposes the US map center + zoom", () => {
    expect(US_CENTER).toHaveLength(2);
    expect(US_ZOOM).toBe(4);
  });
});

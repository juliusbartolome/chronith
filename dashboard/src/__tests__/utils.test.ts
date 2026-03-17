import { describe, it, expect } from "vitest";
import { cn, formatDate } from "@/lib/utils";
import { formatPrice } from "@/lib/format";

describe("cn utility", () => {
  it("merges class names", () => {
    expect(cn("foo", "bar")).toBe("foo bar");
  });

  it("handles conditional classes", () => {
    expect(cn("foo", false && "bar", "baz")).toBe("foo baz");
  });

  it("deduplicates tailwind classes", () => {
    // tailwind-merge keeps the last conflicting utility
    expect(cn("p-2", "p-4")).toBe("p-4");
  });
});

describe("formatDate", () => {
  it("formats an ISO date string to a locale string", () => {
    const result = formatDate("2024-06-01T10:00:00Z");
    expect(typeof result).toBe("string");
    expect(result.length).toBeGreaterThan(0);
  });

  it("returns a non-empty string for any valid ISO date", () => {
    const result = formatDate("2020-01-15T00:00:00.000Z");
    expect(result).toBeTruthy();
  });
});

describe("formatPrice", () => {
  it("returns 'Free' when price is 0", () => {
    expect(formatPrice(0)).toBe("Free");
  });

  it("formats non-zero centavos as PHP currency", () => {
    const result = formatPrice(10000);
    expect(result).toContain("100.00");
    expect(result).toMatch(/₱/);
  });

  it("formats 50 centavos correctly", () => {
    const result = formatPrice(50);
    expect(result).toContain("0.50");
  });
});

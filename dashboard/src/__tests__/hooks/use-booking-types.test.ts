import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import { useBookingTypes, useBookingType } from "@/hooks/use-booking-types";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
}

describe("useBookingTypes", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches booking types list", async () => {
    const mockData = {
      items: [{ id: "1", name: "Haircut", slug: "haircut" }],
      totalCount: 1,
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useBookingTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });
});

describe("useBookingType", () => {
  it("is disabled when slug is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(() => useBookingType(""), {
      wrapper: createWrapper(),
    });
    expect(result.current.fetchStatus).toBe("idle");
    expect(global.fetch).not.toHaveBeenCalled();
  });
});

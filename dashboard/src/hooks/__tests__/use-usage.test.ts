import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useUsage } from "../use-usage";

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
};

describe("useUsage", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches usage from /api/tenant/usage", async () => {
    const mockUsage = {
      bookingTypesUsed: 2,
      bookingTypesLimit: 3,
      staffMembersUsed: 1,
      staffMembersLimit: 2,
      bookingsThisMonth: 10,
      bookingsPerMonthLimit: 50,
      customersUsed: 5,
      customersLimit: 100,
      planName: "Free",
    };
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockUsage,
    } as Response);

    const { result } = renderHook(() => useUsage(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith("/api/tenant/usage");
    expect(result.current.data).toEqual(mockUsage);
  });
});

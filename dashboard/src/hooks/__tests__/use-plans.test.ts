import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { usePlans } from "../use-plans";

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  }
  return Wrapper;
};

describe("usePlans", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches plans from /api/plans", async () => {
    const mockPlans = [
      {
        id: "plan-1",
        name: "Free",
        maxBookingTypes: 3,
        maxStaffMembers: 2,
        maxBookingsPerMonth: 50,
        maxCustomers: 100,
        notificationsEnabled: false,
        analyticsEnabled: false,
        customBrandingEnabled: false,
        apiAccessEnabled: false,
        auditLogEnabled: false,
        priceCentavos: 0,
        sortOrder: 1,
      },
    ];
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockPlans,
    } as Response);

    const { result } = renderHook(() => usePlans(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith("/api/plans");
    expect(result.current.data).toEqual(mockPlans);
  });
});

import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import {
  useSubscription,
  useChangePlan,
  useCancelSubscription,
} from "../use-subscription";

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
};

describe("useSubscription", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches subscription from /api/tenant/subscription", async () => {
    const mockSub = {
      id: "sub-1",
      tenantId: "tenant-1",
      planId: "plan-1",
      planName: "Free",
      status: "Active",
      trialEndsAt: null,
      currentPeriodStart: "2026-01-01T00:00:00Z",
      currentPeriodEnd: "2026-02-01T00:00:00Z",
      paymentProviderSubscriptionId: null,
      createdAt: "2026-01-01T00:00:00Z",
      cancelledAt: null,
      cancelReason: null,
    };
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockSub,
    } as Response);

    const { result } = renderHook(() => useSubscription(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith("/api/tenant/subscription");
    expect(result.current.data).toEqual(mockSub);
  });
});

describe("useChangePlan", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("calls PUT /api/tenant/subscription", async () => {
    const mockSub = { id: "sub-1", planName: "Pro" };
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockSub,
    } as Response);

    const { result } = renderHook(() => useChangePlan(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.mutate({ newPlanId: "plan-pro" });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith(
      "/api/tenant/subscription",
      expect.objectContaining({ method: "PUT" }),
    );
  });
});

describe("useCancelSubscription", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("calls DELETE /api/tenant/subscription", async () => {
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCancelSubscription(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.mutate({ reason: "No longer needed" });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith(
      "/api/tenant/subscription",
      expect.objectContaining({ method: "DELETE" }),
    );
  });
});

import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import { usePlans } from "@/hooks/use-plans";
import { useUsage } from "@/hooks/use-usage";
import { useSubscription, useChangePlan, useCancelSubscription } from "@/hooks/use-subscription";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

// ─── usePlans ────────────────────────────────────────────────────────────────

describe("usePlans", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches plans and returns data", async () => {
    const mockPlans = [
      {
        id: "plan-1",
        name: "Starter",
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
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockPlans,
    } as Response);

    const { result } = renderHook(() => usePlans(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockPlans);
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: false } as Response);
    const { result } = renderHook(() => usePlans(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Failed to fetch plans");
  });
});

// ─── useUsage ────────────────────────────────────────────────────────────────

describe("useUsage", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches usage and returns data", async () => {
    const mockUsage = {
      bookingTypesUsed: 1,
      bookingTypesLimit: 3,
      staffMembersUsed: 1,
      staffMembersLimit: 2,
      bookingsThisMonth: 5,
      bookingsPerMonthLimit: 50,
      customersUsed: 10,
      customersLimit: 100,
      planName: "Starter",
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockUsage,
    } as Response);

    const { result } = renderHook(() => useUsage(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockUsage);
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: false } as Response);
    const { result } = renderHook(() => useUsage(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Failed to fetch usage");
  });
});

// ─── useSubscription ─────────────────────────────────────────────────────────

describe("useSubscription", () => {
  beforeEach(() => vi.restoreAllMocks());

  const mockSub = {
    id: "sub-1",
    tenantId: "t-1",
    planId: "plan-1",
    planName: "Starter",
    status: "active",
    trialEndsAt: null,
    currentPeriodStart: "2026-01-01T00:00:00Z",
    currentPeriodEnd: "2026-02-01T00:00:00Z",
    paymentProviderSubscriptionId: null,
    createdAt: "2026-01-01T00:00:00Z",
    cancelledAt: null,
    cancelReason: null,
  };

  it("fetches subscription and returns data", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockSub,
    } as Response);

    const { result } = renderHook(() => useSubscription(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockSub);
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: false } as Response);
    const { result } = renderHook(() => useSubscription(), { wrapper: createWrapper() });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Failed to fetch subscription");
  });
});

// ─── useChangePlan ────────────────────────────────────────────────────────────

describe("useChangePlan", () => {
  beforeEach(() => vi.restoreAllMocks());

  const mockSub = {
    id: "sub-1",
    tenantId: "t-1",
    planId: "plan-2",
    planName: "Pro",
    status: "active",
    trialEndsAt: null,
    currentPeriodStart: "2026-01-01T00:00:00Z",
    currentPeriodEnd: "2026-02-01T00:00:00Z",
    paymentProviderSubscriptionId: null,
    createdAt: "2026-01-01T00:00:00Z",
    cancelledAt: null,
    cancelReason: null,
  };

  it("calls PUT and returns updated subscription", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockSub,
    } as Response);

    const { result } = renderHook(() => useChangePlan(), { wrapper: createWrapper() });
    result.current.mutate({ newPlanId: "plan-2" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockSub);
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: false } as Response);
    const { result } = renderHook(() => useChangePlan(), { wrapper: createWrapper() });
    result.current.mutate({ newPlanId: "plan-2" });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Failed to change plan");
  });
});

// ─── useCancelSubscription ────────────────────────────────────────────────────

describe("useCancelSubscription", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("calls DELETE and succeeds", async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: true } as Response);
    const { result } = renderHook(() => useCancelSubscription(), { wrapper: createWrapper() });
    result.current.mutate({ reason: "too expensive" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({ ok: false } as Response);
    const { result } = renderHook(() => useCancelSubscription(), { wrapper: createWrapper() });
    result.current.mutate({ reason: "too expensive" });
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Failed to cancel subscription");
  });
});

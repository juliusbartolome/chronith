import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  usePublicBookingTypes,
  usePublicAvailability,
} from "@/hooks/use-public-booking";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("usePublicBookingTypes", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches booking types from /api/public/{tenantSlug}/booking-types", async () => {
    const mockData = [
      {
        id: "bt-1",
        slug: "haircut",
        name: "Haircut",
        description: "Standard haircut",
        durationMinutes: 60,
        priceCentavos: 50000,
        requiresStaffAssignment: false,
      },
    ];
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => usePublicBookingTypes("my-salon"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/public/my-salon/booking-types",
    );
  });

  it("is disabled when tenantSlug is empty", async () => {
    global.fetch = vi.fn();
    const { result } = renderHook(() => usePublicBookingTypes(""), {
      wrapper: createWrapper(),
    });

    // Should not be fetching
    expect(result.current.isFetching).toBe(false);
    expect(global.fetch).not.toHaveBeenCalled();
  });
});

describe("usePublicAvailability", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches availability from /api/public/{tenantSlug}/{btSlug}/availability", async () => {
    const mockData = {
      date: "2026-03-15",
      slots: ["09:00", "10:00", "11:00"],
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(
      () => usePublicAvailability("my-salon", "haircut", "2026-03-15"),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/public/my-salon/haircut/availability?date=2026-03-15",
    );
  });

  it("is disabled when date is empty", async () => {
    global.fetch = vi.fn();
    const { result } = renderHook(
      () => usePublicAvailability("my-salon", "haircut", ""),
      { wrapper: createWrapper() },
    );

    expect(result.current.isFetching).toBe(false);
    expect(global.fetch).not.toHaveBeenCalled();
  });
});

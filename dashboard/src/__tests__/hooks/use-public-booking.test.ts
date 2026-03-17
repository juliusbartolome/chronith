import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  usePublicBookingTypes,
  usePublicAvailability,
  useCreatePublicBooking,
} from "@/hooks/use-public-booking";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("usePublicBookingTypes", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches booking types for a tenant", async () => {
    const mockData = [{ slug: "haircut", name: "Haircut", durationMinutes: 30 }];
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => usePublicBookingTypes("acme"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
    expect(global.fetch).toHaveBeenCalledWith("/api/public/acme/booking-types");
  });

  it("is disabled when tenantSlug is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(() => usePublicBookingTypes(""), {
      wrapper: createWrapper(),
    });
    expect(result.current.fetchStatus).toBe("idle");
    expect(global.fetch).not.toHaveBeenCalled();
  });

  it("errors on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => usePublicBookingTypes("acme"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("usePublicAvailability", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches availability for a booking type and date", async () => {
    const mockData = { date: "2026-03-15", slots: ["09:00", "10:00"] };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(
      () => usePublicAvailability("acme", "haircut", "2026-03-15"),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/public/acme/haircut/availability?date=2026-03-15",
    );
  });

  it("is disabled when tenantSlug is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(
      () => usePublicAvailability("", "haircut", "2026-03-15"),
      { wrapper: createWrapper() },
    );
    expect(result.current.fetchStatus).toBe("idle");
  });

  it("is disabled when btSlug is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(
      () => usePublicAvailability("acme", "", "2026-03-15"),
      { wrapper: createWrapper() },
    );
    expect(result.current.fetchStatus).toBe("idle");
  });

  it("is disabled when date is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(
      () => usePublicAvailability("acme", "haircut", ""),
      { wrapper: createWrapper() },
    );
    expect(result.current.fetchStatus).toBe("idle");
  });

  it("errors on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(
      () => usePublicAvailability("acme", "haircut", "2026-03-15"),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCreatePublicBooking", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("creates a public booking successfully", async () => {
    const mockData = {
      id: "b1",
      bookingTypeSlug: "haircut",
      status: "PendingPayment",
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCreatePublicBooking("acme"), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      bookingTypeSlug: "haircut",
      date: "2026-03-15",
      startTime: "09:00",
      customerName: "Jane",
      customerEmail: "jane@test.com",
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/public/acme/bookings",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("throws error with message on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      text: async () => "Slot already taken",
    } as Response);

    const { result } = renderHook(() => useCreatePublicBooking("acme"), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      bookingTypeSlug: "haircut",
      date: "2026-03-15",
      startTime: "09:00",
      customerName: "Jane",
      customerEmail: "jane@test.com",
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Slot already taken");
  });

  it("throws generic error when response body is empty", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      text: async () => "",
    } as Response);

    const { result } = renderHook(() => useCreatePublicBooking("acme"), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      bookingTypeSlug: "haircut",
      date: "2026-03-15",
      startTime: "09:00",
      customerName: "Jane",
      customerEmail: "jane@test.com",
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect((result.current.error as Error).message).toBe("Failed to create booking");
  });
});

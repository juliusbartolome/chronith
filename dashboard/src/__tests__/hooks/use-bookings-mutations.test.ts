import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  useBookings,
  useBooking,
  useConfirmBooking,
  useCancelBooking,
} from "@/hooks/use-bookings";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("useBookings with filters", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches with page filter", async () => {
    const mockData = { items: [], totalCount: 0, page: 2, pageSize: 10 };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(
      () => useBookings({ page: 2, pageSize: 10 }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("page=2"),
    );
  });

  it("fetches with status filter", async () => {
    const mockData = { items: [], totalCount: 0 };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(
      () => useBookings({ status: "Confirmed" }),
      { wrapper: createWrapper() },
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("status=Confirmed"),
    );
  });
});

describe("useBooking", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches a single booking", async () => {
    const mockData = { id: "b1", status: "Confirmed" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useBooking("b1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });

  it("is disabled when id is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(() => useBooking(""), {
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

    const { result } = renderHook(() => useBooking("b1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useConfirmBooking", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("confirms a booking successfully", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useConfirmBooking(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("b1");
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/bookings/b1/confirm",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useConfirmBooking(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("b1");
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCancelBooking", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("cancels a booking successfully", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCancelBooking(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ id: "b1", reason: "Changed plans" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/bookings/b1/cancel",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ reason: "Changed plans" }),
      }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCancelBooking(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ id: "b1", reason: "test" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

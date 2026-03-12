import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import { useBookings } from "@/hooks/use-bookings";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
}

describe("useBookings", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("fetches bookings and returns data", async () => {
    const mockData = {
      items: [{ id: "1" }],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useBookings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useBookings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeDefined();
  });
});

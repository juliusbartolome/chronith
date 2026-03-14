import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  useBookingTypes,
  useBookingType,
  useCreateBookingType,
  useUpdateBookingType,
  useDeleteBookingType,
} from "@/hooks/use-booking-types";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("useBookingTypes", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches booking types list", async () => {
    const mockData = { items: [{ slug: "haircut", name: "Haircut" }], totalCount: 1 };
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

  it("fetches with page filter", async () => {
    const mockData = { items: [], totalCount: 0 };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useBookingTypes({ page: 2, pageSize: 5 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("page=2"),
    );
  });

  it("errors on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useBookingTypes(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useBookingType", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches a single booking type by slug", async () => {
    const mockData = { slug: "haircut", name: "Haircut" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useBookingType("haircut"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });

  it("is disabled when slug is empty", () => {
    global.fetch = vi.fn();
    const { result } = renderHook(() => useBookingType(""), {
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

    const { result } = renderHook(() => useBookingType("haircut"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCreateBookingType", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("creates a booking type successfully", async () => {
    const mockData = { slug: "new-type", name: "New Type" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCreateBookingType(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "New Type", durationMinutes: 60 });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/booking-types",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCreateBookingType(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "X" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useUpdateBookingType", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("updates a booking type successfully", async () => {
    const mockData = { slug: "haircut", name: "Updated Haircut" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useUpdateBookingType(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ slug: "haircut", data: { name: "Updated Haircut" } });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/booking-types/haircut",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useUpdateBookingType(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ slug: "haircut", data: {} });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useDeleteBookingType", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("deletes a booking type successfully", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useDeleteBookingType(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("haircut");
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/booking-types/haircut",
      expect.objectContaining({ method: "DELETE" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useDeleteBookingType(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("haircut");
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

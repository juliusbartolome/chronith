import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  useCustomerLogin,
  useCustomerRegister,
  useCustomerLogout,
  useCustomerMe,
  useCustomerBookings,
  useBookingDetail,
} from "@/hooks/use-customer-auth";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("useCustomerLogin", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("logs in successfully", async () => {
    const mockData = { id: "c1", name: "Jane", email: "jane@test.com" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCustomerLogin(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ email: "jane@test.com", password: "pass" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/public/auth/customer/login",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("throws on failed login", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerLogin(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ email: "x@x.com", password: "wrong" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCustomerRegister", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("registers successfully", async () => {
    const mockData = { id: "c2", name: "John", email: "john@test.com" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCustomerRegister(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      name: "John",
      email: "john@test.com",
      password: "pass123",
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("registers with optional phone", async () => {
    const mockData = { id: "c3", name: "Alice" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCustomerRegister(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      name: "Alice",
      email: "alice@test.com",
      password: "pass",
      phone: "+639171234567",
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("throws on failed registration", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerRegister(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "X", email: "x@x.com", password: "p" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCustomerLogout", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("logs out successfully", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerLogout(), {
      wrapper: createWrapper(),
    });

    result.current.mutate();
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/public/auth/customer/logout",
      expect.objectContaining({ method: "POST" }),
    );
  });
});

describe("useCustomerMe", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("returns profile when authenticated", async () => {
    const mockData = { id: "c1", name: "Jane", email: "jane@test.com" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCustomerMe(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });

  it("returns null on 401", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerMe(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toBeNull();
  });

  it("throws on non-401 error", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerMe(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCustomerBookings", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("returns bookings list", async () => {
    const mockData = {
      items: [{ id: "b1", bookingTypeName: "Haircut", status: "Confirmed" }],
      totalCount: 1,
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCustomerBookings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });

  it("returns empty list on 401", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerBookings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual({ items: [], totalCount: 0 });
  });

  it("throws on non-401 error", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCustomerBookings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useBookingDetail", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches booking detail", async () => {
    const mockData = { id: "b1", bookingTypeName: "Haircut", status: "Confirmed" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useBookingDetail("b1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
  });

  it("errors on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useBookingDetail("b1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

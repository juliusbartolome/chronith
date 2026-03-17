import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  useStaffList,
  useStaffMember,
  useCreateStaff,
  useUpdateStaff,
  useUpdateStaffAvailability,
  useDeactivateStaff,
} from "@/hooks/use-staff";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("useStaffList with filters", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches with page and pageSize filters", async () => {
    const mockData = { items: [], totalCount: 0 };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useStaffList({ page: 2, pageSize: 10 }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("page=2"),
    );
  });

  it("fetches with isActive filter", async () => {
    const mockData = { items: [], totalCount: 0 };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useStaffList({ isActive: true }), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("isActive=true"),
    );
  });
});

describe("useStaffMember", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches staff member by id", async () => {
    const mockData = { id: "s1", name: "Alice", isActive: true };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useStaffMember("s1"), {
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

    const { result } = renderHook(() => useStaffMember("s1"), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCreateStaff", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("creates a staff member successfully", async () => {
    const mockData = { id: "s2", name: "Bob", isActive: true };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCreateStaff(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "Bob", email: "bob@test.com" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/staff",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCreateStaff(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "Bob", email: "bob@test.com" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useUpdateStaff", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("updates a staff member successfully", async () => {
    const mockData = { id: "s1", name: "Alice Updated" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useUpdateStaff(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      id: "s1",
      data: { name: "Alice Updated", email: "alice@test.com" },
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/staff/s1",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useUpdateStaff(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ id: "s1", data: { name: "X", email: "x@x.com" } });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useUpdateStaffAvailability", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("updates availability successfully", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useUpdateStaffAvailability(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      id: "s1",
      windows: [{ dayOfWeek: 1, startTime: "09:00", endTime: "17:00" }],
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/staff/s1/availability",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useUpdateStaffAvailability(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ id: "s1", windows: [] });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useDeactivateStaff", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("deactivates a staff member", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useDeactivateStaff(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("s1");
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/staff/s1",
      expect.objectContaining({ method: "DELETE" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useDeactivateStaff(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("s1");
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

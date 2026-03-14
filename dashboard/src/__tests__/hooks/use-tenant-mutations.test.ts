import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  useTenantProfile,
  useUpdateTenantProfile,
  useTenantAuthConfig,
  useUpdateTenantAuthConfig,
  useApiKeys,
  useCreateApiKey,
  useRevokeApiKey,
} from "@/hooks/use-tenant";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("useTenantProfile extended", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("errors on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useTenantProfile(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useUpdateTenantProfile", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("updates tenant profile successfully", async () => {
    const mockData = { name: "New Name", timezone: "Asia/Manila" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useUpdateTenantProfile(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "New Name", timezone: "Asia/Manila" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/settings/profile",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useUpdateTenantProfile(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ name: "X", timezone: "UTC" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useTenantAuthConfig", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("fetches auth config", async () => {
    const mockData = { googleClientId: "g123", githubClientId: null };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useTenantAuthConfig(), {
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

    const { result } = renderHook(() => useTenantAuthConfig(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useUpdateTenantAuthConfig", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("updates auth config successfully", async () => {
    const mockData = { googleClientId: "new-id" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useUpdateTenantAuthConfig(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ googleClientId: "new-id" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/settings/auth",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useUpdateTenantAuthConfig(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({});
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useApiKeys extended", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("errors on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useApiKeys(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useCreateApiKey", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("creates an API key successfully", async () => {
    const mockData = { id: "k1", label: "CI Key", key: "secret" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCreateApiKey(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ label: "CI Key", role: "Staff" });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/settings/api-keys",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("creates with optional expiresAt", async () => {
    const mockData = { id: "k2", label: "Expiring Key" };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useCreateApiKey(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({
      label: "Expiring Key",
      role: "Staff",
      expiresAt: "2027-01-01T00:00:00Z",
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useCreateApiKey(), {
      wrapper: createWrapper(),
    });

    result.current.mutate({ label: "X", role: "Staff" });
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

describe("useRevokeApiKey", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("revokes an API key successfully", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useRevokeApiKey(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("k1");
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(global.fetch).toHaveBeenCalledWith(
      "/api/settings/api-keys/k1",
      expect.objectContaining({ method: "DELETE" }),
    );
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useRevokeApiKey(), {
      wrapper: createWrapper(),
    });

    result.current.mutate("k1");
    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});

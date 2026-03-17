import { renderHook, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from "@/hooks/use-tenant-settings";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("useTenantSettings", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("fetches tenant settings from /api/tenant/settings", async () => {
    const mockData = {
      id: "abc",
      tenantId: "t1",
      logoUrl: null,
      primaryColor: "#2563EB",
      accentColor: null,
      customDomain: null,
      bookingPageEnabled: true,
      welcomeMessage: null,
      termsUrl: null,
      privacyUrl: null,
      updatedAt: "2026-03-01T00:00:00Z",
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockData,
    } as Response);

    const { result } = renderHook(() => useTenantSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockData);
    expect(global.fetch).toHaveBeenCalledWith("/api/tenant/settings");
  });

  it("throws on non-ok response", async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      json: async () => ({}),
    } as Response);

    const { result } = renderHook(() => useTenantSettings(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeDefined();
  });
});

describe("useUpdateTenantSettings", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("calls PUT /api/tenant/settings with the payload", async () => {
    const updated = {
      id: "abc",
      tenantId: "t1",
      logoUrl: "https://example.com/logo.png",
      primaryColor: "#FF0000",
      accentColor: null,
      customDomain: null,
      bookingPageEnabled: true,
      welcomeMessage: "Hello!",
      termsUrl: null,
      privacyUrl: null,
      updatedAt: "2026-03-12T00:00:00Z",
    };
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => updated,
    } as Response);

    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    const wrapper = ({ children }: { children: React.ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children);

    const { result } = renderHook(() => useUpdateTenantSettings(), { wrapper });

    await result.current.mutateAsync({
      primaryColor: "#FF0000",
      welcomeMessage: "Hello!",
    });

    expect(global.fetch).toHaveBeenCalledWith(
      "/api/tenant/settings",
      expect.objectContaining({ method: "PUT" }),
    );
  });
});

import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import {
  useCustomerLogin,
  useCustomerRegister,
  useCustomerMe,
} from "../use-customer-auth";

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
};

describe("useCustomerLogin", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("calls POST /api/public/auth/customer/login", async () => {
    const mockProfile = {
      id: "abc",
      name: "Test User",
      email: "test@example.com",
    };
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockProfile,
    } as Response);

    const { result } = renderHook(() => useCustomerLogin(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.mutate({
        email: "test@example.com",
        password: "Test1234!",
      });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith(
      "/api/public/auth/customer/login",
      expect.objectContaining({ method: "POST" }),
    );
  });
});

describe("useCustomerRegister", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("calls POST /api/public/auth/customer/register", async () => {
    const mockProfile = {
      id: "xyz",
      name: "New User",
      email: "new@example.com",
    };
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockProfile,
    } as Response);

    const { result } = renderHook(() => useCustomerRegister(), {
      wrapper: createWrapper(),
    });

    act(() => {
      result.current.mutate({
        name: "New User",
        email: "new@example.com",
        password: "Test1234!",
      });
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith(
      "/api/public/auth/customer/register",
      expect.objectContaining({ method: "POST" }),
    );
  });
});

describe("useCustomerMe", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("calls GET /api/public/auth/customer/me", async () => {
    const mockProfile = {
      id: "abc",
      name: "Test User",
      email: "test@example.com",
    };
    global.fetch = vi.fn().mockResolvedValueOnce({
      ok: true,
      json: async () => mockProfile,
    } as Response);

    const { result } = renderHook(() => useCustomerMe(), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(fetch).toHaveBeenCalledWith("/api/public/auth/customer/me");
  });
});

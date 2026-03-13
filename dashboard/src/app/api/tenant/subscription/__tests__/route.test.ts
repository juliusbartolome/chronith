import { describe, it, expect, vi, beforeEach } from "vitest";
import { NextRequest } from "next/server";

vi.mock("@/lib/proxy", () => ({
  proxyToApi: vi.fn(),
}));

import { proxyToApi } from "@/lib/proxy";
import { GET, POST, PUT, DELETE } from "../route";

describe("/api/tenant/subscription", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("GET proxies /v1/tenant/subscription", async () => {
    const mockResponse = new Response(JSON.stringify({}), { status: 200 });
    vi.mocked(proxyToApi).mockResolvedValue(mockResponse as never);

    const req = new NextRequest("http://localhost/api/tenant/subscription");
    await GET(req);

    expect(proxyToApi).toHaveBeenCalledWith(req, "/v1/tenant/subscription");
  });

  it("POST proxies /v1/tenant/subscription with body", async () => {
    const mockResponse = new Response(JSON.stringify({}), { status: 201 });
    vi.mocked(proxyToApi).mockResolvedValue(mockResponse as never);

    const body = JSON.stringify({ planId: "plan-id" });
    const req = new NextRequest("http://localhost/api/tenant/subscription", {
      method: "POST",
      body,
    });
    await POST(req);

    expect(proxyToApi).toHaveBeenCalledWith(
      req,
      "/v1/tenant/subscription",
      expect.objectContaining({ method: "POST" }),
    );
  });

  it("PUT proxies /v1/tenant/subscription/plan with body", async () => {
    const mockResponse = new Response(JSON.stringify({}), { status: 200 });
    vi.mocked(proxyToApi).mockResolvedValue(mockResponse as never);

    const body = JSON.stringify({ newPlanId: "new-plan-id" });
    const req = new NextRequest("http://localhost/api/tenant/subscription", {
      method: "PUT",
      body,
    });
    await PUT(req);

    expect(proxyToApi).toHaveBeenCalledWith(
      req,
      "/v1/tenant/subscription/plan",
      expect.objectContaining({ method: "PUT" }),
    );
  });

  it("DELETE proxies /v1/tenant/subscription with body", async () => {
    const mockResponse = new Response(null, { status: 204 });
    vi.mocked(proxyToApi).mockResolvedValue(mockResponse as never);

    const body = JSON.stringify({ reason: "No longer needed" });
    const req = new NextRequest("http://localhost/api/tenant/subscription", {
      method: "DELETE",
      body,
    });
    await DELETE(req);

    expect(proxyToApi).toHaveBeenCalledWith(
      req,
      "/v1/tenant/subscription",
      expect.objectContaining({ method: "DELETE" }),
    );
  });
});

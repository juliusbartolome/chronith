import { describe, it, expect, vi, beforeEach } from "vitest";
import { NextRequest } from "next/server";

vi.mock("@/lib/proxy", () => ({
  proxyToApi: vi.fn(),
}));

import { proxyToApi } from "@/lib/proxy";
import { POST } from "../route";

describe("POST /api/signup", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("proxies POST /v1/signup unauthenticated with body", async () => {
    const mockResponse = new Response(JSON.stringify({ tenantId: "abc" }), {
      status: 201,
    });
    vi.mocked(proxyToApi).mockResolvedValue(mockResponse as never);

    const body = JSON.stringify({
      tenantName: "Test",
      tenantSlug: "test",
      email: "test@example.com",
      password: "Test1234!",
      timeZoneId: "Asia/Manila",
    });
    const req = new NextRequest("http://localhost/api/signup", {
      method: "POST",
      body,
    });
    await POST(req);

    expect(proxyToApi).toHaveBeenCalledWith(
      req,
      "/v1/signup",
      expect.objectContaining({ unauthenticated: true }),
    );
  });
});

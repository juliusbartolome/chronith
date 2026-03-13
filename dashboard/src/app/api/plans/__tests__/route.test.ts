import { describe, it, expect, vi, beforeEach } from "vitest";
import { NextRequest } from "next/server";

vi.mock("@/lib/proxy", () => ({
  proxyToApi: vi.fn(),
}));

import { proxyToApi } from "@/lib/proxy";
import { GET } from "../route";

describe("GET /api/plans", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("proxies GET /v1/plans unauthenticated", async () => {
    const mockResponse = new Response(JSON.stringify([]), { status: 200 });
    vi.mocked(proxyToApi).mockResolvedValue(mockResponse as never);

    const req = new NextRequest("http://localhost/api/plans");
    await GET(req);

    expect(proxyToApi).toHaveBeenCalledWith(
      req,
      "/v1/plans",
      expect.objectContaining({ unauthenticated: true }),
    );
  });
});

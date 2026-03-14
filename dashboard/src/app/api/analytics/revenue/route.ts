import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(req: NextRequest) {
  const params = req.nextUrl.searchParams.toString();
  return proxyToApi(req, `/v1/analytics/revenue${params ? `?${params}` : ""}`);
}

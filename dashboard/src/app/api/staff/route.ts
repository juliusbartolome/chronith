import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams.toString();
  return proxyToApi(request, `/v1/staff${params ? `?${params}` : ""}`);
}

export async function POST(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/staff", { method: "POST", body });
}

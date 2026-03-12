import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(request: NextRequest) {
  return proxyToApi(request, "/v1/tenant");
}

export async function PUT(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/tenant", { method: "PUT", body });
}

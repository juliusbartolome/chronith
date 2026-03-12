import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(request: NextRequest) {
  return proxyToApi(request, "/v1/api-keys");
}

export async function POST(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/api-keys", { method: "POST", body });
}

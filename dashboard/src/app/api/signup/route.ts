import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/signup", {
    unauthenticated: true,
    method: "POST",
    body,
  });
}

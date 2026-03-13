import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(request: NextRequest) {
  return proxyToApi(request, "/v1/tenant/subscription");
}

export async function POST(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/tenant/subscription", {
    method: "POST",
    body,
  });
}

export async function PUT(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/tenant/subscription/plan", {
    method: "PUT",
    body,
  });
}

export async function DELETE(request: NextRequest) {
  const body = await request.text();
  return proxyToApi(request, "/v1/tenant/subscription", {
    method: "DELETE",
    body,
  });
}

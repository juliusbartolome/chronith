import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(request, `/v1/tenant/notification-templates/${id}`);
}

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  const body = await request.text();
  return proxyToApi(request, `/v1/tenant/notification-templates/${id}`, { method: "PUT", body });
}

import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ channel: string }> },
) {
  const { channel } = await params;
  const body = await request.text();
  return proxyToApi(request, `/v1/tenant/notifications/${channel}`, { method: "PUT", body });
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ channel: string }> },
) {
  const { channel } = await params;
  return proxyToApi(request, `/v1/tenant/notifications/${channel}`, {
    method: "DELETE",
  });
}

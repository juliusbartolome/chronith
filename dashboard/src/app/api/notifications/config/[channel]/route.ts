import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ channel: string }> },
) {
  const { channel } = await params;
  return proxyToApi(req, `/v1/notifications/config/${channel}`, {
    method: "PUT",
  });
}

export async function DELETE(
  req: NextRequest,
  { params }: { params: Promise<{ channel: string }> },
) {
  const { channel } = await params;
  return proxyToApi(req, `/v1/notifications/config/${channel}`, {
    method: "DELETE",
  });
}

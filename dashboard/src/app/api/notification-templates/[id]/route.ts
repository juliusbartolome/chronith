import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(req, `/v1/notification-templates/${id}`);
}

export async function PUT(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(req, `/v1/notification-templates/${id}`, {
    method: "PUT",
  });
}

import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  const body = await request.text();
  return proxyToApi(request, `/v1/staff/${id}/availability`, {
    method: "PUT",
    body,
  });
}

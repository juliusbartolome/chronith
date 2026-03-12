import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(request, `/v1/api-keys/${id}`, {
    method: "DELETE",
  });
}

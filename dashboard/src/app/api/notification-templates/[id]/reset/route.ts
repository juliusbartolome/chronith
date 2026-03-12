import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(
  req: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(req, `/v1/notification-templates/${id}/reset`, {
    method: "POST",
  });
}

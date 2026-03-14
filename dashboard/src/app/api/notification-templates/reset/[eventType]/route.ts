import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ eventType: string }> },
) {
  const { eventType } = await params;
  return proxyToApi(request, `/v1/tenant/notification-templates/reset/${eventType}`, { method: "POST" });
}

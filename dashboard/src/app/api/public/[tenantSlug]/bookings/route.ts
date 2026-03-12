import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ tenantSlug: string }> },
) {
  const { tenantSlug } = await params;
  const body = await request.text();
  return proxyToApi(request, `/v1/public/${tenantSlug}/bookings`, {
    method: "POST",
    body,
  });
}

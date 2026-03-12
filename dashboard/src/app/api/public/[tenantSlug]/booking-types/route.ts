import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ tenantSlug: string }> },
) {
  const { tenantSlug } = await params;
  return proxyToApi(request, `/v1/public/${tenantSlug}/booking-types`, {
    unauthenticated: true,
  });
}

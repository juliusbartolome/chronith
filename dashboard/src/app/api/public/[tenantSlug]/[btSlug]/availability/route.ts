import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ tenantSlug: string; btSlug: string }> },
) {
  const { tenantSlug, btSlug } = await params;
  const searchParams = request.nextUrl.searchParams.toString();
  const query = searchParams ? `?${searchParams}` : "";
  return proxyToApi(
    request,
    `/v1/public/${tenantSlug}/${btSlug}/availability${query}`,
    { unauthenticated: true },
  );
}

import { NextRequest, NextResponse } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(request: NextRequest) {
  const { searchParams } = request.nextUrl;
  const bookingId = searchParams.get("bookingId");
  const tenantSlug = searchParams.get("tenantSlug");
  const expires = searchParams.get("expires");
  const sig = searchParams.get("sig");

  if (!bookingId || !tenantSlug || !expires || !sig) {
    return NextResponse.json(
      { error: "Missing required parameters" },
      { status: 400 },
    );
  }

  const path = `/v1/public/${tenantSlug}/bookings/${bookingId}/verify?expires=${encodeURIComponent(expires)}&sig=${encodeURIComponent(sig)}`;

  return proxyToApi(request, path, { unauthenticated: true });
}

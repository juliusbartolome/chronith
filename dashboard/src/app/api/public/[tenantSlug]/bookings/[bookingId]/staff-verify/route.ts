import { NextRequest, NextResponse } from "next/server";
import { proxyToApi } from "@/lib/proxy";

const UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ tenantSlug: string; bookingId: string }> },
) {
  const { tenantSlug, bookingId } = await params;

  if (!UUID_RE.test(bookingId)) {
    return NextResponse.json({ error: "Invalid booking ID" }, { status: 400 });
  }

  const { searchParams } = request.nextUrl;
  const expires = searchParams.get("expires");
  const sig = searchParams.get("sig");

  if (!expires || !sig) {
    return NextResponse.json(
      { error: "Missing required parameters" },
      { status: 400 },
    );
  }

  const body = await request.text();

  return proxyToApi(
    request,
    `/v1/public/${encodeURIComponent(tenantSlug)}/bookings/${bookingId}/staff-verify?expires=${encodeURIComponent(expires)}&sig=${encodeURIComponent(sig)}`,
    { method: "POST", body, unauthenticated: true },
  );
}

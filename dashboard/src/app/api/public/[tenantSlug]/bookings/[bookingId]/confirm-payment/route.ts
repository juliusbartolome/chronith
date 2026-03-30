import { NextRequest, NextResponse } from "next/server";

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

  const contentType = request.headers.get("content-type") ?? "";

  const apiUrl = process.env.CHRONITH_API_URL ?? "http://localhost:5001";
  const url = `${apiUrl}/v1/public/${encodeURIComponent(tenantSlug)}/bookings/${bookingId}/confirm-payment?expires=${encodeURIComponent(expires)}&sig=${encodeURIComponent(sig)}`;

  const res = await fetch(url, {
    method: "POST",
    headers: {
      "Content-Type": contentType,
      "X-Correlation-Id": crypto.randomUUID(),
    },
    body: request.body,
    // @ts-expect-error Node.js fetch supports duplex for streaming
    duplex: "half",
  });

  const data = await res.text();
  return new NextResponse(data, {
    status: res.status,
    headers: {
      "Content-Type": res.headers.get("content-type") ?? "application/json",
    },
  });
}

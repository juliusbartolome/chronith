import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string }> },
) {
  const { slug } = await params;
  return proxyToApi(request, `/v1/booking-types/${slug}`);
}

export async function PUT(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string }> },
) {
  const { slug } = await params;
  const body = await request.text();
  return proxyToApi(request, `/v1/booking-types/${slug}`, {
    method: "PUT",
    body,
  });
}

export async function DELETE(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string }> },
) {
  const { slug } = await params;
  return proxyToApi(request, `/v1/booking-types/${slug}`, {
    method: "DELETE",
  });
}

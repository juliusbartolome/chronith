import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(
  request: NextRequest,
  { params }: { params: Promise<{ slug: string; id: string }> },
) {
  const { slug, id } = await params;
  return proxyToApi(request, `/v1/booking-types/${slug}/webhooks/${id}/test`, {
    method: "POST",
  });
}

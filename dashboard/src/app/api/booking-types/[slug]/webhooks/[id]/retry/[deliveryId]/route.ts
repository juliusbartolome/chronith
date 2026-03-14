import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function POST(
  request: NextRequest,
  {
    params,
  }: { params: Promise<{ slug: string; id: string; deliveryId: string }> },
) {
  const { slug, id, deliveryId } = await params;
  return proxyToApi(
    request,
    `/v1/booking-types/${slug}/webhooks/${id}/retry/${deliveryId}`,
    { method: "POST" },
  );
}

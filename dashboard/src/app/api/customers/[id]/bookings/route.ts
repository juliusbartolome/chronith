import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(
  request: NextRequest,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;
  return proxyToApi(request, `/v1/customers/${id}/bookings`);
}

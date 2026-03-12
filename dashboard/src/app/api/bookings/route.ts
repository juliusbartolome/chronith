import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams.toString();
  return proxyToApi(request, `/v1/bookings${params ? `?${params}` : ""}`);
}

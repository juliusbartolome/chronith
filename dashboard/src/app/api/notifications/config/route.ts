import { NextRequest } from "next/server";
import { proxyToApi } from "@/lib/proxy";

export async function GET(req: NextRequest) {
  return proxyToApi(req, "/v1/notifications/config");
}

import { NextRequest, NextResponse } from "next/server";

const API_URL = process.env.CHRONITH_API_URL ?? "http://localhost:5001";

export async function proxyToApi(
  request: NextRequest,
  path: string,
  init?: RequestInit,
): Promise<NextResponse> {
  const token = request.cookies.get("chronith-auth")?.value;

  const res = await fetch(`${API_URL}${path}`, {
    method: init?.method ?? request.method,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      "X-Correlation-Id": crypto.randomUUID(),
    },
    body: init?.body,
    cache: "no-store",
  });

  const body = await res.text();
  return new NextResponse(body, {
    status: res.status,
    headers: {
      "Content-Type": res.headers.get("Content-Type") ?? "application/json",
    },
  });
}

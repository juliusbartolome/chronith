import { NextRequest, NextResponse } from "next/server";

const API_URL = process.env.CHRONITH_API_URL ?? "http://localhost:5001";

export async function GET(request: NextRequest) {
  const token = request.cookies.get("chronith-auth")?.value;
  const params = request.nextUrl.searchParams.toString();

  const res = await fetch(
    `${API_URL}/v1/audit/export${params ? `?${params}` : ""}`,
    {
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        "X-Correlation-Id": crypto.randomUUID(),
      },
      cache: "no-store",
    },
  );

  const contentType =
    res.headers.get("Content-Type") ?? "application/octet-stream";
  const contentDisposition = res.headers.get("Content-Disposition") ?? "";
  const body = await res.arrayBuffer();

  return new NextResponse(body, {
    status: res.status,
    headers: {
      "Content-Type": contentType,
      "Content-Disposition": contentDisposition,
    },
  });
}

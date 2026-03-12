import { NextRequest, NextResponse } from "next/server";

const API_URL = process.env.CHRONITH_API_URL ?? "http://localhost:5001";

export async function POST(request: NextRequest) {
  const refreshToken = request.cookies.get("chronith-refresh")?.value;
  if (!refreshToken) {
    return NextResponse.json({ error: "No refresh token" }, { status: 401 });
  }

  const res = await fetch(`${API_URL}/v1/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });

  if (!res.ok) {
    const response = NextResponse.json(
      { error: "Refresh failed" },
      { status: 401 },
    );
    response.cookies.delete("chronith-auth");
    response.cookies.delete("chronith-refresh");
    return response;
  }

  const data = await res.json();
  const response = NextResponse.json({ ok: true });
  response.cookies.set("chronith-auth", data.accessToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict",
    maxAge: 60 * 60,
    path: "/",
  });
  return response;
}

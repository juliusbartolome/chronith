import { NextRequest, NextResponse } from "next/server";

const API_URL = process.env.CHRONITH_API_URL ?? "http://localhost:5001";

export async function POST(request: NextRequest) {
  const body = await request.json();

  const res = await fetch(`${API_URL}/v1/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const error = await res.json().catch(() => ({ title: "Login failed" }));
    return NextResponse.json(error, { status: res.status });
  }

  const data = await res.json();
  const { accessToken, refreshToken } = data;

  const response = NextResponse.json({ ok: true });
  response.cookies.set("chronith-auth", accessToken, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "strict",
    maxAge: 60 * 60, // 1 hour
    path: "/",
  });
  if (refreshToken) {
    response.cookies.set("chronith-refresh", refreshToken, {
      httpOnly: true,
      secure: process.env.NODE_ENV === "production",
      sameSite: "strict",
      maxAge: 60 * 60 * 24 * 30, // 30 days
      path: "/api/auth/refresh",
    });
  }

  return response;
}

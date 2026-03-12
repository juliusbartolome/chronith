import { NextRequest, NextResponse } from "next/server";

const API_BASE = process.env.CHRONITH_API_URL ?? "http://localhost:5001";

export async function POST(req: NextRequest) {
  const body = await req.text();

  const apiRes = await fetch(`${API_BASE}/v1/auth/customer/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body,
  });

  if (!apiRes.ok) {
    const err = await apiRes.text();
    return new NextResponse(err, { status: apiRes.status });
  }

  const data = await apiRes.json();
  const { token, ...profile } = data;

  const response = NextResponse.json(profile);
  response.cookies.set("customer_token", token, {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax",
    path: "/",
    maxAge: 60 * 60 * 24 * 7, // 7 days
  });
  return response;
}

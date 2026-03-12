import { NextRequest, NextResponse } from "next/server";

const API_BASE = process.env.API_BASE_URL ?? "http://localhost:5001";

export async function GET(req: NextRequest) {
  const token = req.cookies.get("customer_token")?.value;
  if (!token) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const apiRes = await fetch(`${API_BASE}/v1/auth/customer/me`, {
    headers: { Authorization: `Bearer ${token}` },
  });

  if (!apiRes.ok) {
    const err = await apiRes.text();
    return new NextResponse(err, { status: apiRes.status });
  }

  return NextResponse.json(await apiRes.json());
}

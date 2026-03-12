import { NextRequest, NextResponse } from "next/server";

const API_BASE = process.env.CHRONITH_API_URL ?? "http://localhost:5001";

export async function GET(req: NextRequest) {
  const token = req.cookies.get("customer_token")?.value;
  if (!token) {
    return NextResponse.json({ error: "Not authenticated" }, { status: 401 });
  }

  const { searchParams } = new URL(req.url);
  const query = searchParams.toString();

  const apiRes = await fetch(
    `${API_BASE}/v1/auth/customer/bookings${query ? `?${query}` : ""}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );

  if (!apiRes.ok) {
    return new NextResponse(await apiRes.text(), { status: apiRes.status });
  }

  return NextResponse.json(await apiRes.json());
}

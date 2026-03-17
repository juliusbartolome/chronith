import { NextResponse } from "next/server";

export async function POST() {
  const response = NextResponse.json({ ok: true });
  response.cookies.delete("chronith-auth");
  response.cookies.delete("chronith-refresh");
  return response;
}

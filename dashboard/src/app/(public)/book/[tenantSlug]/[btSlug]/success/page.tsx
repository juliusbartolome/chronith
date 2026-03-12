"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useBookingSession } from "@/lib/booking-session";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

function buildGoogleCalendarUrl(params: {
  title: string;
  date: string;
  startTime: string;
  durationMinutes: number;
  description?: string;
}): string {
  const start = `${params.date.replace(/-/g, "")}T${params.startTime.replace(/:/g, "")}00`;
  const [sh, sm] = params.startTime.split(":").map(Number);
  const totalMinutes = sh * 60 + sm + params.durationMinutes;
  const endH = Math.floor(totalMinutes / 60) % 24;
  const endM = totalMinutes % 60;
  const end = `${params.date.replace(/-/g, "")}T${String(endH).padStart(2, "0")}${String(endM).padStart(2, "0")}00`;
  const url = new URL("https://calendar.google.com/calendar/render");
  url.searchParams.set("action", "TEMPLATE");
  url.searchParams.set("text", params.title);
  url.searchParams.set("dates", `${start}/${end}`);
  if (params.description) url.searchParams.set("details", params.description);
  return url.toString();
}

function generateIcalContent(params: {
  uid: string;
  title: string;
  date: string;
  startTime: string;
  durationMinutes: number;
  description?: string;
}): string {
  const start = `${params.date.replace(/-/g, "")}T${params.startTime.replace(/:/g, "")}00`;
  const [sh, sm] = params.startTime.split(":").map(Number);
  const totalMinutes = sh * 60 + sm + params.durationMinutes;
  const endH = Math.floor(totalMinutes / 60) % 24;
  const endM = totalMinutes % 60;
  const end = `${params.date.replace(/-/g, "")}T${String(endH).padStart(2, "0")}${String(endM).padStart(2, "0")}00`;

  return [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Chronith//Chronith Booking//EN",
    "BEGIN:VEVENT",
    `UID:${params.uid}@chronith`,
    `DTSTAMP:${new Date().toISOString().replace(/[-:]/g, "").split(".")[0]}Z`,
    `DTSTART:${start}`,
    `DTEND:${end}`,
    `SUMMARY:${params.title}`,
    params.description ? `DESCRIPTION:${params.description}` : "",
    "END:VEVENT",
    "END:VCALENDAR",
  ]
    .filter(Boolean)
    .join("\r\n");
}

export default function SuccessPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();

  const session = useBookingSession();
  const resetSession = useBookingSession((s) => s.resetSession);

  const bookingId = session.confirmedBookingId;

  // Guard: no confirmed booking
  useEffect(() => {
    if (!session.confirmedBookingId) {
      router.replace(`/book/${tenantSlug}/${btSlug}`);
    }
  }, [session.confirmedBookingId, router, tenantSlug, btSlug]);

  if (!bookingId) return null;

  const handleBookAnother = () => {
    resetSession();
    router.push(`/book/${tenantSlug}`);
  };

  const handleDownloadIcal = () => {
    if (!session.selectedDate || !session.selectedSlot) return;
    const content = generateIcalContent({
      uid: bookingId ?? "booking",
      title: session.btName,
      date: session.selectedDate,
      startTime: session.selectedSlot,
      durationMinutes: session.durationMinutes,
    });
    const blob = new Blob([content], { type: "text/calendar;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `booking-${bookingId ?? "confirmation"}.ics`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const googleCalUrl =
    session.selectedDate && session.selectedSlot
      ? buildGoogleCalendarUrl({
          title: session.btName,
          date: session.selectedDate,
          startTime: session.selectedSlot,
          durationMinutes: session.durationMinutes,
        })
      : null;

  return (
    <div className="max-w-lg mx-auto px-4 py-16 text-center">
      <div className="mb-6 flex justify-center">
        <div className="h-16 w-16 rounded-full bg-green-100 flex items-center justify-center">
          <svg
            className="h-8 w-8 text-green-600"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
      </div>

      <h1 className="text-2xl font-bold text-zinc-900 mb-2">Booking Confirmed!</h1>
      <p className="text-zinc-500 text-sm mb-8">
        Your appointment has been successfully booked. A confirmation email will be sent to{" "}
        <span className="font-medium">{session.customerInfo?.email}</span>.
      </p>

      <Card className="text-left mb-8">
        <CardContent className="pt-4 space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-zinc-500">Booking ID</span>
            <span className="font-mono text-xs text-zinc-700">{bookingId?.slice(0, 8)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Service</span>
            <span className="font-medium text-zinc-900">{session.btName}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Date</span>
            <span className="font-medium text-zinc-900">{session.selectedDate}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Time</span>
            <span className="font-medium text-zinc-900">{session.selectedSlot}</span>
          </div>
          {session.selectedStaffName && (
            <p>
              <span className="text-zinc-500">Staff:</span>{" "}
              {session.selectedStaffName}
            </p>
          )}
        </CardContent>
      </Card>

      {/* Add to Calendar */}
      <div className="space-y-2 mb-8">
        <p className="text-sm font-medium text-zinc-700">Add to Calendar</p>
        <div className="flex flex-col sm:flex-row gap-2 justify-center">
          {googleCalUrl && (
            <a
              href={googleCalUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center justify-center rounded-md border px-4 py-2 text-sm font-medium hover:bg-zinc-50"
            >
              Google Calendar
            </a>
          )}
          <Button variant="outline" onClick={handleDownloadIcal}>
            Apple / Outlook (.ics)
          </Button>
        </div>
      </div>

      <Button onClick={handleBookAnother} className="w-full">
        Book Another Appointment
      </Button>
    </div>
  );
}

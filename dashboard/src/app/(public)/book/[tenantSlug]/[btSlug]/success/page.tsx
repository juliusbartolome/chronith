"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useBookingSession } from "@/lib/booking-session";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

export default function SuccessPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();

  const session = useBookingSession();
  const resetSession = useBookingSession((s) => s.resetSession);

  // Guard: no confirmed booking
  useEffect(() => {
    if (!session.confirmedBookingId) {
      router.replace(`/book/${tenantSlug}/${btSlug}`);
    }
  }, [session.confirmedBookingId, router, tenantSlug, btSlug]);

  const handleBookAnother = () => {
    resetSession();
    router.push(`/book/${tenantSlug}`);
  };

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
            <span className="font-mono text-xs text-zinc-700">{session.confirmedBookingId}</span>
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
        </CardContent>
      </Card>

      <Button onClick={handleBookAnother} className="w-full">
        Book Another Appointment
      </Button>
    </div>
  );
}

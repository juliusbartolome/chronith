"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { useBookingSession } from "@/lib/booking-session";
import { useCreatePublicBooking } from "@/hooks/use-public-booking";
import { formatPrice } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";

export default function ConfirmPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();

  const session = useBookingSession();
  const setConfirmedBookingId = useBookingSession((s) => s.setConfirmedBookingId);

  const { mutateAsync: createBooking, isPending } = useCreatePublicBooking(tenantSlug);

  // Guard: incomplete session
  useEffect(() => {
    if (!session.selectedDate || !session.customerInfo) {
      router.replace(`/book/${tenantSlug}/${btSlug}`);
    }
  }, [session.selectedDate, session.customerInfo, router, tenantSlug, btSlug]);

  if (!session.selectedDate || !session.customerInfo) return null;

  const handleConfirm = async () => {
    if (!session.customerInfo) return;

    try {
      const result = await createBooking({
        bookingTypeSlug: session.btSlug,
        date: session.selectedDate!,
        startTime: session.selectedSlot!,
        staffId: session.selectedStaffId ?? undefined,
        customerName: session.customerInfo.name,
        customerEmail: session.customerInfo.email,
        customerPhone: session.customerInfo.phone,
        customFields: Object.keys(session.customFields).length > 0
          ? session.customFields
          : undefined,
      });

      setConfirmedBookingId(result.id);
      router.push(`/book/${tenantSlug}/${btSlug}/success`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to create booking. Please try again.");
    }
  };

  return (
    <div className="max-w-lg mx-auto px-4 py-10">
      <p className="text-xs text-zinc-500 mb-6">Step 4 of 5 — Review Booking</p>
      <h1 className="text-xl font-bold text-zinc-900 mb-6">Review Booking</h1>

      <Card>
        <CardContent className="pt-4 space-y-3">
          <div className="flex justify-between text-sm">
            <span className="text-zinc-500">Service</span>
            <span className="font-medium text-zinc-900">{session.btName}</span>
          </div>
          <Separator />
          <div className="flex justify-between text-sm">
            <span className="text-zinc-500">Date</span>
            <span className="font-medium text-zinc-900">{session.selectedDate}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-zinc-500">Time</span>
            <span className="font-medium text-zinc-900">{session.selectedSlot}</span>
          </div>
          {session.selectedStaffName && (
            <div className="flex justify-between text-sm">
              <span className="text-zinc-500">Staff</span>
              <span className="font-medium text-zinc-900">{session.selectedStaffName}</span>
            </div>
          )}
          <Separator />
          <div className="flex justify-between text-sm">
            <span className="text-zinc-500">Name</span>
            <span className="font-medium text-zinc-900">{session.customerInfo?.name}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-zinc-500">Email</span>
            <span className="font-medium text-zinc-900">{session.customerInfo?.email}</span>
          </div>
          {session.customerInfo?.phone && (
            <div className="flex justify-between text-sm">
              <span className="text-zinc-500">Phone</span>
              <span className="font-medium text-zinc-900">{session.customerInfo.phone}</span>
            </div>
          )}
          <Separator />
          <div className="flex justify-between text-sm font-semibold">
            <span className="text-zinc-700">Total</span>
            <span className="text-zinc-900">{formatPrice(session.priceCentavos)}</span>
          </div>
        </CardContent>
      </Card>

      <div className="mt-6 flex gap-3">
        <Button
          variant="outline"
          className="flex-1"
          onClick={() => router.back()}
          disabled={isPending}
        >
          Back
        </Button>
        <Button
          className="flex-1"
          onClick={handleConfirm}
          disabled={isPending}
        >
          {isPending ? "Confirming…" : "Confirm Booking"}
        </Button>
      </div>
    </div>
  );
}

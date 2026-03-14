"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { usePublicBookingTypes, usePublicAvailability } from "@/hooks/use-public-booking";
import { useBookingSession } from "@/lib/booking-session";
import { Calendar } from "@/components/ui/calendar";
import { Skeleton } from "@/components/ui/skeleton";

function formatDateLocal(date: Date): string {
  // Returns YYYY-MM-DD in local time (not UTC)
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export default function AvailabilityPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();
  const setBookingType = useBookingSession((s) => s.setBookingType);
  const setDateAndSlot = useBookingSession((s) => s.setDateAndSlot);

  const [selectedDate, setSelectedDate] = useState<Date | undefined>();
  const dateStr = selectedDate ? formatDateLocal(selectedDate) : "";

  const { data: bookingTypes } = usePublicBookingTypes(tenantSlug);
  const bookingType = bookingTypes?.find((bt) => bt.slug === btSlug);

  const {
    data: availability,
    isLoading: isLoadingSlots,
  } = usePublicAvailability(tenantSlug, btSlug, dateStr);

  const handleSlotSelect = (slot: string) => {
    if (!bookingType) return;

    setBookingType({
      tenantSlug,
      btSlug,
      btName: bookingType.name,
      durationMinutes: bookingType.durationMinutes,
      priceCentavos: bookingType.priceCentavos,
      requiresStaffAssignment: bookingType.requiresStaffAssignment,
      customFieldSchema: bookingType.customFieldSchema ?? [],
    });
    setDateAndSlot(dateStr, slot);

    if (bookingType.requiresStaffAssignment) {
      router.push(`/book/${tenantSlug}/${btSlug}/staff`);
    } else {
      router.push(`/book/${tenantSlug}/${btSlug}/details`);
    }
  };

  return (
    <div className="max-w-3xl mx-auto px-4 py-10">
      {/* Progress */}
      <p className="text-xs text-zinc-500 mb-6">Step 1 of 5 — Choose Date & Time</p>

      <h1 className="text-xl font-bold text-zinc-900 mb-6">
        {bookingType?.name ?? btSlug}
      </h1>

      <div className="grid gap-8 md:grid-cols-2">
        {/* Calendar */}
        <div>
          <p className="text-sm font-medium text-zinc-700 mb-2">Select a date</p>
          <Calendar
            mode="single"
            selected={selectedDate}
            onSelect={setSelectedDate}
            disabled={(date) => {
              const today = new Date();
              today.setHours(0, 0, 0, 0);
              return date < today;
            }}
            className="rounded-md border"
          />
        </div>

        {/* Time slots */}
        <div>
          <p className="text-sm font-medium text-zinc-700 mb-2">
            {selectedDate
              ? `Available slots on ${selectedDate.toLocaleDateString()}`
              : "Select a date to see slots"}
          </p>

          {isLoadingSlots && (
            <div className="flex flex-wrap gap-2">
              {[1, 2, 3, 4].map((i) => (
                <Skeleton key={i} className="h-8 w-20" />
              ))}
            </div>
          )}

          {!isLoadingSlots && availability?.slots && availability.slots.length === 0 && (
            <p className="text-sm text-zinc-500">No slots available for this date.</p>
          )}

          {!isLoadingSlots && availability?.slots && availability.slots.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {availability.slots.map((slot) => (
                <button
                  key={slot}
                  type="button"
                  onClick={() => handleSlotSelect(slot)}
                  className="rounded-md border border-zinc-300 px-3 py-1.5 text-sm font-medium hover:border-[var(--color-primary)] hover:text-[var(--color-primary)] transition-colors"
                >
                  {slot}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

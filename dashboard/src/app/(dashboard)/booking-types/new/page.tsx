"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  BookingTypeForm,
  BookingTypeFormValues,
} from "@/components/booking-types/booking-type-form";
import { useCreateBookingType } from "@/hooks/use-booking-types";
import { Button } from "@/components/ui/button";

export default function NewBookingTypePage() {
  const router = useRouter();
  const createType = useCreateBookingType();
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (values: BookingTypeFormValues) => {
    setError(null);
    try {
      await createType.mutateAsync(values);
      router.push("/booking-types");
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to create booking type");
    }
  };

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          ←
        </Button>
        <h1 className="text-2xl font-semibold">New Booking Type</h1>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md bg-red-50 p-3 text-sm text-red-700"
        >
          {error}
        </div>
      )}

      <BookingTypeForm
        onSubmit={handleSubmit}
        isSubmitting={createType.isPending}
      />
    </div>
  );
}

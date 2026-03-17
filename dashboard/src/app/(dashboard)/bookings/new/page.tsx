"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const STEPS = [
  "Select Type",
  "Pick Date/Time",
  "Select Staff",
  "Customer Info",
  "Custom Fields",
  "Review",
];

function useBookingTypes() {
  return useQuery({
    queryKey: ["booking-types", {}],
    queryFn: async () => {
      const res = await fetch("/api/booking-types");
      if (!res.ok) throw new Error("Failed to fetch booking types");
      return res.json();
    },
  });
}

interface BookingTypeOption {
  id: string;
  name: string;
  slug: string;
  kind: string;
}

function BookingTypeSelector({
  onSelect,
}: {
  onSelect: (slug: string) => void;
}) {
  const { data, isLoading } = useBookingTypes();

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;

  return (
    <div className="grid grid-cols-2 gap-3">
      {data?.items?.map((bt: BookingTypeOption) => (
        <button
          key={bt.id}
          className="rounded-lg border p-4 text-left hover:border-zinc-400 hover:bg-zinc-50"
          onClick={() => onSelect(bt.slug)}
        >
          <p className="font-medium">{bt.name}</p>
          <p className="text-xs text-zinc-500">{bt.kind}</p>
        </button>
      ))}
    </div>
  );
}

export default function CreateBookingPage() {
  const router = useRouter();
  const [step, setStep] = useState(0);
  const [formData, setFormData] = useState<Record<string, unknown>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const next = () => setStep((s) => Math.min(s + 1, STEPS.length - 1));
  const prev = () => setStep((s) => Math.max(s - 1, 0));

  const handleSubmit = async () => {
    setIsSubmitting(true);
    setError(null);
    try {
      const res = await fetch(
        `/api/booking-types/${formData.bookingTypeSlug}/bookings`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            start: formData.start,
            customerEmail: formData.customerEmail,
            customerId: formData.customerId,
            staffMemberId: formData.staffMemberId,
            customFields: formData.customFields ?? {},
          }),
        },
      );
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body.title ?? "Failed to create booking");
      }
      router.push("/bookings");
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to create booking");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          ←
        </Button>
        <h1 className="text-2xl font-semibold">New Booking</h1>
      </div>

      {/* Step indicator */}
      <div className="flex gap-1">
        {STEPS.map((s, i) => (
          <div
            key={s}
            className={`h-1.5 flex-1 rounded-full ${i <= step ? "bg-zinc-900" : "bg-zinc-200"}`}
          />
        ))}
      </div>

      <p className="text-sm font-medium text-zinc-500">
        Step {step + 1} of {STEPS.length}: {STEPS[step]}
      </p>

      {error && (
        <div
          role="alert"
          className="rounded-md bg-red-50 p-3 text-sm text-red-700"
        >
          {error}
        </div>
      )}

      <Card>
        <CardHeader>
          <CardTitle>{STEPS[step]}</CardTitle>
        </CardHeader>
        <CardContent>
          {step === 0 && (
            <BookingTypeSelector
              onSelect={(slug) => {
                setFormData((d) => ({ ...d, bookingTypeSlug: slug }));
                next();
              }}
            />
          )}
          {step === 1 && (
            <div className="space-y-3">
              <Label htmlFor="start">Start Date/Time</Label>
              <Input
                id="start"
                type="datetime-local"
                onChange={(e) =>
                  setFormData((d) => ({ ...d, start: e.target.value }))
                }
              />
            </div>
          )}
          {step === 2 && (
            <p className="text-sm text-zinc-500">
              Staff selection will be available based on the booking type
              configuration.
            </p>
          )}
          {step === 3 && (
            <div className="space-y-3">
              <div>
                <Label htmlFor="customerEmail">Customer Email</Label>
                <Input
                  id="customerEmail"
                  type="email"
                  onChange={(e) =>
                    setFormData((d) => ({
                      ...d,
                      customerEmail: e.target.value,
                    }))
                  }
                />
              </div>
              <div>
                <Label htmlFor="customerId">Customer ID</Label>
                <Input
                  id="customerId"
                  onChange={(e) =>
                    setFormData((d) => ({ ...d, customerId: e.target.value }))
                  }
                />
              </div>
            </div>
          )}
          {step === 4 && (
            <p className="text-sm text-zinc-500">
              Custom fields will be rendered based on the booking type schema.
            </p>
          )}
          {step === 5 && (
            <div className="space-y-2 text-sm">
              <p>
                <span className="font-medium">Type:</span>{" "}
                {String(formData.bookingTypeSlug ?? "")}
              </p>
              <p>
                <span className="font-medium">Start:</span>{" "}
                {String(formData.start ?? "")}
              </p>
              <p>
                <span className="font-medium">Customer:</span>{" "}
                {String(formData.customerEmail ?? "")}
              </p>
            </div>
          )}
        </CardContent>
      </Card>

      <div className="flex justify-between">
        <Button variant="outline" onClick={prev} disabled={step === 0}>
          Back
        </Button>
        {step < STEPS.length - 1 ? (
          <Button onClick={next}>Next</Button>
        ) : (
          <Button onClick={handleSubmit} disabled={isSubmitting}>
            {isSubmitting ? "Creating…" : "Create Booking"}
          </Button>
        )}
      </div>
    </div>
  );
}

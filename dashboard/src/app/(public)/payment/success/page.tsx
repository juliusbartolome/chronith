"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { usePaymentResult } from "@/hooks/use-payment-result";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

function formatAmount(centavos: number): string {
  return (centavos / 100).toLocaleString("en-PH", {
    style: "currency",
    currency: "PHP",
  });
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-PH", {
    weekday: "long",
    year: "numeric",
    month: "long",
    day: "numeric",
  });
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("en-PH", {
    hour: "numeric",
    minute: "2-digit",
  });
}

function Spinner({ className = "h-8 w-8" }: { className?: string }) {
  return (
    <svg
      className={`animate-spin text-zinc-400 ${className}`}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
      />
    </svg>
  );
}

function PaymentSuccessContent() {
  const searchParams = useSearchParams();
  const tenantSlug = searchParams.get("tenantSlug");

  const state = usePaymentResult(
    {
      bookingId: searchParams.get("bookingId"),
      tenantSlug,
      expires: searchParams.get("expires"),
      sig: searchParams.get("sig"),
    },
    { poll: true },
  );

  // 1. Loading
  if (state.status === "loading") {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <Spinner />
        </div>
        <p className="text-zinc-500 text-sm">Verifying payment...</p>
      </div>
    );
  }

  // 5. Invalid HMAC
  if (state.status === "invalid") {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <div className="h-16 w-16 rounded-full bg-amber-100 flex items-center justify-center">
            <svg
              className="h-8 w-8 text-amber-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v2m0 4h.01M12 2l9.5 16.5H2.5L12 2z"
              />
            </svg>
          </div>
        </div>
        <h1 className="text-2xl font-bold text-zinc-900 mb-2">
          This link has expired or is invalid
        </h1>
      </div>
    );
  }

  // 6. Error
  if (state.status === "error") {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <div className="h-16 w-16 rounded-full bg-red-100 flex items-center justify-center">
            <svg
              className="h-8 w-8 text-red-600"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth={2}
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M6 18L18 6M6 6l12 12"
              />
            </svg>
          </div>
        </div>
        <h1 className="text-2xl font-bold text-zinc-900 mb-2">
          Something went wrong
        </h1>
        <p className="text-zinc-500 text-sm">Please try again later.</p>
      </div>
    );
  }

  // state.status === "verified" from here
  const { booking } = state;

  // 3. Verified + PendingPayment (polling active, webhook hasn't arrived yet)
  if (booking.status === "PendingPayment") {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <Spinner />
        </div>
        <p className="text-zinc-900 font-medium mb-1">
          Confirming your booking...
        </p>
        <p className="text-zinc-500 text-sm">This may take a few moments.</p>
      </div>
    );
  }

  // 2. Verified + Confirmed
  if (
    booking.status === "Confirmed" ||
    booking.status === "PendingVerification"
  ) {
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
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M5 13l4 4L19 7"
              />
            </svg>
          </div>
        </div>

        <h1 className="text-2xl font-bold text-zinc-900 mb-2">
          Payment Successful!
        </h1>
        <p className="text-zinc-500 text-sm mb-8">
          Your booking has been confirmed.
        </p>

        <Card className="text-left mb-8">
          <CardContent className="pt-4 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-zinc-500">Reference ID</span>
              <span className="font-mono text-xs text-zinc-700">
                {booking.referenceId.slice(0, 8)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-500">Date</span>
              <span className="font-medium text-zinc-900">
                {formatDate(booking.start)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-500">Time</span>
              <span className="font-medium text-zinc-900">
                {formatTime(booking.start)} &ndash; {formatTime(booking.end)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-500">Amount</span>
              <span className="font-medium text-zinc-900">
                {formatAmount(booking.amountInCentavos)}
              </span>
            </div>
          </CardContent>
        </Card>

        {tenantSlug && (
          <Button asChild className="w-full">
            <Link href={`/book/${tenantSlug}`}>Book Another Appointment</Link>
          </Button>
        )}
      </div>
    );
  }

  // 4. Verified + PaymentFailed or Cancelled
  return (
    <div className="max-w-lg mx-auto px-4 py-16 text-center">
      <div className="mb-6 flex justify-center">
        <div className="h-16 w-16 rounded-full bg-red-100 flex items-center justify-center">
          <svg
            className="h-8 w-8 text-red-600"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
            strokeWidth={2}
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M6 18L18 6M6 6l12 12"
            />
          </svg>
        </div>
      </div>

      <h1 className="text-2xl font-bold text-zinc-900 mb-2">
        Something went wrong
      </h1>
      <p className="text-zinc-500 text-sm mb-8">
        Your payment could not be processed.
      </p>

      {tenantSlug && (
        <Button asChild className="w-full">
          <Link href={`/book/${tenantSlug}`}>Book Again</Link>
        </Button>
      )}
    </div>
  );
}

export default function PaymentSuccessPage() {
  return (
    <Suspense
      fallback={
        <div className="max-w-lg mx-auto px-4 py-16 text-center">
          <div className="mb-6 flex justify-center">
            <Spinner />
          </div>
          <p className="text-zinc-500 text-sm">Verifying payment...</p>
        </div>
      }
    >
      <PaymentSuccessContent />
    </Suspense>
  );
}

"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { usePaymentResult } from "@/hooks/use-payment-result";
import { Button } from "@/components/ui/button";

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

function PaymentFailedContent() {
  const searchParams = useSearchParams();
  const tenantSlug = searchParams.get("tenantSlug");

  const state = usePaymentResult({
    bookingId: searchParams.get("bookingId"),
    tenantSlug,
    expires: searchParams.get("expires"),
    sig: searchParams.get("sig"),
  });

  // 1. Loading
  if (state.status === "loading") {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <Spinner />
        </div>
        <p className="text-zinc-500 text-sm">Loading...</p>
      </div>
    );
  }

  // 3. Invalid HMAC
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

  // 4. Error
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

  // 2. Verified (any booking status — this is the failed page)
  const { booking } = state;

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
        Payment Not Completed
      </h1>
      <p className="text-zinc-500 text-sm mb-8">
        Your payment was cancelled or could not be processed.
      </p>

      {booking.referenceId && (
        <p className="text-zinc-400 text-xs font-mono mb-8">
          Reference: {booking.referenceId.slice(0, 8)}
        </p>
      )}

      {tenantSlug && (
        <Button asChild className="w-full">
          <Link href={`/book/${tenantSlug}`}>Book Again</Link>
        </Button>
      )}
    </div>
  );
}

export default function PaymentFailedPage() {
  return (
    <Suspense
      fallback={
        <div className="max-w-lg mx-auto px-4 py-16 text-center">
          <div className="mb-6 flex justify-center">
            <Spinner />
          </div>
          <p className="text-zinc-500 text-sm">Loading...</p>
        </div>
      }
    >
      <PaymentFailedContent />
    </Suspense>
  );
}

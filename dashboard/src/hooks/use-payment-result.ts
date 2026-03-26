import { useState, useEffect, useRef, useCallback } from "react";

export interface PaymentResultParams {
  bookingId: string | null;
  tenantSlug: string | null;
  expires: string | null;
  sig: string | null;
}

export interface BookingStatus {
  id: string;
  referenceId: string;
  status:
    | "PendingPayment"
    | "PendingVerification"
    | "Confirmed"
    | "Cancelled"
    | "PaymentFailed";
  start: string;
  end: string;
  amountInCentavos: number;
  currency: string;
  paymentReference: string | null;
  checkoutUrl: string | null;
}

export type VerificationState =
  | { status: "loading" }
  | { status: "verified"; booking: BookingStatus }
  | { status: "invalid" }
  | { status: "error"; message: string };

const POLL_INTERVAL_MS = 3_000;
const MAX_POLL_ATTEMPTS = 10;

export function usePaymentResult(
  params: PaymentResultParams,
  options?: { poll?: boolean },
): VerificationState {
  const { bookingId, tenantSlug, expires, sig } = params;
  const poll = options?.poll ?? false;

  const allParamsPresent =
    bookingId !== null &&
    tenantSlug !== null &&
    expires !== null &&
    sig !== null;

  const [state, setState] = useState<VerificationState>(() =>
    allParamsPresent ? { status: "loading" } : { status: "invalid" },
  );

  const pollCountRef = useRef(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const clearPolling = useCallback(() => {
    if (timerRef.current !== null) {
      clearInterval(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const fetchStatus = useCallback(
    async (signal: AbortSignal): Promise<VerificationState> => {
      const qs = new URLSearchParams({
        bookingId: bookingId!,
        tenantSlug: tenantSlug!,
        expires: expires!,
        sig: sig!,
      });
      const res = await fetch(`/api/public/payment/verify?${qs}`, { signal });

      if (res.status === 401) {
        return { status: "invalid" };
      }

      if (!res.ok) {
        const text = await res.text().catch(() => "");
        return {
          status: "error",
          message: text || `Server error (${res.status})`,
        };
      }

      const booking: BookingStatus = await res.json();
      return { status: "verified", booking };
    },
    [bookingId, tenantSlug, expires, sig],
  );

  useEffect(() => {
    if (!allParamsPresent) {
      return;
    }

    const controller = new AbortController();
    abortRef.current = controller;
    pollCountRef.current = 0;

    let mounted = true;

    async function initialFetch() {
      setState({ status: "loading" });

      try {
        const result = await fetchStatus(controller.signal);
        if (!mounted) return;

        setState(result);

        const shouldPoll =
          poll &&
          result.status === "verified" &&
          result.booking.status === "PendingPayment";

        if (shouldPoll) {
          startPolling(controller.signal);
        }
      } catch (err: unknown) {
        if (!mounted) return;
        if (err instanceof DOMException && err.name === "AbortError") return;
        setState({
          status: "error",
          message: err instanceof Error ? err.message : "Unknown error",
        });
      }
    }

    function startPolling(signal: AbortSignal) {
      timerRef.current = setInterval(async () => {
        pollCountRef.current += 1;

        if (pollCountRef.current >= MAX_POLL_ATTEMPTS) {
          clearPolling();
          return;
        }

        try {
          const result = await fetchStatus(signal);
          if (!mounted) return;

          setState(result);

          const stillPending =
            result.status === "verified" &&
            result.booking.status === "PendingPayment";

          if (!stillPending) {
            clearPolling();
          }
        } catch (err: unknown) {
          if (!mounted) return;
          if (err instanceof DOMException && err.name === "AbortError") return;
          clearPolling();
          setState({
            status: "error",
            message: err instanceof Error ? err.message : "Unknown error",
          });
        }
      }, POLL_INTERVAL_MS);
    }

    initialFetch();

    return () => {
      mounted = false;
      clearPolling();
      controller.abort();
    };
  }, [allParamsPresent, poll, fetchStatus, clearPolling]);

  return state;
}

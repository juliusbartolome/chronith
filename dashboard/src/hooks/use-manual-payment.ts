import { useMutation, useQuery } from "@tanstack/react-query";

export type PublicBookingStatusDto = {
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
  paymentMode: string | null;
  manualPaymentOptions: {
    qrCodeUrl: string | null;
    publicNote: string | null;
    label: string;
  } | null;
  proofOfPaymentUrl: string | null;
  proofOfPaymentFileName: string | null;
  paymentNote: string | null;
};

export function useBookingStatus(
  tenantSlug: string,
  bookingId: string,
  enabled = true,
) {
  return useQuery<PublicBookingStatusDto>({
    queryKey: ["public-booking-status", tenantSlug, bookingId],
    queryFn: async () => {
      const res = await fetch(
        `/api/public/${tenantSlug}/bookings/${bookingId}`,
      );
      if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(
          text || `Failed to fetch booking status (${res.status})`,
        );
      }
      return res.json();
    },
    enabled: !!tenantSlug && !!bookingId && enabled,
  });
}

export type ConfirmManualPaymentParams = {
  tenantSlug: string;
  bookingId: string;
  expires: string;
  sig: string;
  proofFile?: File;
  paymentNote?: string;
};

export function useConfirmManualPayment() {
  return useMutation({
    mutationFn: async (params: ConfirmManualPaymentParams) => {
      const formData = new FormData();

      if (params.proofFile) {
        formData.append("ProofFile", params.proofFile);
      }
      if (params.paymentNote) {
        formData.append("PaymentNote", params.paymentNote);
      }

      const qs = new URLSearchParams({
        expires: params.expires,
        sig: params.sig,
      });

      const res = await fetch(
        `/api/public/${params.tenantSlug}/bookings/${params.bookingId}/confirm-payment?${qs}`,
        {
          method: "POST",
          body: formData,
        },
      );

      if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(text || `Payment confirmation failed (${res.status})`);
      }

      return res.json() as Promise<PublicBookingStatusDto>;
    },
  });
}

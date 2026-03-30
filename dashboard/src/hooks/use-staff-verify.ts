import { useMutation } from "@tanstack/react-query";
import type { PublicBookingStatusDto } from "@/hooks/use-manual-payment";

export type StaffVerifyParams = {
  tenantSlug: string;
  bookingId: string;
  expires: string;
  sig: string;
  action: "approve" | "reject";
  note?: string;
};

export function useStaffVerify() {
  return useMutation({
    mutationFn: async (params: StaffVerifyParams) => {
      const qs = new URLSearchParams({
        expires: params.expires,
        sig: params.sig,
      });

      const res = await fetch(
        `/api/public/${params.tenantSlug}/bookings/${params.bookingId}/staff-verify?${qs}`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            Action: params.action,
            ...(params.note ? { Note: params.note } : {}),
          }),
        },
      );

      if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(text || `Staff verification failed (${res.status})`);
      }

      return res.json() as Promise<PublicBookingStatusDto>;
    },
  });
}

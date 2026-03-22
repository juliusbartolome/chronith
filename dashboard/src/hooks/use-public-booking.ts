import { useMutation, useQuery } from "@tanstack/react-query";

export type PublicBookingTypeDto = {
  id: string;
  slug: string;
  name: string;
  description: string | null;
  kind: "TimeSlot" | "Calendar";
  durationMinutes: number;
  priceCentavos: number;
  requiresStaffAssignment: boolean;
  customFieldSchema?: PublicCustomField[];
};

export type PublicCustomField = {
  key: string;
  label: string;
  type: "Text" | "Number" | "Select" | "Checkbox" | "Date";
  required: boolean;
  options?: string[];
};

export type PublicAvailabilityDto = {
  date: string;
  slots: string[];
  staffAvailability?: {
    staffId: string;
    staffName: string;
    slots: string[];
  }[];
};

export type CreatePublicBookingRequest = {
  bookingTypeSlug: string;
  date: string;
  startTime: string;
  staffId?: string;
  customerName: string;
  customerEmail: string;
  customerPhone?: string;
  customFields?: Record<string, unknown>;
};

export type PublicBookingCreatedDto = {
  id: string;
  bookingTypeSlug: string;
  date: string;
  startTime: string;
  endTime: string;
  status: string;
  customerName: string;
  customerEmail: string;
  priceCentavos: number;
};

export function usePublicBookingTypes(tenantSlug: string) {
  return useQuery<PublicBookingTypeDto[]>({
    queryKey: ["public-booking-types", tenantSlug],
    queryFn: async () => {
      const res = await fetch(`/api/public/${tenantSlug}/booking-types`);
      if (!res.ok) throw new Error("Failed to fetch booking types");
      const data = await res.json();
      // Map API response (with 'kind' from BookingKind enum) to PublicBookingTypeDto
      return data.map((bt: Record<string, unknown>) => ({
        id: bt.id,
        slug: bt.slug,
        name: bt.name,
        description: bt.description,
        kind: bt.kind === "Calendar" ? "Calendar" : "TimeSlot",
        durationMinutes: bt.durationMinutes,
        priceCentavos: bt.priceInCentavos,
        requiresStaffAssignment: bt.requiresStaffAssignment,
        customFieldSchema: bt.customFieldSchema,
      }));
    },
    enabled: !!tenantSlug,
  });
}

export function usePublicAvailability(
  tenantSlug: string,
  btSlug: string,
  date: string,
) {
  return useQuery<PublicAvailabilityDto>({
    queryKey: ["public-availability", tenantSlug, btSlug, date],
    queryFn: async () => {
      const res = await fetch(
        `/api/public/${tenantSlug}/${btSlug}/availability?date=${date}`,
      );
      if (!res.ok) throw new Error("Failed to fetch availability");
      return res.json();
    },
    enabled: !!tenantSlug && !!btSlug && !!date,
  });
}

export function useCreatePublicBooking(tenantSlug: string) {
  return useMutation({
    mutationFn: async (data: CreatePublicBookingRequest) => {
      const res = await fetch(`/api/public/${tenantSlug}/bookings`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) {
        const err = await res.text();
        throw new Error(err || "Failed to create booking");
      }
      return res.json() as Promise<PublicBookingCreatedDto>;
    },
  });
}

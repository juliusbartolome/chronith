import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

export interface BookingFilters {
  page?: number;
  pageSize?: number;
  status?: string;
  bookingTypeSlug?: string;
}

export function useBookings(filters: BookingFilters = {}) {
  const params = new URLSearchParams();
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
  if (filters.status) params.set("status", filters.status);

  return useQuery({
    queryKey: ["bookings", filters],
    queryFn: async () => {
      const res = await fetch(`/api/bookings?${params}`);
      if (!res.ok) throw new Error("Failed to fetch bookings");
      return res.json();
    },
  });
}

export function useBooking(id: string) {
  return useQuery({
    queryKey: ["bookings", id],
    queryFn: async () => {
      const res = await fetch(`/api/bookings/${id}`);
      if (!res.ok) throw new Error("Failed to fetch booking");
      return res.json();
    },
    enabled: !!id,
  });
}

export function useConfirmBooking() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/bookings/${id}/confirm`, {
        method: "POST",
      });
      if (!res.ok) throw new Error("Failed to confirm booking");
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["bookings"] });
    },
  });
}

export function usePayBooking() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/bookings/${id}/pay`, {
        method: "POST",
      });
      if (!res.ok) throw new Error("Failed to mark booking as paid");
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["bookings"] });
    },
  });
}

export function useCancelBooking() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reason }: { id: string; reason: string }) => {
      const res = await fetch(`/api/bookings/${id}/cancel`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason }),
      });
      if (!res.ok) throw new Error("Failed to cancel booking");
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["bookings"] }),
  });
}

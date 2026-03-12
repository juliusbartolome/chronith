import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

export interface BookingTypeFilters {
  page?: number;
  pageSize?: number;
}

export function useBookingTypes(filters: BookingTypeFilters = {}) {
  const params = new URLSearchParams();
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));

  return useQuery({
    queryKey: ["booking-types", filters],
    queryFn: async () => {
      const res = await fetch(`/api/booking-types?${params}`);
      if (!res.ok) throw new Error("Failed to fetch booking types");
      return res.json();
    },
  });
}

export function useBookingType(slug: string) {
  return useQuery({
    queryKey: ["booking-types", slug],
    queryFn: async () => {
      const res = await fetch(`/api/booking-types/${slug}`);
      if (!res.ok) throw new Error("Failed to fetch booking type");
      return res.json();
    },
    enabled: !!slug,
  });
}

export function useCreateBookingType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => {
      const res = await fetch("/api/booking-types", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to create booking type");
      return res.json();
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["booking-types"] }),
  });
}

export function useUpdateBookingType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      slug,
      data,
    }: {
      slug: string;
      data: Record<string, unknown>;
    }) => {
      const res = await fetch(`/api/booking-types/${slug}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to update booking type");
      return res.json();
    },
    onSuccess: (_, { slug }) => {
      qc.invalidateQueries({ queryKey: ["booking-types"] });
      qc.invalidateQueries({ queryKey: ["booking-types", slug] });
    },
  });
}

export function useDeleteBookingType() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (slug: string) => {
      const res = await fetch(`/api/booking-types/${slug}`, {
        method: "DELETE",
      });
      if (!res.ok) throw new Error("Failed to delete booking type");
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["booking-types"] }),
  });
}

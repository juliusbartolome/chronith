import { useQuery } from "@tanstack/react-query";

export interface BookingAnalyticsDto {
  totalBookings: number;
  confirmedRate: number;
  cancellationRate: number;
  averagePerDay: number;
  byStatus: Array<{ status: string; count: number }>;
  overTime: Array<{ date: string; count: number }>;
  byBookingType: Array<{ name: string; count: number }>;
  byStaffMember: Array<{ name: string; count: number }>;
}

export interface RevenueAnalyticsDto {
  totalRevenueCentavos: number;
  averageBookingValueCentavos: number;
  overTime: Array<{ date: string; amountCentavos: number }>;
  byBookingType: Array<{ name: string; amountCentavos: number }>;
  byStaffMember: Array<{ name: string; amountCentavos: number }>;
}

export interface UtilizationAnalyticsDto {
  overallUtilizationRate: number;
  busiestDayOfWeek: string;
  busiestTimeSlot: string;
  byBookingType: Array<{ name: string; fillRate: number }>;
  byStaffMember: Array<{ name: string; utilizationRate: number }>;
}

export interface AnalyticsFilters {
  from?: string;
  to?: string;
  groupBy?: string;
}

export function useBookingAnalytics(filters: AnalyticsFilters = {}) {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.groupBy) params.set("groupBy", filters.groupBy);

  return useQuery<BookingAnalyticsDto>({
    queryKey: ["analytics", "bookings", filters],
    queryFn: async () => {
      const qs = params.toString();
      const res = await fetch(`/api/analytics/bookings${qs ? `?${qs}` : ""}`);
      if (!res.ok) throw new Error("Failed to fetch booking analytics");
      return res.json();
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useRevenueAnalytics(filters: AnalyticsFilters = {}) {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  if (filters.groupBy) params.set("groupBy", filters.groupBy);

  return useQuery<RevenueAnalyticsDto>({
    queryKey: ["analytics", "revenue", filters],
    queryFn: async () => {
      const qs = params.toString();
      const res = await fetch(`/api/analytics/revenue${qs ? `?${qs}` : ""}`);
      if (!res.ok) throw new Error("Failed to fetch revenue analytics");
      return res.json();
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useUtilizationAnalytics(
  filters: Pick<AnalyticsFilters, "from" | "to"> = {},
) {
  const params = new URLSearchParams();
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);

  return useQuery<UtilizationAnalyticsDto>({
    queryKey: ["analytics", "utilization", filters],
    queryFn: async () => {
      const qs = params.toString();
      const res = await fetch(
        `/api/analytics/utilization${qs ? `?${qs}` : ""}`,
      );
      if (!res.ok) throw new Error("Failed to fetch utilization analytics");
      return res.json();
    },
    staleTime: 5 * 60 * 1000,
  });
}

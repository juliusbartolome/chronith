import { useQuery } from "@tanstack/react-query";

export interface TenantUsageDto {
  bookingTypesUsed: number;
  bookingTypesLimit: number;
  staffMembersUsed: number;
  staffMembersLimit: number;
  bookingsThisMonth: number;
  bookingsPerMonthLimit: number;
  customersUsed: number;
  customersLimit: number;
  planName: string;
}

export function useUsage() {
  return useQuery<TenantUsageDto>({
    queryKey: ["usage"],
    queryFn: async () => {
      const res = await fetch("/api/tenant/usage");
      if (!res.ok) throw new Error("Failed to fetch usage");
      return res.json();
    },
  });
}

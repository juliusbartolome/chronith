import { useQuery } from "@tanstack/react-query";

export interface TenantPlanDto {
  id: string;
  name: string;
  maxBookingTypes: number;
  maxStaffMembers: number;
  maxBookingsPerMonth: number;
  maxCustomers: number;
  notificationsEnabled: boolean;
  analyticsEnabled: boolean;
  customBrandingEnabled: boolean;
  apiAccessEnabled: boolean;
  auditLogEnabled: boolean;
  priceCentavos: number;
  sortOrder: number;
}

export function usePlans() {
  return useQuery<TenantPlanDto[]>({
    queryKey: ["plans"],
    queryFn: async () => {
      const res = await fetch("/api/plans");
      if (!res.ok) throw new Error("Failed to fetch plans");
      return res.json();
    },
  });
}

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export type TenantSettingsDto = {
  id: string;
  tenantId: string;
  logoUrl: string | null;
  primaryColor: string;
  accentColor: string | null;
  customDomain: string | null;
  bookingPageEnabled: boolean;
  welcomeMessage: string | null;
  termsUrl: string | null;
  privacyUrl: string | null;
  updatedAt: string;
};

export type UpdateTenantSettingsRequest = Partial<{
  logoUrl: string | null;
  primaryColor: string;
  accentColor: string | null;
  customDomain: string | null;
  bookingPageEnabled: boolean;
  welcomeMessage: string | null;
  termsUrl: string | null;
  privacyUrl: string | null;
}>;

export function useTenantSettings() {
  return useQuery<TenantSettingsDto>({
    queryKey: ["tenant-settings"],
    queryFn: async () => {
      const res = await fetch("/api/tenant/settings");
      if (!res.ok) throw new Error("Failed to fetch tenant settings");
      return res.json();
    },
  });
}

export function useUpdateTenantSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: UpdateTenantSettingsRequest) => {
      const res = await fetch("/api/tenant/settings", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to update tenant settings");
      return res.json() as Promise<TenantSettingsDto>;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["tenant-settings"] });
    },
  });
}

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface TenantSubscriptionDto {
  id: string;
  tenantId: string;
  planId: string;
  planName: string;
  status: string;
  trialEndsAt: string | null;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  paymentProviderSubscriptionId: string | null;
  createdAt: string;
  cancelledAt: string | null;
  cancelReason: string | null;
}

export function useSubscription() {
  return useQuery<TenantSubscriptionDto>({
    queryKey: ["subscription"],
    queryFn: async () => {
      const res = await fetch("/api/tenant/subscription");
      if (!res.ok) throw new Error("Failed to fetch subscription");
      return res.json();
    },
  });
}

export function useChangePlan() {
  const queryClient = useQueryClient();
  return useMutation<TenantSubscriptionDto, Error, { newPlanId: string }>({
    mutationFn: async ({ newPlanId }) => {
      const res = await fetch("/api/tenant/subscription", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ newPlanId }),
      });
      if (!res.ok) throw new Error("Failed to change plan");
      return res.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["subscription"] });
      queryClient.invalidateQueries({ queryKey: ["usage"] });
    },
  });
}

export function useCancelSubscription() {
  const queryClient = useQueryClient();
  return useMutation<void, Error, { reason?: string }>({
    mutationFn: async ({ reason }) => {
      const res = await fetch("/api/tenant/subscription", {
        method: "DELETE",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason }),
      });
      if (!res.ok) throw new Error("Failed to cancel subscription");
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["subscription"] });
    },
  });
}

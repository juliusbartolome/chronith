import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

export interface WebhookDto {
  id: string;
  url: string;
  events: string[];
  isActive: boolean;
  lastDeliveryStatus?: string;
  secret?: string;
}

export interface WebhookDeliveryDto {
  id: string;
  webhookId: string;
  event: string;
  statusCode?: number;
  requestBody: string;
  responseBody?: string;
  deliveredAt?: string;
  status: "Success" | "Failed" | "Pending";
  errorMessage?: string;
}

export function useWebhooks(bookingTypeSlug: string) {
  return useQuery<WebhookDto[]>({
    queryKey: ["webhooks", bookingTypeSlug],
    queryFn: async () => {
      const res = await fetch(
        `/api/booking-types/${bookingTypeSlug}/webhooks`,
      );
      if (!res.ok) throw new Error("Failed to fetch webhooks");
      return res.json();
    },
    enabled: !!bookingTypeSlug,
  });
}

export function useCreateWebhook(bookingTypeSlug: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (data: {
      url: string;
      events: string[];
      secret?: string;
    }) => {
      const res = await fetch(
        `/api/booking-types/${bookingTypeSlug}/webhooks`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(data),
        },
      );
      if (!res.ok) throw new Error("Failed to create webhook");
      return res.json();
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["webhooks", bookingTypeSlug] }),
  });
}

export function useDeleteWebhook(bookingTypeSlug: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (webhookId: string) => {
      const res = await fetch(
        `/api/booking-types/${bookingTypeSlug}/webhooks/${webhookId}`,
        { method: "DELETE" },
      );
      if (!res.ok) throw new Error("Failed to delete webhook");
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["webhooks", bookingTypeSlug] }),
  });
}

export function useWebhookDeliveries(
  bookingTypeSlug: string,
  webhookId: string | null,
) {
  return useQuery<WebhookDeliveryDto[]>({
    queryKey: ["webhook-deliveries", bookingTypeSlug, webhookId],
    queryFn: async () => {
      const res = await fetch(
        `/api/booking-types/${bookingTypeSlug}/webhooks/${webhookId}/deliveries`,
      );
      if (!res.ok) throw new Error("Failed to fetch webhook deliveries");
      return res.json();
    },
    enabled: !!webhookId,
  });
}

export function useRetryWebhookDelivery(bookingTypeSlug: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      webhookId,
      deliveryId,
    }: {
      webhookId: string;
      deliveryId: string;
    }) => {
      const res = await fetch(
        `/api/booking-types/${bookingTypeSlug}/webhooks/${webhookId}/retry/${deliveryId}`,
        { method: "POST" },
      );
      if (!res.ok) throw new Error("Failed to retry delivery");
      return res.json();
    },
    onSuccess: (_, { webhookId }) =>
      qc.invalidateQueries({
        queryKey: ["webhook-deliveries", bookingTypeSlug, webhookId],
      }),
  });
}

export function useTestWebhook(bookingTypeSlug: string) {
  return useMutation({
    mutationFn: async (webhookId: string) => {
      const res = await fetch(
        `/api/booking-types/${bookingTypeSlug}/webhooks/${webhookId}/test`,
        { method: "POST" },
      );
      if (!res.ok) throw new Error("Failed to send test event");
      return res.json();
    },
  });
}

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface NotificationTemplateDto {
  id: string;
  tenantId: string;
  eventType: string;
  channelType: "Email" | "Sms" | "Push";
  subject?: string;
  body: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export function useNotificationTemplates() {
  return useQuery<NotificationTemplateDto[]>({
    queryKey: ["notification-templates"],
    queryFn: () => fetchJson("/api/notification-templates"),
  });
}

export function useUpdateNotificationTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      data,
    }: {
      id: string;
      data: { subject?: string; body: string; isActive: boolean };
    }) => {
      const res = await fetch(`/api/notification-templates/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error(await res.text());
      return res.json();
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["notification-templates"] }),
  });
}

export function useResetNotificationTemplate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (eventType: string) => {
      const res = await fetch(`/api/notification-templates/reset/${eventType}`, {
        method: "POST",
      });
      if (!res.ok) throw new Error(await res.text());
      return res.json();
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["notification-templates"] }),
  });
}

export function usePreviewNotificationTemplate() {
  return useMutation({
    mutationFn: async ({
      id,
      sampleData,
    }: {
      id: string;
      sampleData: Record<string, string>;
    }) => {
      const res = await fetch(`/api/notification-templates/${id}/preview`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ variables: sampleData }),
      });
      if (!res.ok) throw new Error(await res.text());
      return res.json() as Promise<{ subject?: string; body: string }>;
    },
  });
}

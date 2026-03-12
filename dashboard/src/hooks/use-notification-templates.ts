import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface NotificationTemplateDto {
  id: string;
  eventType: string;
  channel: "Email" | "Sms" | "Push";
  subjectTemplate?: string;
  bodyTemplate: string;
  variables: string[];
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
      data: { subjectTemplate?: string; bodyTemplate: string };
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
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/notification-templates/${id}/reset`, {
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
        body: JSON.stringify({ sampleData }),
      });
      if (!res.ok) throw new Error(await res.text());
      return res.json() as Promise<{ subject?: string; body: string }>;
    },
  });
}

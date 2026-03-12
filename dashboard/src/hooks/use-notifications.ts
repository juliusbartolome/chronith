import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface NotificationConfigDto {
  id: string;
  channel: "Email" | "Sms" | "Push";
  isEnabled: boolean;
  smtpHost?: string;
  smtpPort?: number;
  smtpUsername?: string;
  fromEmail?: string;
  twilioAccountSid?: string;
  twilioFromNumber?: string;
  firebaseProjectId?: string;
}

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export function useNotificationConfigs() {
  return useQuery<NotificationConfigDto[]>({
    queryKey: ["notification-configs"],
    queryFn: () => fetchJson("/api/notifications/config"),
  });
}

export function useUpdateNotificationConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      channel,
      data,
    }: {
      channel: string;
      data: Partial<NotificationConfigDto>;
    }) => {
      const res = await fetch(`/api/notifications/config/${channel}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error(await res.text());
      return res.json();
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["notification-configs"] }),
  });
}

export function useDisableNotificationChannel() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (channel: string) => {
      const res = await fetch(`/api/notifications/config/${channel}`, {
        method: "DELETE",
      });
      if (!res.ok) throw new Error(await res.text());
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["notification-configs"] }),
  });
}

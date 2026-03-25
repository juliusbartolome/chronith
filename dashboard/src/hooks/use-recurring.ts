import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface RecurringRuleDto {
  id: string;
  customerFirstName: string;
  customerLastName: string;
  customerEmail: string;
  bookingTypeName: string;
  frequency: "Daily" | "Weekly" | "Monthly";
  interval: number;
  startTime: string;
  durationMinutes: number;
  nextOccurrenceAt?: string;
  status: "Active" | "Cancelled";
  totalOccurrences: number;
  seriesStartAt: string;
  seriesEndAt?: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

async function fetchJson<T>(url: string): Promise<T> {
  const res = await fetch(url);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export function useRecurringRules(params?: {
  page?: number;
  bookingTypeId?: string;
  status?: string;
}) {
  const sp = new URLSearchParams();
  if (params?.page) sp.set("page", String(params.page));
  if (params?.bookingTypeId) sp.set("bookingTypeId", params.bookingTypeId);
  if (params?.status) sp.set("status", params.status);
  const q = sp.toString();

  return useQuery<PagedResult<RecurringRuleDto>>({
    queryKey: ["recurring", params],
    queryFn: () => fetchJson(`/api/recurring${q ? `?${q}` : ""}`),
  });
}

export function useCancelRecurringSeries() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/recurring/${id}`, { method: "DELETE" });
      if (!res.ok) throw new Error(await res.text());
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["recurring"] }),
  });
}

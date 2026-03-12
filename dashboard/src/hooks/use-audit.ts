import { useQuery } from "@tanstack/react-query";

export interface AuditEntryDto {
  id: string;
  timestamp: string;
  userId: string;
  userName: string;
  userRole: string;
  entityType: string;
  entityId: string;
  action: string;
  summary: string;
  oldValues?: Record<string, unknown>;
  newValues?: Record<string, unknown>;
  ipAddress?: string;
  correlationId?: string;
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

export function useAuditEntries(params?: {
  entityType?: string;
  action?: string;
  userId?: string;
  from?: string;
  to?: string;
  search?: string;
  page?: number;
}) {
  const sp = new URLSearchParams();
  if (params?.entityType) sp.set("entityType", params.entityType);
  if (params?.action) sp.set("action", params.action);
  if (params?.userId) sp.set("userId", params.userId);
  if (params?.from) sp.set("from", params.from);
  if (params?.to) sp.set("to", params.to);
  if (params?.search) sp.set("search", params.search);
  if (params?.page) sp.set("page", String(params.page));
  const q = sp.toString();

  return useQuery<PagedResult<AuditEntryDto>>({
    queryKey: ["audit", params],
    queryFn: () => fetchJson(`/api/audit${q ? `?${q}` : ""}`),
  });
}

export function useAuditEntry(id: string) {
  return useQuery<AuditEntryDto>({
    queryKey: ["audit", id],
    queryFn: () => fetchJson(`/api/audit/${id}`),
    enabled: !!id,
  });
}

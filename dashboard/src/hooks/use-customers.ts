import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export interface CustomerDto {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  authProvider: "BuiltIn" | "Oidc";
  bookingCount: number;
  isActive: boolean;
  createdAt: string;
  lastBookingAt?: string;
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

export function useCustomers(params?: {
  page?: number;
  search?: string;
  authProvider?: string;
  isActive?: boolean;
}) {
  const sp = new URLSearchParams();
  if (params?.page) sp.set("page", String(params.page));
  if (params?.search) sp.set("search", params.search);
  if (params?.authProvider) sp.set("authProvider", params.authProvider);
  if (params?.isActive !== undefined)
    sp.set("isActive", String(params.isActive));
  const q = sp.toString();

  return useQuery<PagedResult<CustomerDto>>({
    queryKey: ["customers", params],
    queryFn: () => fetchJson(`/api/customers${q ? `?${q}` : ""}`),
  });
}

export function useDeactivateCustomer() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/customers/${id}`, { method: "DELETE" });
      if (!res.ok) throw new Error(await res.text());
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["customers"] }),
  });
}

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

export interface StaffFilters {
  page?: number;
  pageSize?: number;
  isActive?: boolean;
}

export function useStaffList(filters: StaffFilters = {}) {
  const params = new URLSearchParams();
  if (filters.page) params.set("page", String(filters.page));
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
  if (filters.isActive !== undefined)
    params.set("isActive", String(filters.isActive));

  return useQuery({
    queryKey: ["staff", filters],
    queryFn: async () => {
      const res = await fetch(`/api/staff?${params}`);
      if (!res.ok) throw new Error("Failed to fetch staff");
      return res.json();
    },
  });
}

export function useStaffMember(id: string) {
  return useQuery({
    queryKey: ["staff", id],
    queryFn: async () => {
      const res = await fetch(`/api/staff/${id}`);
      if (!res.ok) throw new Error("Failed to fetch staff member");
      return res.json();
    },
    enabled: !!id,
  });
}

export function useCreateStaff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (data: { name: string; email: string }) => {
      const res = await fetch("/api/staff", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to create staff member");
      return res.json();
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["staff"] }),
  });
}

export function useUpdateStaff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      data,
    }: {
      id: string;
      data: { name: string; email: string };
    }) => {
      const res = await fetch(`/api/staff/${id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to update staff member");
      return res.json();
    },
    onSuccess: (_, { id }) => {
      qc.invalidateQueries({ queryKey: ["staff"] });
      qc.invalidateQueries({ queryKey: ["staff", id] });
    },
  });
}

export function useUpdateStaffAvailability() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      windows,
    }: {
      id: string;
      windows: Array<{
        dayOfWeek: number;
        startTime: string;
        endTime: string;
      }>;
    }) => {
      const res = await fetch(`/api/staff/${id}/availability`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ windows }),
      });
      if (!res.ok) throw new Error("Failed to update availability");
    },
    onSuccess: (_, { id }) => qc.invalidateQueries({ queryKey: ["staff", id] }),
  });
}

export function useDeactivateStaff() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/staff/${id}`, { method: "DELETE" });
      if (!res.ok) throw new Error("Failed to deactivate staff member");
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["staff"] }),
  });
}

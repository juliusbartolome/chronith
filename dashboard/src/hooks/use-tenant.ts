import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";

export function useTenantProfile() {
  return useQuery({
    queryKey: ["tenant", "profile"],
    queryFn: async () => {
      const res = await fetch("/api/settings/profile");
      if (!res.ok) throw new Error("Failed to fetch tenant profile");
      return res.json();
    },
  });
}

export function useUpdateTenantProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (data: { name: string; timezone: string }) => {
      const res = await fetch("/api/settings/profile", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to update tenant profile");
      return res.json();
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["tenant", "profile"] }),
  });
}

export function useTenantAuthConfig() {
  return useQuery({
    queryKey: ["tenant", "auth-config"],
    queryFn: async () => {
      const res = await fetch("/api/settings/auth");
      if (!res.ok) throw new Error("Failed to fetch auth config");
      return res.json();
    },
  });
}

export function useUpdateTenantAuthConfig() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (data: Record<string, unknown>) => {
      const res = await fetch("/api/settings/auth", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to update auth config");
      return res.json();
    },
    onSuccess: () =>
      qc.invalidateQueries({ queryKey: ["tenant", "auth-config"] }),
  });
}

export function useApiKeys() {
  return useQuery({
    queryKey: ["api-keys"],
    queryFn: async () => {
      const res = await fetch("/api/settings/api-keys");
      if (!res.ok) throw new Error("Failed to fetch API keys");
      return res.json();
    },
  });
}

export function useCreateApiKey() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (data: {
      label: string;
      role: string;
      expiresAt?: string;
    }) => {
      const res = await fetch("/api/settings/api-keys", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Failed to create API key");
      return res.json();
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["api-keys"] }),
  });
}

export function useRevokeApiKey() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const res = await fetch(`/api/settings/api-keys/${id}`, {
        method: "DELETE",
      });
      if (!res.ok) throw new Error("Failed to revoke API key");
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ["api-keys"] }),
  });
}

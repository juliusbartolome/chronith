import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

export type CustomerProfileDto = {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  mobile: string | null;
  emailVerified: boolean;
  createdAt: string;
};

export type CustomerLoginRequest = {
  email: string;
  password: string;
};

export type CustomerRegisterRequest = {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  mobile?: string;
};

export type CustomerBookingDto = {
  id: string;
  bookingTypeName: string;
  startTime: string;
  endTime: string;
  status: string;
  staffName: string | null;
  priceCentavos: number;
};

export function useCustomerLogin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CustomerLoginRequest) => {
      const res = await fetch("/api/public/auth/customer/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Login failed");
      return res.json() as Promise<CustomerProfileDto>;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["customer-me"] });
    },
  });
}

export function useCustomerRegister() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: CustomerRegisterRequest) => {
      const res = await fetch("/api/public/auth/customer/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(data),
      });
      if (!res.ok) throw new Error("Registration failed");
      return res.json() as Promise<CustomerProfileDto>;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["customer-me"] });
    },
  });
}

export function useCustomerLogout() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await fetch("/api/public/auth/customer/logout", { method: "POST" });
    },
    onSuccess: () => {
      queryClient.setQueryData(["customer-me"], null);
      queryClient.invalidateQueries({ queryKey: ["customer-me"] });
    },
  });
}

export function useCustomerMe() {
  return useQuery<CustomerProfileDto | null>({
    queryKey: ["customer-me"],
    queryFn: async () => {
      const res = await fetch("/api/public/auth/customer/me");
      if (res.status === 401) return null;
      if (!res.ok) throw new Error("Failed to fetch profile");
      return res.json();
    },
    staleTime: 5 * 60 * 1000,
  });
}

export function useCustomerBookings() {
  return useQuery<{ items: CustomerBookingDto[]; totalCount: number }>({
    queryKey: ["customer-bookings"],
    queryFn: async () => {
      const res = await fetch("/api/public/auth/customer/bookings");
      if (res.status === 401) return { items: [], totalCount: 0 };
      if (!res.ok) throw new Error("Failed to fetch bookings");
      return res.json();
    },
    staleTime: 60 * 1000,
  });
}

export type BookingDetailDto = {
  id: string;
  bookingTypeName: string;
  startTime: string;
  endTime: string;
  status: string;
  staffName: string | null;
  firstName: string;
  lastName: string;
  customerEmail: string;
  priceCentavos: number;
  notes: string | null;
};

export function useBookingDetail(id: string) {
  return useQuery<BookingDetailDto>({
    queryKey: ["customer-booking", id],
    queryFn: async () => {
      const res = await fetch(`/api/public/auth/customer/bookings/${id}`);
      if (!res.ok) throw new Error("Failed to fetch booking");
      return res.json();
    },
  });
}

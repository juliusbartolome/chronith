import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { PublicCustomField } from "@/hooks/use-public-booking";

export type CustomerInfo = {
  name: string;
  email: string;
  phone?: string;
};

export type BookingSession = {
  tenantSlug: string;
  btSlug: string;
  btName: string;
  durationMinutes: number;
  priceCentavos: number;
  requiresStaffAssignment: boolean;
  customFieldSchema: PublicCustomField[];
  selectedDate: string | null;
  selectedSlot: string | null;
  selectedStaffId: string | null;
  selectedStaffName: string | null;
  customerInfo: CustomerInfo | null;
  customFields: Record<string, unknown>;
  confirmedBookingId: string | null;
};

type BookingSessionActions = {
  setBookingType: (bt: {
    tenantSlug: string;
    btSlug: string;
    btName: string;
    durationMinutes: number;
    priceCentavos: number;
    requiresStaffAssignment: boolean;
    customFieldSchema: PublicCustomField[];
  }) => void;
  setDateAndSlot: (date: string, slot: string) => void;
  setStaff: (staffId: string, staffName: string) => void;
  setCustomerInfo: (info: CustomerInfo) => void;
  setCustomFields: (fields: Record<string, unknown>) => void;
  setConfirmedBookingId: (id: string) => void;
  resetSession: () => void;
};

const initialState: BookingSession = {
  tenantSlug: "",
  btSlug: "",
  btName: "",
  durationMinutes: 0,
  priceCentavos: 0,
  requiresStaffAssignment: false,
  customFieldSchema: [],
  selectedDate: null,
  selectedSlot: null,
  selectedStaffId: null,
  selectedStaffName: null,
  customerInfo: null,
  customFields: {},
  confirmedBookingId: null,
};

export const useBookingSession = create<
  BookingSession & BookingSessionActions
>()(
  persist(
    (set) => ({
      ...initialState,
      setBookingType: (bt) =>
        set({
          tenantSlug: bt.tenantSlug,
          btSlug: bt.btSlug,
          btName: bt.btName,
          durationMinutes: bt.durationMinutes,
          priceCentavos: bt.priceCentavos,
          requiresStaffAssignment: bt.requiresStaffAssignment,
          customFieldSchema: bt.customFieldSchema,
          selectedDate: null,
          selectedSlot: null,
          selectedStaffId: null,
          selectedStaffName: null,
          customerInfo: null,
          customFields: {},
          confirmedBookingId: null,
        }),
      setDateAndSlot: (date, slot) =>
        set({ selectedDate: date, selectedSlot: slot }),
      setStaff: (staffId, staffName) =>
        set({ selectedStaffId: staffId, selectedStaffName: staffName }),
      setCustomerInfo: (info) => set({ customerInfo: info }),
      setCustomFields: (fields) => set({ customFields: fields }),
      setConfirmedBookingId: (id) => set({ confirmedBookingId: id }),
      resetSession: () => set(initialState),
    }),
    {
      name: "chronith-booking-session",
      storage: {
        getItem: (name) => {
          if (typeof window === "undefined") return null;
          const item = sessionStorage.getItem(name);
          return item ? JSON.parse(item) : null;
        },
        setItem: (name, value) => {
          if (typeof window !== "undefined") {
            sessionStorage.setItem(name, JSON.stringify(value));
          }
        },
        removeItem: (name) => {
          if (typeof window !== "undefined") {
            sessionStorage.removeItem(name);
          }
        },
      },
    },
  ),
);

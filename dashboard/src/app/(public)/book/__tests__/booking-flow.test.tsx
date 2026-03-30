import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";

// Mock next/navigation
vi.mock("next/navigation", () => ({
  useParams: () => ({ tenantSlug: "my-salon", btSlug: "haircut" }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

// Mock the manual payment hook (useBookingStatus used by success page)
vi.mock("@/hooks/use-manual-payment", () => ({
  useBookingStatus: vi.fn(() => ({
    data: {
      id: "booking-123",
      referenceId: "REF-123",
      status: "Confirmed",
      start: "2026-03-15T09:00:00Z",
      end: "2026-03-15T10:00:00Z",
      amountInCentavos: 50000,
      currency: "PHP",
      paymentReference: null,
      checkoutUrl: null,
      paymentMode: null,
      manualPaymentOptions: null,
      proofOfPaymentUrl: null,
      proofOfPaymentFileName: null,
      paymentNote: null,
    },
    isLoading: false,
    error: null,
  })),
  useConfirmManualPayment: vi.fn(() => ({
    mutateAsync: vi.fn(),
    isPending: false,
  })),
}));

// Mock the hooks
vi.mock("@/hooks/use-public-booking", () => ({
  usePublicBookingTypes: vi.fn(() => ({
    data: [
      {
        id: "bt-1",
        slug: "haircut",
        name: "Haircut",
        description: "Standard haircut",
        durationMinutes: 60,
        priceCentavos: 50000,
        requiresStaffAssignment: false,
      },
    ],
    isLoading: false,
    isError: false,
  })),
  usePublicAvailability: vi.fn(() => ({
    data: { date: "2026-03-15", slots: ["09:00", "10:00"] },
    isLoading: false,
  })),
  useCreatePublicBooking: vi.fn(() => ({
    mutateAsync: vi.fn(),
    isPending: false,
  })),
}));

// Mock the booking session store
vi.mock("@/lib/booking-session", () => ({
  useBookingSession: vi.fn((selector?: (s: unknown) => unknown) => {
    const state = {
      btName: "Haircut",
      selectedDate: "2026-03-15",
      selectedSlot: "09:00",
      selectedStaffId: null,
      selectedStaffName: null,
      durationMinutes: 60,
      priceCentavos: 50000,
      customerInfo: {
        firstName: "John",
        lastName: "Doe",
        email: "john@example.com",
      },
      customFields: {},
      confirmedBookingId: "booking-123",
      requiresStaffAssignment: false,
      customFieldSchema: [],
      setBookingType: vi.fn(),
      setDateAndSlot: vi.fn(),
      setStaff: vi.fn(),
      setCustomerInfo: vi.fn(),
      setCustomFields: vi.fn(),
      setConfirmedBookingId: vi.fn(),
      resetSession: vi.fn(),
    };
    return selector ? selector(state) : state;
  }),
}));

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

describe("Public Booking Landing Page", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders booking type cards", async () => {
    const { default: LandingPage } = await import("../[tenantSlug]/page");
    render(createElement(LandingPage), { wrapper: createWrapper() });
    await waitFor(() =>
      expect(screen.getByText("Haircut")).toBeInTheDocument(),
    );
  });
});

describe("Confirm Page", () => {
  it("renders review summary with booking details", async () => {
    const { default: ConfirmPage } =
      await import("../[tenantSlug]/[btSlug]/confirm/page");
    render(createElement(ConfirmPage), { wrapper: createWrapper() });
    await waitFor(() =>
      expect(screen.getByText("Review Booking")).toBeInTheDocument(),
    );
    expect(screen.getByText("Haircut")).toBeInTheDocument();
    expect(screen.getByText("2026-03-15")).toBeInTheDocument();
  });
});

describe("Success Page", () => {
  it("renders booking confirmation message", async () => {
    const { default: SuccessPage } =
      await import("../[tenantSlug]/[btSlug]/success/page");
    render(createElement(SuccessPage), { wrapper: createWrapper() });
    await waitFor(() =>
      expect(
        screen.getByText("Your booking is confirmed!"),
      ).toBeInTheDocument(),
    );
  });
});

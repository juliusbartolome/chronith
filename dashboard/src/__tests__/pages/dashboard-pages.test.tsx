import { render, screen, fireEvent, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";

// ─── Audit Page ───────────────────────────────────────────────────────────────

vi.mock("@/hooks/use-audit", () => ({
  useAuditEntries: vi.fn(),
  useAuditEntry: vi.fn(),
}));

vi.mock("@/hooks/use-customers", () => ({
  useCustomers: vi.fn(),
  useDeactivateCustomer: vi.fn(),
}));

vi.mock("@/hooks/use-recurring", () => ({
  useRecurringRules: vi.fn(),
  useCancelRecurringSeries: vi.fn(),
}));

import { useAuditEntries, useAuditEntry } from "@/hooks/use-audit";
import { useCustomers, useDeactivateCustomer } from "@/hooks/use-customers";
import {
  useRecurringRules,
  useCancelRecurringSeries,
} from "@/hooks/use-recurring";
import AuditPage from "@/app/(dashboard)/audit/page";
import CustomersPage from "@/app/(dashboard)/customers/page";
import RecurringPage from "@/app/(dashboard)/recurring/page";

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children);
  Wrapper.displayName = "TestWrapper";
  return Wrapper;
}

const mockMutation = {
  mutate: vi.fn(),
  mutateAsync: vi.fn(),
  isPending: false,
  isSuccess: false,
  isError: false,
  error: null,
  reset: vi.fn(),
};

// ─── AuditPage ────────────────────────────────────────────────────────────────

describe("AuditPage", () => {
  beforeEach(() => vi.clearAllMocks());

  it("shows loading state", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("shows error state", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(
      screen.getByText(/Failed to load audit entries/i),
    ).toBeInTheDocument();
  });

  it("renders audit entries when data is available", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "a1",
            timestamp: "2026-01-01T10:00:00Z",
            userName: "Alice",
            userRole: "Admin",
            entityType: "Booking",
            entityId: "b1",
            action: "Create",
            summary: "Created booking",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("Create")).toBeInTheDocument();
    expect(screen.getByText("Created booking")).toBeInTheDocument();
  });

  it("renders heading", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Audit Log")).toBeInTheDocument();
  });

  it("shows pagination and total count when data is present", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 42, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Total: 42")).toBeInTheDocument();
  });

  it("can click View to open modal", () => {
    const entry = {
      id: "a1",
      timestamp: "2026-01-01T10:00:00Z",
      userName: "Alice",
      userRole: "Admin",
      entityType: "Booking",
      entityId: "b1",
      action: "Create",
      summary: "Created booking",
    };
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [entry], totalCount: 1, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    vi.mocked(useAuditEntry).mockReturnValue({
      data: {
        ...entry,
        ipAddress: "127.0.0.1",
        oldValues: null,
        newValues: null,
      },
    } as unknown as ReturnType<typeof useAuditEntry>);

    const { getByRole } = render(<AuditPage />, { wrapper: createWrapper() });
    act(() => {
      fireEvent.click(getByRole("button", { name: /view/i }));
    });
    // After clicking View, the modal should render the action
    expect(screen.getAllByText("Create").length).toBeGreaterThan(0);
  });

  it("modal shows IP address when detail loaded", () => {
    const entry = {
      id: "a2",
      timestamp: "2026-01-01T10:00:00Z",
      userName: "Bob",
      userRole: "Staff",
      entityType: "Booking",
      entityId: "b2",
      action: "Update",
      summary: "Updated booking",
    };
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [entry], totalCount: 1, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    vi.mocked(useAuditEntry).mockReturnValue({
      data: {
        ...entry,
        ipAddress: "192.168.1.1",
        oldValues: null,
        newValues: null,
      },
    } as unknown as ReturnType<typeof useAuditEntry>);

    render(<AuditPage />, { wrapper: createWrapper() });
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /view/i }));
    });
    expect(screen.getByText("192.168.1.1")).toBeInTheDocument();
  });

  it("modal shows old and new values when present", () => {
    const entry = {
      id: "a3",
      timestamp: "2026-01-01T10:00:00Z",
      userName: "Carol",
      userRole: "Admin",
      entityType: "Booking",
      entityId: "b3",
      action: "Update",
      summary: "Updated price",
    };
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [entry], totalCount: 1, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    vi.mocked(useAuditEntry).mockReturnValue({
      data: {
        ...entry,
        ipAddress: "10.0.0.1",
        oldValues: { price: 100 },
        newValues: { price: 200 },
      },
    } as unknown as ReturnType<typeof useAuditEntry>);

    render(<AuditPage />, { wrapper: createWrapper() });
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /view/i }));
    });
    expect(screen.getByText("Old Values")).toBeInTheDocument();
    expect(screen.getByText("New Values")).toBeInTheDocument();
  });

  it("pagination Previous button is disabled on page 1", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 0, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(screen.getByRole("button", { name: /previous/i })).toBeDisabled();
  });

  it("pagination Next button is disabled when items < pageSize", () => {
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 0, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    expect(screen.getByRole("button", { name: /^next$/i })).toBeDisabled();
  });

  it("clicking Next on audit page increments page (not disabled when full page)", () => {
    const items = Array.from({ length: 20 }, (_, i) => ({
      id: `a${i}`,
      timestamp: "2026-01-01T10:00:00Z",
      userName: `User${i}`,
      userRole: "Admin",
      entityType: "Booking",
      entityId: `b${i}`,
      action: "Create",
      summary: "Created",
    }));
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items, totalCount: 40, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    const next = screen.getByRole("button", { name: /^next$/i });
    expect(next).not.toBeDisabled();
    act(() => {
      fireEvent.click(next);
    });
    // After click, page state updates — just verify it didn't throw
    expect(
      screen.getByRole("button", { name: /previous/i }),
    ).not.toBeDisabled();
  });

  it("clicking Previous on audit page after advancing decrements page", () => {
    const items = Array.from({ length: 20 }, (_, i) => ({
      id: `a${i}`,
      timestamp: "2026-01-01T10:00:00Z",
      userName: `User${i}`,
      userRole: "Admin",
      entityType: "Booking",
      entityId: `b${i}`,
      action: "Create",
      summary: "Created",
    }));
    vi.mocked(useAuditEntries).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items, totalCount: 40, pageSize: 20 },
    } as unknown as ReturnType<typeof useAuditEntries>);
    render(<AuditPage />, { wrapper: createWrapper() });
    // Advance to page 2
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    });
    // Previous should now be enabled (page > 1)
    const prev = screen.getByRole("button", { name: /previous/i });
    expect(prev).not.toBeDisabled();
    // Go back to page 1
    act(() => {
      fireEvent.click(prev);
    });
    expect(screen.getByRole("button", { name: /previous/i })).toBeDisabled();
  });
});

// ─── CustomersPage ────────────────────────────────────────────────────────────

describe("CustomersPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useDeactivateCustomer).mockReturnValue(
      mockMutation as unknown as ReturnType<typeof useDeactivateCustomer>,
    );
  });

  it("shows loading state", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("shows error state", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(screen.getByText(/Failed to load customers/i)).toBeInTheDocument();
  });

  it("renders customers when data is available", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "c1",
            firstName: "Bob",
            lastName: "Smith",
            email: "bob@example.com",
            mobile: "+639171234567",
            isActive: true,
            totalBookings: 3,
            createdAt: "2026-01-01T00:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Bob Smith")).toBeInTheDocument();
    expect(screen.getByText("bob@example.com")).toBeInTheDocument();
  });

  it("renders heading", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Customers")).toBeInTheDocument();
  });

  it("shows total count in pagination", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 7, pageSize: 20 },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(screen.getByText(/Total: 7/i)).toBeInTheDocument();
  });

  it("shows inactive badge for inactive customers", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "c2",
            firstName: "Dave",
            lastName: "",
            email: "dave@x.com",
            mobile: null,
            isActive: false,
            totalBookings: 0,
            createdAt: "2026-01-01T00:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Inactive")).toBeInTheDocument();
  });
});

// ─── CustomersPage — additional branch coverage ───────────────────────────────

describe("CustomersPage — deactivate dialog", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useDeactivateCustomer).mockReturnValue(
      mockMutation as unknown as ReturnType<typeof useDeactivateCustomer>,
    );
  });

  it("shows Deactivate button for active customers", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "c1",
            firstName: "Alice",
            lastName: "",
            email: "alice@x.com",
            mobile: null,
            authProvider: "Email",
            bookingCount: 2,
            isActive: true,
            createdAt: "2026-01-01T00:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(
      screen.getByRole("button", { name: /deactivate/i }),
    ).toBeInTheDocument();
  });

  it("opens confirmation dialog on Deactivate click", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "c1",
            firstName: "Alice",
            lastName: "",
            email: "alice@x.com",
            mobile: null,
            authProvider: "Email",
            bookingCount: 2,
            isActive: true,
            createdAt: "2026-01-01T00:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /deactivate/i }));
    });
    expect(screen.getByText("Deactivate customer?")).toBeInTheDocument();
  });

  it("shows Previous pagination button disabled on page 1", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 0, pageSize: 20 },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    const prev = screen.getByRole("button", { name: /previous/i });
    expect(prev).toBeDisabled();
  });

  it("shows Next pagination button disabled when items < pageSize", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 0, pageSize: 20 },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    const next = screen.getByRole("button", { name: /^next$/i });
    expect(next).toBeDisabled();
  });

  it("does not show Deactivate button for inactive customers", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "c2",
            firstName: "Dave",
            lastName: "",
            email: "dave@x.com",
            mobile: null,
            authProvider: "Email",
            bookingCount: 0,
            isActive: false,
            createdAt: "2026-01-01T00:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    expect(
      screen.queryByRole("button", { name: /deactivate/i }),
    ).not.toBeInTheDocument();
  });

  it("clicking Deactivate action in dialog calls mutateAsync", () => {
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "c1",
            firstName: "Alice",
            lastName: "",
            email: "alice@x.com",
            mobile: null,
            authProvider: "Email",
            bookingCount: 2,
            isActive: true,
            createdAt: "2026-01-01T00:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useCustomers>);
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    vi.mocked(useDeactivateCustomer).mockReturnValue({
      ...mockMutation,
      mutateAsync,
    } as unknown as ReturnType<typeof useDeactivateCustomer>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /deactivate/i }));
    });
    // Dialog is open, click the Deactivate action button
    const deactivateActions = screen.getAllByRole("button", {
      name: /deactivate/i,
    });
    // The last one is inside the dialog
    act(() => {
      fireEvent.click(deactivateActions[deactivateActions.length - 1]);
    });
    expect(mutateAsync).toHaveBeenCalledWith("c1");
  });

  it("clicking Next on customers page when full page is possible", () => {
    const items = Array.from({ length: 20 }, (_, i) => ({
      id: `c${i}`,
      firstName: `Customer${i}`,
      lastName: "",
      email: `c${i}@x.com`,
      mobile: null,
      isActive: true,
      totalBookings: 0,
      createdAt: "2026-01-01T00:00:00Z",
    }));
    vi.mocked(useCustomers).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items, totalCount: 40, pageSize: 20 },
    } as unknown as ReturnType<typeof useCustomers>);
    render(<CustomersPage />, { wrapper: createWrapper() });
    const next = screen.getByRole("button", { name: /^next$/i });
    expect(next).not.toBeDisabled();
    act(() => {
      fireEvent.click(next);
    });
    expect(
      screen.getByRole("button", { name: /previous/i }),
    ).not.toBeDisabled();
  });
});

// ─── RecurringPage ────────────────────────────────────────────────────────────

describe("RecurringPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useCancelRecurringSeries).mockReturnValue(
      mockMutation as unknown as ReturnType<typeof useCancelRecurringSeries>,
    );
  });

  it("shows loading state", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: true,
      isError: false,
      data: undefined,
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("shows error state", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: true,
      data: undefined,
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(
      screen.getByText(/Failed to load recurring bookings/i),
    ).toBeInTheDocument();
  });

  it("renders recurring rules when data is available", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "r1",
            bookingTypeName: "Haircut",
            customerFirstName: "Carol",
            customerLastName: "",
            frequency: "Weekly",
            status: "Active",
            nextOccurrenceAt: "2026-01-12T09:00:00Z",
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Haircut")).toBeInTheDocument();
    expect(screen.getByText("Carol")).toBeInTheDocument();
  });

  it("renders heading", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: undefined,
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Recurring Bookings")).toBeInTheDocument();
  });

  it("shows total count when data is present", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 5, pageSize: 20 },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Total: 5")).toBeInTheDocument();
  });

  it("shows Cancel Series button for active rules", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "r1",
            bookingTypeName: "Haircut",
            customerFirstName: "Carol",
            customerLastName: "",
            frequency: "Daily",
            status: "Active",
            nextOccurrenceAt: null,
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(
      screen.getByRole("button", { name: /cancel series/i }),
    ).toBeInTheDocument();
  });

  it("does not show Cancel Series button for inactive rules", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "r2",
            bookingTypeName: "Yoga",
            customerFirstName: "Eve",
            customerLastName: "",
            frequency: "Monthly",
            status: "Cancelled",
            nextOccurrenceAt: null,
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(
      screen.queryByRole("button", { name: /cancel series/i }),
    ).not.toBeInTheDocument();
  });

  it("opens confirmation dialog on Cancel Series click", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "r1",
            bookingTypeName: "Haircut",
            customerFirstName: "Carol",
            customerLastName: "",
            frequency: "Weekly",
            status: "Active",
            nextOccurrenceAt: null,
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /cancel series/i }));
    });
    expect(screen.getByText("Cancel recurring series?")).toBeInTheDocument();
  });

  it("shows pagination Previous disabled on page 1", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items: [], totalCount: 0, pageSize: 20 },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(screen.getByRole("button", { name: /previous/i })).toBeDisabled();
  });

  it("shows null nextOccurrenceAt as dash", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "r3",
            bookingTypeName: "Yoga",
            customerFirstName: "Eve",
            customerLastName: "",
            frequency: "Monthly",
            status: "Active",
            nextOccurrenceAt: null,
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("clicking Cancel Series action in dialog calls mutateAsync", () => {
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: {
        items: [
          {
            id: "r1",
            bookingTypeName: "Haircut",
            customerFirstName: "Carol",
            customerLastName: "",
            frequency: "Weekly",
            status: "Active",
            nextOccurrenceAt: null,
          },
        ],
        totalCount: 1,
        pageSize: 20,
      },
    } as unknown as ReturnType<typeof useRecurringRules>);
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    vi.mocked(useCancelRecurringSeries).mockReturnValue({
      ...mockMutation,
      mutateAsync,
    } as unknown as ReturnType<typeof useCancelRecurringSeries>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    // Open the dialog
    act(() => {
      fireEvent.click(screen.getByRole("button", { name: /cancel series/i }));
    });
    expect(screen.getByText("Cancel recurring series?")).toBeInTheDocument();
    // Click the "Cancel Series" action inside the dialog (the AlertDialogAction)
    const cancelSeriesButtons = screen.getAllByRole("button", {
      name: /cancel series/i,
    });
    act(() => {
      fireEvent.click(cancelSeriesButtons[cancelSeriesButtons.length - 1]);
    });
    expect(mutateAsync).toHaveBeenCalledWith("r1");
  });

  it("clicking Next on recurring page when full page is possible", () => {
    const items = Array.from({ length: 20 }, (_, i) => ({
      id: `r${i}`,
      bookingTypeName: "Yoga",
      customerFirstName: `Person${i}`,
      customerLastName: "",
      frequency: "Weekly",
      status: "Active",
      nextOccurrenceAt: null,
    }));
    vi.mocked(useRecurringRules).mockReturnValue({
      isLoading: false,
      isError: false,
      data: { items, totalCount: 40, pageSize: 20 },
    } as unknown as ReturnType<typeof useRecurringRules>);
    render(<RecurringPage />, { wrapper: createWrapper() });
    const next = screen.getByRole("button", { name: /^next$/i });
    expect(next).not.toBeDisabled();
    act(() => {
      fireEvent.click(next);
    });
    expect(
      screen.getByRole("button", { name: /previous/i }),
    ).not.toBeDisabled();
  });
});

import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import CustomersPage from "./page";

vi.mock("@/hooks/use-customers", () => ({
  useCustomers: vi.fn(() => ({
    data: {
      items: [
        {
          id: "cust-1",
          firstName: "Alice",
          lastName: "",
          email: "alice@example.com",
          authProvider: "BuiltIn",
          bookingCount: 5,
          isActive: true,
          createdAt: "2026-01-01T00:00:00Z",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    },
    isLoading: false,
  })),
  useDeactivateCustomer: vi.fn(() => ({ mutateAsync: vi.fn() })),
}));

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>
    {children}
  </QueryClientProvider>
);

describe("CustomersPage", () => {
  it("renders customer list", () => {
    render(<CustomersPage />, { wrapper });
    expect(screen.getByText("Alice")).toBeInTheDocument();
    expect(screen.getByText("alice@example.com")).toBeInTheDocument();
  });

  it("shows auth provider badge", () => {
    render(<CustomersPage />, { wrapper });
    expect(screen.getByText("BuiltIn")).toBeInTheDocument();
  });
});

import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import RecurringPage from "./page";

vi.mock("@/hooks/use-recurring", () => ({
  useRecurringRules: vi.fn(() => ({
    data: {
      items: [
        {
          id: "rule-1",
          customerFirstName: "Bob",
          customerLastName: "",
          bookingTypeName: "Haircut",
          frequency: "Weekly",
          nextOccurrenceAt: "2026-03-15T10:00:00Z",
          status: "Active",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
    },
    isLoading: false,
  })),
  useCancelRecurringSeries: vi.fn(() => ({ mutateAsync: vi.fn() })),
}));

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>
    {children}
  </QueryClientProvider>
);

describe("RecurringPage", () => {
  it("renders recurring rules", () => {
    render(<RecurringPage />, { wrapper });
    expect(screen.getByText("Bob")).toBeInTheDocument();
    expect(screen.getByText("Haircut")).toBeInTheDocument();
    expect(screen.getByText("Weekly")).toBeInTheDocument();
  });
});

import { render, screen, fireEvent, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createElement } from "react";

// Mock recharts to avoid SVG rendering issues in jsdom
vi.mock("recharts", () => ({
  LineChart: ({ children }: { children?: React.ReactNode }) => <div data-testid="line-chart">{children}</div>,
  BarChart: ({ children }: { children?: React.ReactNode }) => <div data-testid="bar-chart">{children}</div>,
  PieChart: ({ children }: { children?: React.ReactNode }) => <div data-testid="pie-chart">{children}</div>,
  Line: () => null,
  Bar: () => null,
  Pie: () => null,
  Cell: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  ResponsiveContainer: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  Legend: () => null,
}));

vi.mock("@/hooks/use-analytics", () => ({
  useBookingAnalytics: vi.fn(),
  useRevenueAnalytics: vi.fn(),
  useUtilizationAnalytics: vi.fn(),
}));

vi.mock("@/hooks/use-notifications", () => ({
  useNotificationConfigs: vi.fn(),
  useUpdateNotificationConfig: vi.fn(),
  useDisableNotificationChannel: vi.fn(),
}));

vi.mock("@/hooks/use-notification-templates", () => ({
  useNotificationTemplates: vi.fn(),
  useUpdateNotificationTemplate: vi.fn(),
  useResetNotificationTemplate: vi.fn(),
  usePreviewNotificationTemplate: vi.fn(),
}));

import {
  useBookingAnalytics,
  useRevenueAnalytics,
  useUtilizationAnalytics,
} from "@/hooks/use-analytics";
import {
  useNotificationConfigs,
  useUpdateNotificationConfig,
  useDisableNotificationChannel,
} from "@/hooks/use-notifications";
import {
  useNotificationTemplates,
  useUpdateNotificationTemplate,
  useResetNotificationTemplate,
  usePreviewNotificationTemplate,
} from "@/hooks/use-notification-templates";
import AnalyticsPage from "@/app/(dashboard)/analytics/page";
import NotificationsPage from "@/app/(dashboard)/notifications/page";

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

// ─── AnalyticsPage ────────────────────────────────────────────────────────────

describe("AnalyticsPage", () => {
  beforeEach(() => vi.clearAllMocks());

  const mockBookingData = {
    isLoading: false,
    isError: false,
    data: {
      totalBookings: 120,
      confirmedRate: 0.83,
      cancellationRate: 0.17,
      averagePerDay: 4.0,
      overTime: [],
      byStatus: [],
      byBookingType: [],
    },
  };
  const mockRevenueData = {
    isLoading: false,
    isError: false,
    data: {
      totalRevenueCentavos: 100000,
      averageBookingValueCentavos: 833,
      overTime: [],
      byBookingType: [],
    },
  };
  const mockUtilizationData = {
    isLoading: false,
    isError: false,
    data: {
      averageUtilizationRate: 0.72,
      peakHour: 10,
      peakDay: "Monday",
      byHour: [],
      byBookingType: [],
    },
  };

  it("renders heading", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue(mockRevenueData as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Analytics")).toBeInTheDocument();
  });

  it("shows loading state", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue({ isLoading: true, isError: false, data: undefined } as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue({ isLoading: true, isError: false, data: undefined } as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue({ isLoading: true, isError: false, data: undefined } as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    // BookingsTab shows loading indicator
    expect(document.body.textContent).toContain("Loading");
  });

  it("renders KPI cards when booking data is available", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue(mockRevenueData as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Total Bookings")).toBeInTheDocument();
  });

  it("renders tab navigation", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue(mockRevenueData as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Bookings")).toBeInTheDocument();
    expect(screen.getByText("Revenue")).toBeInTheDocument();
    expect(screen.getByText("Utilization")).toBeInTheDocument();
  });

  it("switches to Revenue tab on click", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue(mockRevenueData as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    act(() => { fireEvent.click(screen.getByRole("tab", { name: "Revenue" })); });
    expect(screen.getByText("Total Revenue")).toBeInTheDocument();
  });

  it("shows Revenue loading when revenue isLoading", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue({ isLoading: true, data: undefined, error: null } as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    act(() => { fireEvent.click(screen.getByRole("tab", { name: "Revenue" })); });
    expect(document.body.textContent).toContain("Loading");
  });

  it("shows Revenue error when revenue errors", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue({ isLoading: false, data: undefined, error: new Error("fail") } as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    act(() => { fireEvent.click(screen.getByRole("tab", { name: "Revenue" })); });
    expect(screen.getByText(/Failed to load analytics/i)).toBeInTheDocument();
  });

  it("switches to Utilization tab on click", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue(mockBookingData as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue(mockRevenueData as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue({
      isLoading: false,
      error: null,
      data: {
        overallUtilizationRate: 0.72,
        busiestDayOfWeek: "Monday",
        busiestTimeSlot: "10:00",
        byBookingType: [],
        byStaffMember: [],
      },
    } as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    act(() => { fireEvent.click(screen.getByRole("tab", { name: "Utilization" })); });
    expect(screen.getByText("Overall Utilization")).toBeInTheDocument();
  });

  it("shows Bookings error when booking errors", () => {
    vi.mocked(useBookingAnalytics).mockReturnValue({ isLoading: false, data: undefined, error: new Error("err") } as unknown as ReturnType<typeof useBookingAnalytics>);
    vi.mocked(useRevenueAnalytics).mockReturnValue(mockRevenueData as unknown as ReturnType<typeof useRevenueAnalytics>);
    vi.mocked(useUtilizationAnalytics).mockReturnValue(mockUtilizationData as unknown as ReturnType<typeof useUtilizationAnalytics>);
    render(<AnalyticsPage />, { wrapper: createWrapper() });
    expect(screen.getByText(/Failed to load analytics/i)).toBeInTheDocument();
  });
});

// ─── NotificationsPage ────────────────────────────────────────────────────────

describe("NotificationsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useUpdateNotificationConfig).mockReturnValue(mockMutation as unknown as ReturnType<typeof useUpdateNotificationConfig>);
    vi.mocked(useDisableNotificationChannel).mockReturnValue(mockMutation as unknown as ReturnType<typeof useDisableNotificationChannel>);
    vi.mocked(useUpdateNotificationTemplate).mockReturnValue(mockMutation as unknown as ReturnType<typeof useUpdateNotificationTemplate>);
    vi.mocked(useResetNotificationTemplate).mockReturnValue(mockMutation as unknown as ReturnType<typeof useResetNotificationTemplate>);
    vi.mocked(usePreviewNotificationTemplate).mockReturnValue(mockMutation as unknown as ReturnType<typeof usePreviewNotificationTemplate>);
  });

  it("renders heading", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Notifications")).toBeInTheDocument();
  });

  it("shows loading state for configs", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: true, isError: false, data: undefined } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("renders notification channels tab", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Channels")).toBeInTheDocument();
  });

  it("renders notification configs when present", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "nc1",
          channelType: "Email",
          isEnabled: true,
          settings: {},
        },
      ],
    } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Email")).toBeInTheDocument();
  });

  it("shows Configured badge for enabled channel", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "nc1",
          channelType: "Email",
          isEnabled: true,
          settings: "{}",
        },
      ],
    } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Configured")).toBeInTheDocument();
  });

  it("shows Not configured badge for disabled channel", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "nc2",
          channelType: "Sms",
          isEnabled: false,
          settings: "{}",
        },
      ],
    } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    expect(screen.getByText("Not configured")).toBeInTheDocument();
  });

  it("renders templates tab content", async () => {
    const user = userEvent.setup();
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "t1",
          eventType: "BookingConfirmed",
          channelType: "Email",
          subject: "Your booking is confirmed",
          body: "Hello {{name}}",
          isActive: true,
        },
      ],
    } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    // Click Templates tab using userEvent for Radix UI
    await user.click(screen.getByRole("tab", { name: "Templates" }));
    expect(screen.getByText("BookingConfirmed")).toBeInTheDocument();
  });

  it("shows loading for templates when templatesLoading", async () => {
    const user = userEvent.setup();
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: true, isError: false, data: undefined } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    // Click Templates tab to reveal loading state
    await user.click(screen.getByRole("tab", { name: "Templates" }));
    expect(screen.getByText("Loading...")).toBeInTheDocument();
  });

  it("opens channel configure dialog on Configure click", () => {
    vi.mocked(useNotificationConfigs).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "nc1",
          channelType: "Email",
          isEnabled: true,
          settings: "{}",
        },
      ],
    } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    act(() => { fireEvent.click(screen.getByRole("button", { name: /configure/i })); });
    expect(screen.getByText(/Settings \(JSON\)/i)).toBeInTheDocument();
  });

  it("ChannelConfigForm Save button calls update mutateAsync", async () => {
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    vi.mocked(useUpdateNotificationConfig).mockReturnValue({ ...mockMutation, mutateAsync } as unknown as ReturnType<typeof useUpdateNotificationConfig>);
    vi.mocked(useNotificationConfigs).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "nc1",
          channelType: "Email",
          isEnabled: true,
          settings: '{"host":"smtp.example.com"}',
        },
      ],
    } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    // Open configure dialog
    act(() => { fireEvent.click(screen.getByRole("button", { name: /configure/i })); });
    // Click Save
    act(() => { fireEvent.click(screen.getByRole("button", { name: /^save$/i })); });
    expect(mutateAsync).toHaveBeenCalled();
  });

  it("ChannelCard toggle disable calls disable mutateAsync when turning off", async () => {
    const disableMutateAsync = vi.fn().mockResolvedValue(undefined);
    vi.mocked(useDisableNotificationChannel).mockReturnValue({ ...mockMutation, mutateAsync: disableMutateAsync } as unknown as ReturnType<typeof useDisableNotificationChannel>);
    vi.mocked(useNotificationConfigs).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "nc1",
          channelType: "Email",
          isEnabled: true,
          settings: "{}",
        },
      ],
    } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    // The Switch is a button with role="switch"
    const switchEl = screen.getByRole("switch");
    act(() => { fireEvent.click(switchEl); });
    expect(disableMutateAsync).toHaveBeenCalledWith("Email");
  });

  it("TemplateEditor opens edit dialog on Edit click", async () => {
    const user = userEvent.setup();
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "t1",
          eventType: "BookingConfirmed",
          channelType: "Email",
          subject: "Your booking is confirmed",
          body: "Hello {{name}}",
          isActive: true,
        },
      ],
    } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    // Navigate to Templates tab
    await user.click(screen.getByRole("tab", { name: "Templates" }));
    // Click Edit button
    act(() => { fireEvent.click(screen.getByRole("button", { name: /^edit$/i })); });
    expect(screen.getByText(/BookingConfirmed — Email/i)).toBeInTheDocument();
  });

  it("TemplateEditor handleSave calls update mutateAsync", async () => {
    const user = userEvent.setup();
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    vi.mocked(useUpdateNotificationTemplate).mockReturnValue({ ...mockMutation, mutateAsync } as unknown as ReturnType<typeof useUpdateNotificationTemplate>);
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "t1",
          eventType: "BookingConfirmed",
          channelType: "Email",
          subject: "Confirmed",
          body: "Hello",
          isActive: true,
        },
      ],
    } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    await user.click(screen.getByRole("tab", { name: "Templates" }));
    act(() => { fireEvent.click(screen.getByRole("button", { name: /^edit$/i })); });
    // Click Save inside the dialog
    act(() => { fireEvent.click(screen.getByRole("button", { name: /^save$/i })); });
    expect(mutateAsync).toHaveBeenCalled();
  });

  it("TemplateEditor handlePreview calls preview mutateAsync", async () => {
    const user = userEvent.setup();
    const mutateAsync = vi.fn().mockResolvedValue({ body: "Rendered preview" });
    vi.mocked(usePreviewNotificationTemplate).mockReturnValue({ ...mockMutation, mutateAsync } as unknown as ReturnType<typeof usePreviewNotificationTemplate>);
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "t1",
          eventType: "BookingConfirmed",
          channelType: "Email",
          subject: "Confirmed",
          body: "Hello",
          isActive: true,
        },
      ],
    } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    await user.click(screen.getByRole("tab", { name: "Templates" }));
    act(() => { fireEvent.click(screen.getByRole("button", { name: /^edit$/i })); });
    act(() => { fireEvent.click(screen.getByRole("button", { name: /preview/i })); });
    expect(mutateAsync).toHaveBeenCalledWith({ id: "t1", sampleData: {} });
  });

  it("TemplateEditor handleReset calls reset mutateAsync", async () => {
    const user = userEvent.setup();
    const mutateAsync = vi.fn().mockResolvedValue(undefined);
    vi.mocked(useResetNotificationTemplate).mockReturnValue({ ...mockMutation, mutateAsync } as unknown as ReturnType<typeof useResetNotificationTemplate>);
    vi.mocked(useNotificationConfigs).mockReturnValue({ isLoading: false, isError: false, data: [] } as unknown as ReturnType<typeof useNotificationConfigs>);
    vi.mocked(useNotificationTemplates).mockReturnValue({
      isLoading: false,
      isError: false,
      data: [
        {
          id: "t1",
          eventType: "BookingConfirmed",
          channelType: "Email",
          subject: "Confirmed",
          body: "Hello",
          isActive: true,
        },
      ],
    } as unknown as ReturnType<typeof useNotificationTemplates>);
    render(<NotificationsPage />, { wrapper: createWrapper() });
    await user.click(screen.getByRole("tab", { name: "Templates" }));
    act(() => { fireEvent.click(screen.getByRole("button", { name: /^edit$/i })); });
    act(() => { fireEvent.click(screen.getByRole("button", { name: /reset to default/i })); });
    expect(mutateAsync).toHaveBeenCalledWith("BookingConfirmed");
  });
});

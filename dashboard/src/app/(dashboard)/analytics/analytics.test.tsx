import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import AnalyticsPage from './page'

vi.mock('@/hooks/use-analytics', () => ({
  useBookingAnalytics: vi.fn(() => ({
    data: {
      totalBookings: 150,
      confirmedRate: 0.82,
      cancellationRate: 0.12,
      averagePerDay: 5.0,
      byStatus: [
        { status: 'Confirmed', count: 123 },
        { status: 'Cancelled', count: 18 },
        { status: 'PendingPayment', count: 9 },
      ],
      overTime: [
        { date: '2026-03-01', count: 5 },
        { date: '2026-03-02', count: 7 },
      ],
      byBookingType: [{ name: 'Haircut', count: 80 }],
      byStaffMember: [{ name: 'Alice', count: 60 }],
    },
    isLoading: false,
    error: null,
  })),
  useRevenueAnalytics: vi.fn(() => ({
    data: {
      totalRevenueCentavos: 75000,
      averageBookingValueCentavos: 500,
      overTime: [{ date: '2026-03-01', amountCentavos: 2500 }],
      byBookingType: [{ name: 'Haircut', amountCentavos: 40000 }],
      byStaffMember: [{ name: 'Alice', amountCentavos: 37500 }],
    },
    isLoading: false,
    error: null,
  })),
  useUtilizationAnalytics: vi.fn(() => ({
    data: {
      overallUtilizationRate: 0.73,
      busiestDayOfWeek: 'Friday',
      busiestTimeSlot: '10:00',
      byBookingType: [{ name: 'Haircut', fillRate: 0.8 }],
      byStaffMember: [{ name: 'Alice', utilizationRate: 0.75 }],
    },
    isLoading: false,
    error: null,
  })),
}))

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>
    {children}
  </QueryClientProvider>
)

describe('AnalyticsPage', () => {
  it('renders the Bookings tab by default', async () => {
    render(<AnalyticsPage />, { wrapper })
    expect(screen.getByText('150')).toBeInTheDocument()
    expect(screen.getByText('Bookings')).toBeInTheDocument()
  })

  it('renders KPI cards for bookings', async () => {
    render(<AnalyticsPage />, { wrapper })
    expect(screen.getByText('Total Bookings')).toBeInTheDocument()
    expect(screen.getByText('Confirmed Rate')).toBeInTheDocument()
    expect(screen.getByText('Cancellation Rate')).toBeInTheDocument()
  })

  it('switches to Revenue tab', async () => {
    render(<AnalyticsPage />, { wrapper })
    await userEvent.click(screen.getByRole('tab', { name: 'Revenue' }))
    expect(screen.getByText('Total Revenue')).toBeInTheDocument()
  })

  it('switches to Utilization tab', async () => {
    render(<AnalyticsPage />, { wrapper })
    await userEvent.click(screen.getByRole('tab', { name: 'Utilization' }))
    expect(screen.getByText('Overall Utilization')).toBeInTheDocument()
  })
})

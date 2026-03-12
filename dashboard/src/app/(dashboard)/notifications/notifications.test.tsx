import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import NotificationsPage from './page'

vi.mock('@/hooks/use-notifications', () => ({
  useNotificationConfigs: vi.fn(() => ({
    data: [
      { id: '1', channelType: 'Email', isEnabled: true, settings: '{}' },
      { id: '2', channelType: 'Sms', isEnabled: false, settings: '{}' },
      { id: '3', channelType: 'Push', isEnabled: false, settings: '{}' },
    ],
    isLoading: false,
  })),
  useUpdateNotificationConfig: vi.fn(() => ({ mutateAsync: vi.fn() })),
  useDisableNotificationChannel: vi.fn(() => ({ mutateAsync: vi.fn() })),
}))

vi.mock('@/hooks/use-notification-templates', () => ({
  useNotificationTemplates: vi.fn(() => ({
    data: [
      {
        id: 'tmpl-1',
        tenantId: 'tenant-1',
        eventType: 'BookingConfirmed',
        channelType: 'Email',
        subject: 'Booking Confirmed',
        body: 'Your booking {{bookingId}} is confirmed.',
        isActive: true,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      },
    ],
    isLoading: false,
  })),
  useUpdateNotificationTemplate: vi.fn(() => ({ mutateAsync: vi.fn() })),
  useResetNotificationTemplate: vi.fn(() => ({ mutateAsync: vi.fn() })),
  usePreviewNotificationTemplate: vi.fn(() => ({ mutateAsync: vi.fn() })),
}))

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('NotificationsPage', () => {
  it('renders Channels tab by default', () => {
    render(<NotificationsPage />, { wrapper })
    expect(screen.getByText('Email')).toBeInTheDocument()
    expect(screen.getByText('SMS')).toBeInTheDocument()
    expect(screen.getByText('Push')).toBeInTheDocument()
  })

  it('shows email channel as enabled', () => {
    render(<NotificationsPage />, { wrapper })
    expect(screen.getAllByText('Configured').length).toBeGreaterThan(0)
  })

  it('switches to Templates tab', async () => {
    render(<NotificationsPage />, { wrapper })
    await userEvent.click(screen.getByRole('tab', { name: 'Templates' }))
    expect(screen.getByText('BookingConfirmed')).toBeInTheDocument()
  })
})

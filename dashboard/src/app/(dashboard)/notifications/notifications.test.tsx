import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import NotificationsPage from './page'

vi.mock('@/hooks/use-notifications', () => ({
  useNotificationConfigs: vi.fn(() => ({
    data: [
      { id: '1', channel: 'Email', isEnabled: true, fromEmail: 'noreply@example.com' },
      { id: '2', channel: 'Sms', isEnabled: false },
      { id: '3', channel: 'Push', isEnabled: false },
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
        eventType: 'BookingConfirmed',
        channel: 'Email',
        subjectTemplate: 'Booking Confirmed',
        bodyTemplate: 'Your booking {{bookingId}} is confirmed.',
        variables: ['bookingId', 'customerName'],
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
    expect(screen.getByText('noreply@example.com')).toBeInTheDocument()
  })

  it('switches to Templates tab', async () => {
    render(<NotificationsPage />, { wrapper })
    await userEvent.click(screen.getByRole('tab', { name: 'Templates' }))
    expect(screen.getByText('BookingConfirmed')).toBeInTheDocument()
  })
})

import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { WebhooksSection } from './webhooks-section'

vi.mock('@/hooks/use-webhooks', () => ({
  useWebhooks: vi.fn(() => ({
    data: [
      {
        id: 'wh-1',
        url: 'https://example.com/hook',
        events: ['booking.confirmed', 'booking.cancelled'],
        isActive: true,
        lastDeliveryStatus: 'Success',
      },
    ],
    isLoading: false,
  })),
  useCreateWebhook: vi.fn(() => ({ mutateAsync: vi.fn() })),
  useDeleteWebhook: vi.fn(() => ({ mutateAsync: vi.fn() })),
  useWebhookDeliveries: vi.fn(() => ({
    data: [],
    isLoading: false,
  })),
  useRetryWebhookDelivery: vi.fn(() => ({ mutateAsync: vi.fn() })),
  useTestWebhook: vi.fn(() => ({ mutateAsync: vi.fn() })),
}))

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('WebhooksSection', () => {
  it('renders existing webhooks', () => {
    render(<WebhooksSection bookingTypeSlug="haircut" />, { wrapper })
    expect(screen.getByText('https://example.com/hook')).toBeInTheDocument()
  })

  it('shows event badges', () => {
    render(<WebhooksSection bookingTypeSlug="haircut" />, { wrapper })
    expect(screen.getByText('booking.confirmed')).toBeInTheDocument()
  })

  it('shows create webhook button', () => {
    render(<WebhooksSection bookingTypeSlug="haircut" />, { wrapper })
    expect(screen.getByRole('button', { name: /add webhook/i })).toBeInTheDocument()
  })
})

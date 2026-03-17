import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import AuditPage from './page'

vi.mock('@/hooks/use-audit', () => ({
  useAuditEntry: vi.fn(() => ({ data: undefined })),
  useAuditEntries: vi.fn(() => ({
    data: {
      items: [
        {
          id: 'audit-1',
          timestamp: '2026-03-01T10:00:00Z',
          userId: 'user-1',
          userName: 'Admin User',
          userRole: 'TenantAdmin',
          entityType: 'Booking',
          entityId: 'booking-1',
          action: 'Created',
          summary: 'Booking created',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 50,
    },
    isLoading: false,
  })),
}))

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

describe('AuditPage', () => {
  it('renders audit entries', () => {
    render(<AuditPage />, { wrapper })
    expect(screen.getByText('Admin User')).toBeInTheDocument()
    expect(screen.getByText('Booking')).toBeInTheDocument()
    expect(screen.getByText('Created')).toBeInTheDocument()
  })
})

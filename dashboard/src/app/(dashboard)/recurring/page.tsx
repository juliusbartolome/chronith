'use client'

import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useRecurringRules, useCancelRecurringSeries } from '@/hooks/use-recurring'

const FREQ_COLORS: Record<string, 'default' | 'secondary' | 'outline'> = {
  Daily: 'default',
  Weekly: 'secondary',
  Monthly: 'outline',
}

export default function RecurringPage() {
  const { data, isLoading } = useRecurringRules()
  const cancel = useCancelRecurringSeries()

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Recurring Bookings</h1>
        <p className="text-muted-foreground">
          Manage recurring booking series.
        </p>
      </div>
      {isLoading && <p>Loading...</p>}
      {data && (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Customer</TableHead>
                <TableHead>Booking Type</TableHead>
                <TableHead>Frequency</TableHead>
                <TableHead>Next Occurrence</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((rule) => (
                <TableRow key={rule.id}>
                  <TableCell className="font-medium">{rule.customerName}</TableCell>
                  <TableCell>{rule.bookingTypeName}</TableCell>
                  <TableCell>
                    <Badge variant={FREQ_COLORS[rule.frequency] ?? 'default'}>
                      {rule.frequency}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {rule.nextOccurrenceAt
                      ? new Date(rule.nextOccurrenceAt).toLocaleString()
                      : '—'}
                  </TableCell>
                  <TableCell>
                    <Badge
                      variant={rule.status === 'Active' ? 'default' : 'secondary'}
                    >
                      {rule.status}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {rule.status === 'Active' && (
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => cancel.mutateAsync(rule.id)}
                      >
                        Cancel Series
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </div>
  )
}

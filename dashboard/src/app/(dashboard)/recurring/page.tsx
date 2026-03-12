'use client'

import { useState } from 'react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
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
  const [page, setPage] = useState(1)
  const [confirmId, setConfirmId] = useState<string | null>(null)
  const { data, isLoading, isError } = useRecurringRules({ page })
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
      {isError && <p className="text-sm text-red-600">Failed to load recurring bookings.</p>}
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
                        disabled={cancel.isPending}
                        onClick={() => setConfirmId(rule.id)}
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
      <div className="flex items-center justify-between">
        <p className="text-sm text-muted-foreground">
          Total: {data?.totalCount ?? 0}
        </p>
        <div className="flex gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
          >
            Previous
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => p + 1)}
            disabled={!data || data.items.length < data.pageSize}
          >
            Next
          </Button>
        </div>
      </div>
      {confirmId && (
        <AlertDialog open onOpenChange={() => setConfirmId(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Cancel recurring series?</AlertDialogTitle>
              <AlertDialogDescription>
                This will cancel all future occurrences in this series.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                onClick={() => {
                  cancel.mutateAsync(confirmId).finally(() => setConfirmId(null))
                }}
              >
                Cancel Series
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}
    </div>
  )
}

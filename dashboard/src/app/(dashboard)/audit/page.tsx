'use client'

import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useAuditEntries, useAuditEntry, type AuditEntryDto } from '@/hooks/use-audit'

function AuditDetailModal({
  entry,
  onClose,
}: {
  entry: AuditEntryDto
  onClose: () => void
}) {
  const { data: detail } = useAuditEntry(entry.id)

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Audit Entry — {entry.action}</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 text-sm">
          <div className="grid grid-cols-2 gap-2">
            <div>
              <p className="font-medium">User</p>
              <p>{entry.userName}</p>
            </div>
            <div>
              <p className="font-medium">Timestamp</p>
              <p>{new Date(entry.timestamp).toLocaleString()}</p>
            </div>
            <div>
              <p className="font-medium">Entity</p>
              <p>
                {entry.entityType} / {entry.entityId}
              </p>
            </div>
            <div>
              <p className="font-medium">IP Address</p>
              <p>{detail?.ipAddress ?? '—'}</p>
            </div>
          </div>
          {detail?.oldValues && (
            <div>
              <p className="font-medium">Old Values</p>
              <pre className="mt-1 rounded bg-muted p-2 text-xs">
                {JSON.stringify(detail.oldValues, null, 2)}
              </pre>
            </div>
          )}
          {detail?.newValues && (
            <div>
              <p className="font-medium">New Values</p>
              <pre className="mt-1 rounded bg-muted p-2 text-xs">
                {JSON.stringify(detail.newValues, null, 2)}
              </pre>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  )
}

export default function AuditPage() {
  const [selected, setSelected] = useState<AuditEntryDto | null>(null)
  const [page, setPage] = useState(1)
  const { data, isLoading, isError } = useAuditEntries({ page })

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Audit Log</h1>
        <p className="text-muted-foreground">
          Complete history of all system actions.
        </p>
      </div>
      {isLoading && <p>Loading...</p>}
      {isError && <p className="text-sm text-red-600">Failed to load audit entries.</p>}
      {data && (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Timestamp</TableHead>
                <TableHead>User</TableHead>
                <TableHead>Entity</TableHead>
                <TableHead>Action</TableHead>
                <TableHead>Summary</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((entry) => (
                <TableRow key={entry.id}>
                  <TableCell>
                    {new Date(entry.timestamp).toLocaleString()}
                  </TableCell>
                  <TableCell>
                    <div>
                      <p className="font-medium">{entry.userName}</p>
                      <Badge variant="outline" className="text-xs">
                        {entry.userRole}
                      </Badge>
                    </div>
                  </TableCell>
                  <TableCell>{entry.entityType}</TableCell>
                  <TableCell>
                    <Badge>{entry.action}</Badge>
                  </TableCell>
                  <TableCell className="max-w-xs truncate">
                    {entry.summary}
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelected(entry)}
                    >
                      View
                    </Button>
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
      {selected && (
        <AuditDetailModal entry={selected} onClose={() => setSelected(null)} />
      )}
    </div>
  )
}

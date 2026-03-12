'use client'

import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useCustomers, useDeactivateCustomer } from '@/hooks/use-customers'

export default function CustomersPage() {
  const [search, setSearch] = useState('')
  const { data, isLoading } = useCustomers({ search: search || undefined })
  const deactivate = useDeactivateCustomer()

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Customers</h1>
        <p className="text-muted-foreground">
          Manage your customers and their booking history.
        </p>
      </div>
      <Input
        placeholder="Search by name or email..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        className="max-w-sm"
      />
      {isLoading && <p>Loading...</p>}
      {data && (
        <div className="rounded-md border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Auth</TableHead>
                <TableHead>Bookings</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((c) => (
                <TableRow key={c.id}>
                  <TableCell className="font-medium">{c.name}</TableCell>
                  <TableCell>{c.email}</TableCell>
                  <TableCell>
                    <Badge variant="outline">{c.authProvider}</Badge>
                  </TableCell>
                  <TableCell>{c.bookingCount}</TableCell>
                  <TableCell>
                    <Badge variant={c.isActive ? 'default' : 'secondary'}>
                      {c.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {c.isActive && (
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => deactivate.mutateAsync(c.id)}
                      >
                        Deactivate
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

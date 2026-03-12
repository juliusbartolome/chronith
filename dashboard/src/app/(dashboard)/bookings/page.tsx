"use client";

import { useState } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { BookingStatusBadge } from "@/components/bookings/booking-status-badge";
import { ExportButton } from "@/components/shared/export-button";
import { useBookings } from "@/hooks/use-bookings";
import { format } from "date-fns";

const STATUSES = [
  "All",
  "PendingPayment",
  "PendingVerification",
  "Confirmed",
  "Cancelled",
];

export default function BookingsPage() {
  const [status, setStatus] = useState<string | undefined>();
  const [page, setPage] = useState(1);
  const { data, isLoading, isError } = useBookings({
    status,
    page,
    pageSize: 25,
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Bookings</h1>
        <div className="flex gap-2">
          <ExportButton exportUrl="/api/bookings/export" filename="bookings" />
          <Button asChild>
            <Link href="/bookings/new">Create Booking</Link>
          </Button>
        </div>
      </div>

      <div className="flex gap-3">
        <Select
          value={status ?? "All"}
          onValueChange={(v) => {
            setStatus(v === "All" ? undefined : v);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-52">
            <SelectValue placeholder="Filter by status" />
          </SelectTrigger>
          <SelectContent>
            {STATUSES.map((s) => (
              <SelectItem key={s} value={s}>
                {s === "All" ? "All Statuses" : s}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isLoading && <p className="text-sm text-zinc-500">Loading…</p>}
      {isError && (
        <p className="text-sm text-red-600">Failed to load bookings.</p>
      )}

      {data && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Customer</TableHead>
                <TableHead>Booking Type</TableHead>
                <TableHead>Start</TableHead>
                <TableHead>Status</TableHead>
                <TableHead>Staff</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-zinc-400">
                    No bookings found.
                  </TableCell>
                </TableRow>
              )}
              {data.items?.map((b: any) => (
                <TableRow key={b.id}>
                  <TableCell>{b.customerEmail}</TableCell>
                  <TableCell>{b.bookingTypeId}</TableCell>
                  <TableCell>{format(new Date(b.start), "PPp")}</TableCell>
                  <TableCell>
                    <BookingStatusBadge status={b.status} />
                  </TableCell>
                  <TableCell>{b.staffMemberId ?? "—"}</TableCell>
                  <TableCell>
                    <Button variant="ghost" size="sm" asChild>
                      <Link href={`/bookings/${b.id}`}>View</Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <div className="flex justify-end gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
            >
              Previous
            </Button>
            <span className="flex items-center text-sm text-zinc-500">
              Page {page}
            </span>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage((p) => p + 1)}
              disabled={!data.items || data.items.length < 25}
            >
              Next
            </Button>
          </div>
        </>
      )}
    </div>
  );
}

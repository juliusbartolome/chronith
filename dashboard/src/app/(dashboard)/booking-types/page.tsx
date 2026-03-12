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
import { Badge } from "@/components/ui/badge";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import {
  useBookingTypes,
  useDeleteBookingType,
} from "@/hooks/use-booking-types";

interface BookingTypeRow {
  id: string;
  name: string;
  slug: string;
  kind: string;
  requiresStaffAssignment: boolean;
}

export default function BookingTypesPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError } = useBookingTypes({ page, pageSize: 25 });
  const deleteType = useDeleteBookingType();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Booking Types</h1>
        <Button asChild>
          <Link href="/booking-types/new">Create Booking Type</Link>
        </Button>
      </div>

      {isLoading && <p className="text-sm text-zinc-500">Loading…</p>}
      {isError && (
        <p className="text-sm text-red-600">Failed to load booking types.</p>
      )}

      {data && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Slug</TableHead>
                <TableHead>Kind</TableHead>
                <TableHead>Staff Required</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} className="text-center text-zinc-400">
                    No booking types found.
                  </TableCell>
                </TableRow>
              )}
              {data.items?.map((bt: BookingTypeRow) => (
                <TableRow key={bt.id}>
                  <TableCell className="font-medium">{bt.name}</TableCell>
                  <TableCell className="font-mono text-sm text-zinc-500">
                    {bt.slug}
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">{bt.kind}</Badge>
                  </TableCell>
                  <TableCell>
                    {bt.requiresStaffAssignment ? "Yes" : "No"}
                  </TableCell>
                  <TableCell className="flex gap-2">
                    <Button variant="ghost" size="sm" asChild>
                      <Link href={`/booking-types/${bt.slug}`}>Edit</Link>
                    </Button>
                    <AlertDialog>
                      <AlertDialogTrigger asChild>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-red-600 hover:text-red-700"
                        >
                          Delete
                        </Button>
                      </AlertDialogTrigger>
                      <AlertDialogContent>
                        <AlertDialogHeader>
                          <AlertDialogTitle>
                            Delete booking type?
                          </AlertDialogTitle>
                          <AlertDialogDescription>
                            This will delete &ldquo;{bt.name}&rdquo;. This cannot be undone.
                          </AlertDialogDescription>
                        </AlertDialogHeader>
                        <AlertDialogFooter>
                          <AlertDialogCancel>Cancel</AlertDialogCancel>
                          <AlertDialogAction
                            onClick={() => deleteType.mutate(bt.slug)}
                          >
                            Delete
                          </AlertDialogAction>
                        </AlertDialogFooter>
                      </AlertDialogContent>
                    </AlertDialog>
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

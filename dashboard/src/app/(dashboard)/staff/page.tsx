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
import { useStaffList, useDeactivateStaff } from "@/hooks/use-staff";

interface StaffRow {
  id: string;
  name: string;
  email: string;
  isActive: boolean;
}

export default function StaffPage() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError } = useStaffList({ page, pageSize: 25 });
  const deactivate = useDeactivateStaff();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-zinc-900">Staff</h1>
        <Button asChild>
          <Link href="/staff/new">Add Staff</Link>
        </Button>
      </div>

      {isLoading && <p className="text-sm text-zinc-500">Loading…</p>}
      {isError && <p className="text-sm text-red-600">Failed to load staff.</p>}

      {data && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items?.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} className="text-center text-zinc-400">
                    No staff members found.
                  </TableCell>
                </TableRow>
              )}
              {data.items?.map((s: StaffRow) => (
                <TableRow key={s.id}>
                  <TableCell className="font-medium">{s.name}</TableCell>
                  <TableCell>{s.email}</TableCell>
                  <TableCell>
                    <Badge variant={s.isActive ? "default" : "outline"}>
                      {s.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </TableCell>
                  <TableCell className="flex gap-2">
                    <Button variant="ghost" size="sm" asChild>
                      <Link href={`/staff/${s.id}`}>Edit</Link>
                    </Button>
                    {s.isActive && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-red-600 hover:text-red-700"
                        onClick={() => deactivate.mutate(s.id)}
                        disabled={deactivate.isPending}
                      >
                        Deactivate
                      </Button>
                    )}
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

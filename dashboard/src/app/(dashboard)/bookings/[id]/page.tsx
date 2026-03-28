"use client";

import { useParams, useRouter } from "next/navigation";
import {
  useBooking,
  useConfirmBooking,
  usePayBooking,
  useCancelBooking,
} from "@/hooks/use-bookings";
import { BookingStatusBadge } from "@/components/bookings/booking-status-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
import { format } from "date-fns";

interface StatusChange {
  id: string;
  changedAt: string;
  fromStatus: string;
  toStatus: string;
  changedByRole: string;
}

export default function BookingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data: booking, isLoading } = useBooking(id);
  const confirm = useConfirmBooking();
  const pay = usePayBooking();
  const cancel = useCancelBooking();

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;
  if (!booking)
    return <p className="text-sm text-red-600">Booking not found.</p>;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => router.back()}>
            ←
          </Button>
          <h1 className="text-2xl font-semibold">Booking</h1>
          <BookingStatusBadge status={booking.status} />
        </div>

        <div className="flex gap-2">
          {booking.status === "PendingPayment" && (
            <Button
              size="sm"
              variant="secondary"
              onClick={() => pay.mutate(id)}
              disabled={pay.isPending}
            >
              Mark as Paid
            </Button>
          )}
          {booking.status === "PendingVerification" && (
            <Button
              size="sm"
              onClick={() => confirm.mutate(id)}
              disabled={confirm.isPending}
            >
              Confirm
            </Button>
          )}
          {["PendingPayment", "PendingVerification", "Confirmed"].includes(
            booking.status,
          ) && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="destructive" size="sm">
                  Cancel
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Cancel booking?</AlertDialogTitle>
                  <AlertDialogDescription>
                    This action cannot be undone. The booking will be cancelled.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Keep</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={() =>
                      cancel.mutate({ id, reason: "Cancelled by admin" })
                    }
                  >
                    Cancel Booking
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Customer</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>{booking.customerEmail}</p>
            <p className="text-zinc-500">{booking.customerId}</p>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Booking Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-1 text-sm">
            <p>Start: {format(new Date(booking.start), "PPp")}</p>
            <p>End: {format(new Date(booking.end), "PPp")}</p>
            <p>
              Amount: ₱{(booking.amountInCentavos / 100).toFixed(2)}{" "}
              {booking.currency}
            </p>
          </CardContent>
        </Card>
      </div>

      {booking.statusChanges?.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm">Status History</CardTitle>
          </CardHeader>
          <CardContent>
            <ol className="space-y-2">
              {booking.statusChanges.map((sc: StatusChange) => (
                <li key={sc.id} className="flex items-center gap-3 text-sm">
                  <span className="text-zinc-400">
                    {format(new Date(sc.changedAt), "PPp")}
                  </span>
                  <span>
                    {sc.fromStatus} → {sc.toStatus}
                  </span>
                  <span className="text-zinc-500">{sc.changedByRole}</span>
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

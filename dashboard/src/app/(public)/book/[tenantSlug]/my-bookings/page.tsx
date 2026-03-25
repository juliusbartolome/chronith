"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { useCustomerMe, useCustomerBookings } from "@/hooks/use-customer-auth";
import { formatDate } from "@/lib/utils";

const STATUS_COLORS: Record<
  string,
  "default" | "secondary" | "outline" | "destructive"
> = {
  PendingPayment: "secondary",
  PendingVerification: "outline",
  Confirmed: "default",
  Cancelled: "destructive",
};

export default function MyBookingsPage() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const { data: customer, isLoading: customerLoading } = useCustomerMe();
  const { data: bookingsData, isLoading: bookingsLoading } =
    useCustomerBookings();

  if (customerLoading) {
    return (
      <div className="p-8">
        <Skeleton className="h-32 w-full" />
      </div>
    );
  }

  if (!customer) {
    return (
      <div className="p-8 text-center space-y-4">
        <p className="text-muted-foreground">Sign in to view your bookings.</p>
        <Button asChild>
          <Link
            href={`/book/${tenantSlug}/auth/login?returnTo=/book/${tenantSlug}/my-bookings`}
          >
            Sign in
          </Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="p-6 max-w-3xl mx-auto space-y-6">
      <div>
        <h1 className="text-2xl font-bold">My Bookings</h1>
        <p className="text-muted-foreground text-sm">
          Welcome, {customer.firstName} {customer.lastName}
        </p>
      </div>

      {bookingsLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-16 w-full" />
          ))}
        </div>
      ) : bookingsData?.items.length === 0 ? (
        <p className="text-muted-foreground">No bookings yet.</p>
      ) : (
        <div className="divide-y">
          {bookingsData?.items.map((booking) => (
            <div
              key={booking.id}
              className="py-4 flex items-center justify-between"
            >
              <div>
                <div className="font-medium">{booking.bookingTypeName}</div>
                <div className="text-sm text-muted-foreground">
                  {formatDate(booking.startTime)}
                  {booking.staffName && ` · ${booking.staffName}`}
                </div>
              </div>
              <div className="flex items-center gap-3">
                <Badge variant={STATUS_COLORS[booking.status] ?? "secondary"}>
                  {booking.status}
                </Badge>
                <Button variant="ghost" size="sm" asChild>
                  <Link href={`/book/${tenantSlug}/my-bookings/${booking.id}`}>
                    View
                  </Link>
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

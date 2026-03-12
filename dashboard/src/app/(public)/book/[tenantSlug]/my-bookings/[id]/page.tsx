"use client";

import { useParams } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { useQuery } from "@tanstack/react-query";

type BookingDetailDto = {
  id: string;
  bookingTypeName: string;
  startTime: string;
  endTime: string;
  status: string;
  staffName: string | null;
  customerName: string;
  customerEmail: string;
  priceCentavos: number;
  notes: string | null;
};

function useBookingDetail(id: string) {
  return useQuery<BookingDetailDto>({
    queryKey: ["customer-booking", id],
    queryFn: async () => {
      const res = await fetch(`/api/public/auth/customer/bookings/${id}`);
      if (!res.ok) throw new Error("Failed to fetch booking");
      return res.json();
    },
  });
}

export default function BookingDetailPage() {
  const { id } = useParams<{ tenantSlug: string; id: string }>();
  const { data: booking, isLoading } = useBookingDetail(id);

  if (isLoading) {
    return (
      <div className="p-8">
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!booking) {
    return <div className="p-8 text-muted-foreground">Booking not found.</div>;
  }

  return (
    <div className="p-6 max-w-2xl mx-auto space-y-6">
      <div className="flex items-start justify-between">
        <h1 className="text-2xl font-bold">{booking.bookingTypeName}</h1>
        <Badge>{booking.status}</Badge>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Booking Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Date &amp; Time</span>
            <span>{new Date(booking.startTime).toLocaleString()}</span>
          </div>
          {booking.staffName && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Staff</span>
              <span>{booking.staffName}</span>
            </div>
          )}
          <div className="flex justify-between">
            <span className="text-muted-foreground">Price</span>
            <span>
              {booking.priceCentavos === 0
                ? "Free"
                : `₱${(booking.priceCentavos / 100).toFixed(2)}`}
            </span>
          </div>
          {booking.notes && (
            <div className="flex justify-between">
              <span className="text-muted-foreground">Notes</span>
              <span>{booking.notes}</span>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

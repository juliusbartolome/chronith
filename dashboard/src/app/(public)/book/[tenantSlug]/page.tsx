"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { usePublicBookingTypes } from "@/hooks/use-public-booking";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";

function formatPrice(centavos: number): string {
  if (centavos === 0) return "Free";
  return `₱${(centavos / 100).toLocaleString("en-PH", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`;
}

export default function PublicBookingLandingPage() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const { data: bookingTypes, isLoading, isError } = usePublicBookingTypes(tenantSlug);

  if (isLoading) {
    return (
      <div className="max-w-3xl mx-auto px-4 py-10 space-y-4">
        <Skeleton className="h-8 w-64" />
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-28 w-full" />
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="max-w-3xl mx-auto px-4 py-10">
        <p className="text-red-600 text-sm">Failed to load booking types. Please try again.</p>
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto px-4 py-10">
      <h1 className="text-2xl font-bold text-zinc-900 mb-6">
        Book an Appointment
      </h1>

      {bookingTypes && bookingTypes.length === 0 && (
        <p className="text-zinc-500 text-sm">
          No booking types are currently available.
        </p>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        {bookingTypes?.map((bt) => (
          <Link
            key={bt.slug}
            href={`/book/${tenantSlug}/${bt.slug}`}
            className="block"
          >
            <Card className="h-full hover:shadow-md transition-shadow cursor-pointer">
              <CardHeader className="pb-2">
                <CardTitle className="text-base">{bt.name}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {bt.description && (
                  <p className="text-sm text-zinc-600 line-clamp-2">
                    {bt.description}
                  </p>
                )}
                <div className="flex gap-2 flex-wrap">
                  <Badge variant="outline">{bt.durationMinutes} min</Badge>
                  <Badge variant="outline">{formatPrice(bt.priceCentavos)}</Badge>
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}

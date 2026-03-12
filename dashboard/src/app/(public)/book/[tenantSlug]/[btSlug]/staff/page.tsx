"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useBookingSession } from "@/lib/booking-session";
import { usePublicAvailability } from "@/hooks/use-public-booking";
import { Card, CardContent } from "@/components/ui/card";

export default function StaffSelectionPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();

  const session = useBookingSession();
  const setStaff = useBookingSession((s) => s.setStaff);

  // Guard: if booking type doesn't require staff, skip this step
  useEffect(() => {
    if (session.btSlug && !session.requiresStaffAssignment) {
      router.replace(`/book/${tenantSlug}/${btSlug}/details`);
    }
  }, [session.btSlug, session.requiresStaffAssignment, router, tenantSlug, btSlug]);

  // Guard: no session (direct navigation)
  useEffect(() => {
    if (!session.selectedDate) {
      router.replace(`/book/${tenantSlug}/${btSlug}`);
    }
  }, [session.selectedDate, router, tenantSlug, btSlug]);

  const { data: availability } = usePublicAvailability(
    tenantSlug,
    btSlug,
    session.selectedDate ?? "",
  );

  const handleStaffSelect = (staffId: string, staffName: string) => {
    setStaff(staffId, staffName);
    router.push(`/book/${tenantSlug}/${btSlug}/details`);
  };

  const staffList = availability?.staffAvailability ?? [];

  return (
    <div className="max-w-3xl mx-auto px-4 py-10">
      <p className="text-xs text-zinc-500 mb-6">Step 2 of 5 — Choose a Staff Member</p>
      <h1 className="text-xl font-bold text-zinc-900 mb-6">Select Staff</h1>

      {staffList.length === 0 && (
        <p className="text-sm text-zinc-500">No staff available for this time slot.</p>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        {staffList.map((s) => (
          <button
            key={s.staffId}
            type="button"
            onClick={() => handleStaffSelect(s.staffId, s.staffName)}
            className="text-left"
          >
            <Card className="hover:shadow-md transition-shadow cursor-pointer">
              <CardContent className="pt-4 space-y-1">
                {/* Avatar placeholder */}
                <div className="h-10 w-10 rounded-full bg-zinc-200 flex items-center justify-center text-sm font-bold text-zinc-500 mb-2">
                  {s.staffName.charAt(0).toUpperCase()}
                </div>
                <p className="font-medium text-zinc-900">{s.staffName}</p>
                <p className="text-xs text-zinc-500">
                  {s.slots.length} slot{s.slots.length !== 1 ? "s" : ""} available
                </p>
              </CardContent>
            </Card>
          </button>
        ))}
      </div>
    </div>
  );
}

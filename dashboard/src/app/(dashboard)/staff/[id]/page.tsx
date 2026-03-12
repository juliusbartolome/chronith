"use client";

import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import {
  useStaffMember,
  useUpdateStaff,
  useUpdateStaffAvailability,
} from "@/hooks/use-staff";
import { StaffForm, StaffFormValues } from "@/components/staff/staff-form";
import {
  AvailabilityEditor,
  AvailabilityWindow,
} from "@/components/staff/availability-editor";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function StaffDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data: staff, isLoading } = useStaffMember(id);
  const updateStaff = useUpdateStaff();
  const updateAvailability = useUpdateStaffAvailability();
  const [windows, setWindows] = useState<AvailabilityWindow[]>([]);
  const [availabilityInitialized, setAvailabilityInitialized] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [availabilityError, setAvailabilityError] = useState<string | null>(
    null,
  );

  // Initialize availability windows from loaded staff data
  if (staff && !availabilityInitialized) {
    setWindows(staff.availabilityWindows ?? []);
    setAvailabilityInitialized(true);
  }

  const handleProfileSave = async (values: StaffFormValues) => {
    setError(null);
    try {
      await updateStaff.mutateAsync({ id, data: values });
    } catch (e: any) {
      setError(e.message ?? "Failed to update staff member");
    }
  };

  const handleAvailabilitySave = async () => {
    setAvailabilityError(null);
    try {
      await updateAvailability.mutateAsync({ id, windows });
    } catch (e: any) {
      setAvailabilityError(e.message ?? "Failed to update availability");
    }
  };

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;
  if (!staff)
    return <p className="text-sm text-red-600">Staff member not found.</p>;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          ←
        </Button>
        <h1 className="text-2xl font-semibold">{staff.name}</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Profile</CardTitle>
        </CardHeader>
        <CardContent>
          {error && (
            <div
              role="alert"
              className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700"
            >
              {error}
            </div>
          )}
          <StaffForm
            defaultValues={{ name: staff.name, email: staff.email }}
            onSubmit={handleProfileSave}
            isSubmitting={updateStaff.isPending}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Availability</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {availabilityError && (
            <div
              role="alert"
              className="rounded-md bg-red-50 p-3 text-sm text-red-700"
            >
              {availabilityError}
            </div>
          )}
          <AvailabilityEditor windows={windows} onChange={setWindows} />
          <Button
            onClick={handleAvailabilitySave}
            disabled={updateAvailability.isPending}
          >
            {updateAvailability.isPending ? "Saving…" : "Save Availability"}
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

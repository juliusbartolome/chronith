"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { StaffForm, StaffFormValues } from "@/components/staff/staff-form";
import { useCreateStaff } from "@/hooks/use-staff";
import { Button } from "@/components/ui/button";

export default function NewStaffPage() {
  const router = useRouter();
  const createStaff = useCreateStaff();
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (values: StaffFormValues) => {
    setError(null);
    try {
      await createStaff.mutateAsync(values);
      router.push("/staff");
    } catch (e: any) {
      setError(e.message ?? "Failed to create staff member");
    }
  };

  return (
    <div className="mx-auto max-w-lg space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          ←
        </Button>
        <h1 className="text-2xl font-semibold">New Staff Member</h1>
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-md bg-red-50 p-3 text-sm text-red-700"
        >
          {error}
        </div>
      )}

      <StaffForm onSubmit={handleSubmit} isSubmitting={createStaff.isPending} />
    </div>
  );
}

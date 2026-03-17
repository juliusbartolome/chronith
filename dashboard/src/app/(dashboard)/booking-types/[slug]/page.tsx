"use client";

import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import {
  useBookingType,
  useUpdateBookingType,
} from "@/hooks/use-booking-types";
import {
  BookingTypeForm,
  BookingTypeFormValues,
} from "@/components/booking-types/booking-type-form";
import {
  CustomFieldEditor,
  CustomField,
} from "@/components/booking-types/custom-field-editor";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { WebhooksSection } from "./webhooks-section";

export default function BookingTypeDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const router = useRouter();
  const { data: bt, isLoading } = useBookingType(slug);
  const updateType = useUpdateBookingType();
  const [customFields, setCustomFields] = useState<CustomField[]>([]);
  const [fieldsInitialized, setFieldsInitialized] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (bt && !fieldsInitialized) {
    setCustomFields(bt.customFieldSchema ?? []);
    setFieldsInitialized(true);
  }

  const handleProfileSave = async (values: BookingTypeFormValues) => {
    setError(null);
    try {
      await updateType.mutateAsync({
        slug,
        data: { ...values, customFieldSchema: customFields },
      });
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to update booking type");
    }
  };

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;
  if (!bt)
    return <p className="text-sm text-red-600">Booking type not found.</p>;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          ←
        </Button>
        <h1 className="text-2xl font-semibold">{bt.name}</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Details</CardTitle>
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
          <BookingTypeForm
            defaultValues={{
              name: bt.name,
              slug: bt.slug,
              description: bt.description ?? "",
              kind: bt.kind,
              durationMinutes: bt.durationMinutes,
              requiresStaffAssignment: bt.requiresStaffAssignment,
            }}
            onSubmit={handleProfileSave}
            isSubmitting={updateType.isPending}
            isEdit
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Custom Fields</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <CustomFieldEditor fields={customFields} onChange={setCustomFields} />
          <Button
            onClick={() =>
              handleProfileSave({
                name: bt.name,
                slug: bt.slug,
                kind: bt.kind,
                requiresStaffAssignment: bt.requiresStaffAssignment,
              })
            }
            disabled={updateType.isPending}
          >
            {updateType.isPending ? "Saving…" : "Save Custom Fields"}
          </Button>
        </CardContent>
        </Card>

      <WebhooksSection bookingTypeSlug={slug} />
    </div>
  );
}

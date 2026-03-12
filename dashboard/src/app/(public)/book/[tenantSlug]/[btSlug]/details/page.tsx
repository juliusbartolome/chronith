"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { toast } from "sonner";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { useBookingSession } from "@/lib/booking-session";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import type { PublicCustomField } from "@/hooks/use-public-booking";

const baseSchema = z.object({
  name: z.string().min(1, "Name is required"),
  email: z.string().email("Valid email required"),
  phone: z.string().optional(),
});

type BaseDetails = z.infer<typeof baseSchema>;

function renderCustomField(
  field: PublicCustomField,
  value: unknown,
  onChange: (val: unknown) => void,
) {
  switch (field.type) {
    case "Text":
      return (
        <Input
          type="text"
          value={(value as string) ?? ""}
          onChange={(e) => onChange(e.target.value)}
          placeholder={field.label}
        />
      );
    case "Number":
      return (
        <Input
          type="number"
          value={(value as number) ?? ""}
          onChange={(e) => onChange(Number(e.target.value))}
        />
      );
    case "Select":
      return (
        <select
          className="w-full rounded-md border border-zinc-200 px-3 py-2 text-sm"
          value={(value as string) ?? ""}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">Select…</option>
          {field.options?.map((opt) => (
            <option key={opt} value={opt}>
              {opt}
            </option>
          ))}
        </select>
      );
    case "Checkbox":
      return (
        <div className="flex items-center gap-2">
          <Checkbox
            checked={!!value}
            onCheckedChange={(checked) => onChange(checked)}
            id={`cf-${field.key}`}
          />
          <Label htmlFor={`cf-${field.key}`}>{field.label}</Label>
        </div>
      );
    case "Date":
      return (
        <Input
          type="date"
          value={(value as string) ?? ""}
          onChange={(e) => onChange(e.target.value)}
        />
      );
    default:
      return null;
  }
}

export default function CustomerDetailsPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();

  const session = useBookingSession();
  const setCustomerInfo = useBookingSession((s) => s.setCustomerInfo);
  const setCustomFields = useBookingSession((s) => s.setCustomFields);

  // Guard: no date selected
  useEffect(() => {
    if (!session.selectedDate) {
      router.replace(`/book/${tenantSlug}/${btSlug}`);
    }
  }, [session.selectedDate, router, tenantSlug, btSlug]);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<BaseDetails>({
    resolver: zodResolver(baseSchema),
    defaultValues: {
      name: session.customerInfo?.name ?? "",
      email: session.customerInfo?.email ?? "",
      phone: session.customerInfo?.phone ?? "",
    },
  });

  const [customFieldValues, setCustomFieldValues] = useState<
    Record<string, unknown>
  >(session.customFields ?? {});

  const onSubmit = (values: BaseDetails) => {
    // Validate required custom fields
    for (const field of session.customFieldSchema ?? []) {
      if (field.required && !customFieldValues[field.key]) {
        toast.error(`"${field.label}" is required.`);
        return;
      }
    }

    setCustomerInfo({
      name: values.name,
      email: values.email,
      phone: values.phone || undefined,
    });
    setCustomFields(customFieldValues);
    router.push(`/book/${tenantSlug}/${btSlug}/confirm`);
  };

  return (
    <div className="max-w-lg mx-auto px-4 py-10">
      <p className="text-xs text-zinc-500 mb-6">Step 3 of 5 — Your Details</p>
      <h1 className="text-xl font-bold text-zinc-900 mb-6">Your Information</h1>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="name">Full Name *</Label>
          <Input id="name" {...register("name")} />
          {errors.name && (
            <p className="mt-1 text-xs text-red-600">{errors.name.message}</p>
          )}
        </div>

        <div>
          <Label htmlFor="email">Email Address *</Label>
          <Input id="email" type="email" {...register("email")} />
          {errors.email && (
            <p className="mt-1 text-xs text-red-600">{errors.email.message}</p>
          )}
        </div>

        <div>
          <Label htmlFor="phone">Phone (optional)</Label>
          <Input id="phone" type="tel" {...register("phone")} />
        </div>

        {/* Dynamic custom fields */}
        {(session.customFieldSchema ?? []).map((field) => (
          <div key={field.key}>
            <Label htmlFor={`cf-${field.key}`}>
              {field.label}
              {field.required && " *"}
            </Label>
            {renderCustomField(field, customFieldValues[field.key], (val) =>
              setCustomFieldValues((prev) => ({ ...prev, [field.key]: val })),
            )}
          </div>
        ))}

        <Button type="submit" className="w-full">
          Continue
        </Button>
      </form>
    </div>
  );
}

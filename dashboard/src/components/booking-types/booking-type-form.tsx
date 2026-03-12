"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

const schema = z.object({
  name: z.string().min(1, "Name is required"),
  slug: z
    .string()
    .min(1, "Slug is required")
    .regex(/^[a-z0-9-]+$/, "Slug must be lowercase alphanumeric with hyphens"),
  description: z.string().optional(),
  kind: z.enum(["TimeSlot", "Calendar"]),
  durationMinutes: z.coerce.number().min(1).optional(),
  requiresStaffAssignment: z.boolean(),
});

export type BookingTypeFormValues = z.infer<typeof schema>;

interface Props {
  defaultValues?: Partial<BookingTypeFormValues>;
  onSubmit: (values: BookingTypeFormValues) => void | Promise<void>;
  isSubmitting?: boolean;
  isEdit?: boolean;
}

export function BookingTypeForm({
  defaultValues,
  onSubmit,
  isSubmitting,
  isEdit,
}: Props) {
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<BookingTypeFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      kind: "TimeSlot",
      requiresStaffAssignment: false,
      ...defaultValues,
    },
  });

  const kind = watch("kind");

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div>
        <Label htmlFor="name">Name</Label>
        <Input id="name" {...register("name")} />
        {errors.name && (
          <p className="mt-1 text-xs text-red-600">{errors.name.message}</p>
        )}
      </div>

      <div>
        <Label htmlFor="slug">Slug</Label>
        <Input id="slug" {...register("slug")} disabled={isEdit} />
        {errors.slug && (
          <p className="mt-1 text-xs text-red-600">{errors.slug.message}</p>
        )}
      </div>

      <div>
        <Label htmlFor="description">Description</Label>
        <Input id="description" {...register("description")} />
      </div>

      <div>
        <Label htmlFor="kind">Kind</Label>
        <Select
          value={kind}
          onValueChange={(v) => setValue("kind", v as "TimeSlot" | "Calendar")}
          disabled={isEdit}
        >
          <SelectTrigger id="kind" className="mt-1">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="TimeSlot">Time Slot (Fixed Duration)</SelectItem>
            <SelectItem value="Calendar">Calendar (Variable)</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {kind === "TimeSlot" && (
        <div>
          <Label htmlFor="durationMinutes">Duration (minutes)</Label>
          <Input
            id="durationMinutes"
            type="number"
            min={1}
            {...register("durationMinutes")}
          />
        </div>
      )}

      <div className="flex items-center gap-2">
        <input
          id="requiresStaff"
          type="checkbox"
          {...register("requiresStaffAssignment")}
          className="h-4 w-4"
        />
        <Label htmlFor="requiresStaff">Requires Staff Assignment</Label>
      </div>

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? "Saving…" : "Save"}
      </Button>
    </form>
  );
}

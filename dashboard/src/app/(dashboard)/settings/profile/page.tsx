"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useState, useEffect } from "react";
import { useTenantProfile, useUpdateTenantProfile } from "@/hooks/use-tenant";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const schema = z.object({
  name: z.string().min(1, "Name is required"),
  timezone: z.string().min(1, "Timezone is required"),
});

type ProfileForm = z.infer<typeof schema>;

export default function ProfileSettingsPage() {
  const { data: profile, isLoading } = useTenantProfile();
  const updateProfile = useUpdateTenantProfile();
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ProfileForm>({ resolver: zodResolver(schema) });

  useEffect(() => {
    if (profile) reset({ name: profile.name, timezone: profile.timezone });
  }, [profile, reset]);

  const onSubmit = async (values: ProfileForm) => {
    setError(null);
    setSuccess(false);
    try {
      await updateProfile.mutateAsync(values);
      setSuccess(true);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to save profile");
    }
  };

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Tenant Profile</CardTitle>
      </CardHeader>
      <CardContent>
        {success && (
          <div className="mb-4 rounded-md bg-green-50 p-3 text-sm text-green-700">
            Profile saved successfully.
          </div>
        )}
        {error && (
          <div
            role="alert"
            className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700"
          >
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div>
            <Label htmlFor="name">Tenant Name</Label>
            <Input id="name" {...register("name")} />
            {errors.name && (
              <p className="mt-1 text-xs text-red-600">{errors.name.message}</p>
            )}
          </div>

          <div>
            <Label htmlFor="slug-display">Slug (read-only)</Label>
            <Input id="slug-display" value={profile?.slug ?? ""} disabled />
          </div>

          <div>
            <Label htmlFor="timezone">Timezone</Label>
            <Input
              id="timezone"
              {...register("timezone")}
              placeholder="Asia/Manila"
            />
            {errors.timezone && (
              <p className="mt-1 text-xs text-red-600">
                {errors.timezone.message}
              </p>
            )}
          </div>

          <Button
            type="submit"
            disabled={isSubmitting || updateProfile.isPending}
          >
            {isSubmitting || updateProfile.isPending
              ? "Saving…"
              : "Save Changes"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

"use client";

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from "@/hooks/use-tenant-settings";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

const schema = z.object({
  logoUrl: z.string().url("Must be a valid URL").or(z.literal("")).nullable(),
  primaryColor: z
    .string()
    .regex(/^#[0-9A-Fa-f]{6}$/, "Must be a valid hex color (e.g. #2563EB)"),
  accentColor: z
    .string()
    .regex(/^#[0-9A-Fa-f]{6}$/, "Must be a valid hex color")
    .or(z.literal(""))
    .nullable(),
  welcomeMessage: z.string().max(500).nullable(),
  termsUrl: z.string().url("Must be a valid URL").or(z.literal("")).nullable(),
  privacyUrl: z
    .string()
    .url("Must be a valid URL")
    .or(z.literal(""))
    .nullable(),
  bookingPageEnabled: z.boolean(),
});

type BrandingForm = z.infer<typeof schema>;

function BrandingPreview({
  logoUrl,
  primaryColor,
  welcomeMessage,
}: {
  logoUrl: string | null;
  primaryColor: string;
  welcomeMessage: string | null;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm font-medium text-zinc-500">
          Live Preview
        </CardTitle>
      </CardHeader>
      <CardContent>
        <div className="rounded-lg border p-4 space-y-3">
          {/* Tenant logo */}
          {logoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={logoUrl}
              alt="Tenant logo"
              className="h-8 object-contain"
              onError={(e) => {
                (e.target as HTMLImageElement).style.display = "none";
              }}
            />
          ) : (
            <div className="h-8 w-24 rounded bg-zinc-200 flex items-center justify-center text-xs text-zinc-400">
              Logo
            </div>
          )}

          {/* Welcome message */}
          {welcomeMessage && (
            <p className="text-sm text-zinc-700">{welcomeMessage}</p>
          )}

          {/* Mock booking card */}
          <div className="rounded-md border p-3 space-y-2">
            <p className="text-sm font-semibold">Sample Booking Type</p>
            <p className="text-xs text-zinc-500">60 min · ₱500</p>
            <button
              type="button"
              className="w-full rounded-md px-3 py-1.5 text-sm font-medium text-white"
              style={{ backgroundColor: primaryColor }}
            >
              Book Now
            </button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export default function BrandingSettingsPage() {
  const { data: settings, isLoading } = useTenantSettings();
  const update = useUpdateTenantSettings();

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<BrandingForm>({
    resolver: zodResolver(schema),
    defaultValues: {
      logoUrl: null,
      primaryColor: "#2563EB",
      accentColor: null,
      welcomeMessage: null,
      termsUrl: null,
      privacyUrl: null,
      bookingPageEnabled: true,
    },
  });

  useEffect(() => {
    if (settings) {
      reset({
        logoUrl: settings.logoUrl ?? "",
        primaryColor: settings.primaryColor,
        accentColor: settings.accentColor ?? "",
        welcomeMessage: settings.welcomeMessage ?? "",
        termsUrl: settings.termsUrl ?? "",
        privacyUrl: settings.privacyUrl ?? "",
        bookingPageEnabled: settings.bookingPageEnabled,
      });
    }
  }, [settings, reset]);

  const watchedLogoUrl = watch("logoUrl");
  const watchedPrimary = watch("primaryColor");
  const watchedWelcome = watch("welcomeMessage");
  const watchedEnabled = watch("bookingPageEnabled");

  const onSubmit = async (values: BrandingForm) => {
    try {
      await update.mutateAsync({
        logoUrl: values.logoUrl || null,
        primaryColor: values.primaryColor,
        accentColor: values.accentColor || null,
        welcomeMessage: values.welcomeMessage || null,
        termsUrl: values.termsUrl || null,
        privacyUrl: values.privacyUrl || null,
        bookingPageEnabled: values.bookingPageEnabled,
      });
      toast.success("Branding saved successfully.");
    } catch {
      toast.error("Failed to save branding. Please try again.");
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
        <Skeleton className="h-10 w-full" />
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">
      {/* Form */}
      <Card>
        <CardHeader>
          <CardTitle>Branding</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            {/* Logo URL */}
            <div>
              <Label htmlFor="logoUrl">Logo URL</Label>
              <Input
                id="logoUrl"
                placeholder="https://example.com/logo.png"
                {...register("logoUrl")}
              />
              {errors.logoUrl && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.logoUrl.message}
                </p>
              )}
            </div>

            {/* Primary Color */}
            <div>
              <Label htmlFor="primaryColor">Primary Color</Label>
              <div className="flex gap-2 items-center">
                <input
                  type="color"
                  value={watchedPrimary ?? "#2563EB"}
                  onChange={(e) =>
                    setValue("primaryColor", e.target.value, {
                      shouldValidate: true,
                    })
                  }
                  className="h-9 w-10 cursor-pointer rounded border border-zinc-200 p-0.5"
                />
                <Input
                  id="primaryColor"
                  placeholder="#2563EB"
                  className="font-mono"
                  {...register("primaryColor")}
                />
              </div>
              {errors.primaryColor && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.primaryColor.message}
                </p>
              )}
            </div>

            {/* Accent Color */}
            <div>
              <Label htmlFor="accentColor">Accent Color (optional)</Label>
              <div className="flex gap-2 items-center">
                <input
                  type="color"
                  value={watch("accentColor") || "#6366F1"}
                  onChange={(e) =>
                    setValue("accentColor", e.target.value, {
                      shouldValidate: true,
                    })
                  }
                  className="h-9 w-10 cursor-pointer rounded border border-zinc-200 p-0.5"
                />
                <Input
                  id="accentColor"
                  placeholder="#6366F1"
                  className="font-mono"
                  {...register("accentColor")}
                />
              </div>
              {errors.accentColor && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.accentColor.message}
                </p>
              )}
            </div>

            {/* Welcome Message */}
            <div>
              <Label htmlFor="welcomeMessage">Welcome Message (optional)</Label>
              <Textarea
                id="welcomeMessage"
                placeholder="Welcome to our booking page!"
                rows={3}
                {...register("welcomeMessage")}
              />
              {errors.welcomeMessage && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.welcomeMessage.message}
                </p>
              )}
            </div>

            {/* Terms URL */}
            <div>
              <Label htmlFor="termsUrl">Terms of Service URL (optional)</Label>
              <Input
                id="termsUrl"
                placeholder="https://example.com/terms"
                {...register("termsUrl")}
              />
              {errors.termsUrl && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.termsUrl.message}
                </p>
              )}
            </div>

            {/* Privacy URL */}
            <div>
              <Label htmlFor="privacyUrl">Privacy Policy URL (optional)</Label>
              <Input
                id="privacyUrl"
                placeholder="https://example.com/privacy"
                {...register("privacyUrl")}
              />
              {errors.privacyUrl && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.privacyUrl.message}
                </p>
              )}
            </div>

            {/* Booking Page Toggle */}
            <div className="flex items-center gap-3">
              <Switch
                id="bookingPageEnabled"
                checked={watchedEnabled}
                onCheckedChange={(val) => setValue("bookingPageEnabled", val)}
              />
              <Label htmlFor="bookingPageEnabled">
                Public booking page enabled
              </Label>
            </div>

            <Button
              type="submit"
              disabled={isSubmitting || update.isPending}
            >
              {isSubmitting || update.isPending ? "Saving…" : "Save Branding"}
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Live Preview */}
      <BrandingPreview
        logoUrl={watchedLogoUrl || null}
        primaryColor={watchedPrimary ?? "#2563EB"}
        welcomeMessage={watchedWelcome || null}
      />
    </div>
  );
}

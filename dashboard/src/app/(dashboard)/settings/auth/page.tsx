"use client";

import { useEffect, useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  useTenantAuthConfig,
  useUpdateTenantAuthConfig,
} from "@/hooks/use-tenant";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";

const schema = z.object({
  builtInAuthEnabled: z.boolean(),
  magicLinkEnabled: z.boolean(),
  oidcIssuerUrl: z.string().optional(),
  oidcClientId: z.string().optional(),
  oidcAudience: z.string().optional(),
});

type AuthConfigForm = z.infer<typeof schema>;

export default function AuthConfigSettingsPage() {
  const { data: config, isLoading } = useTenantAuthConfig();
  const updateConfig = useUpdateTenantAuthConfig();
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, control, setValue, reset } =
    useForm<AuthConfigForm>({
      resolver: zodResolver(schema),
      defaultValues: {
        builtInAuthEnabled: true,
        magicLinkEnabled: false,
      },
    });

  useEffect(() => {
    if (config) reset(config);
  }, [config, reset]);

  const builtInAuthEnabled = useWatch({ control, name: "builtInAuthEnabled" });
  const magicLinkEnabled = useWatch({ control, name: "magicLinkEnabled" });

  const onSubmit = async (values: AuthConfigForm) => {
    setError(null);
    setSuccess(false);
    try {
      await updateConfig.mutateAsync(values);
      setSuccess(true);
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to save auth config");
    }
  };

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Auth Configuration</CardTitle>
      </CardHeader>
      <CardContent>
        {success && (
          <div className="mb-4 rounded-md bg-green-50 p-3 text-sm text-green-700">
            Auth config saved.
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

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
          <div className="flex items-center justify-between">
            <div>
              <Label>Built-in Auth</Label>
              <p className="text-sm text-zinc-500">
                Enable username/password login
              </p>
            </div>
            <Switch
              checked={builtInAuthEnabled}
              onCheckedChange={(v) => setValue("builtInAuthEnabled", v)}
            />
          </div>

          <div className="flex items-center justify-between">
            <div>
              <Label>Magic Link</Label>
              <p className="text-sm text-zinc-500">
                Enable passwordless email login
              </p>
            </div>
            <Switch
              checked={magicLinkEnabled}
              onCheckedChange={(v) => setValue("magicLinkEnabled", v)}
            />
          </div>

          <div className="space-y-4 border-t pt-4">
            <h3 className="text-sm font-semibold">OIDC Configuration</h3>
            <div>
              <Label htmlFor="oidcIssuerUrl">Issuer URL</Label>
              <Input
                id="oidcIssuerUrl"
                {...register("oidcIssuerUrl")}
                placeholder="https://accounts.example.com"
              />
            </div>
            <div>
              <Label htmlFor="oidcClientId">Client ID</Label>
              <Input id="oidcClientId" {...register("oidcClientId")} />
            </div>
            <div>
              <Label htmlFor="oidcAudience">Audience</Label>
              <Input id="oidcAudience" {...register("oidcAudience")} />
            </div>
          </div>

          <Button type="submit" disabled={updateConfig.isPending}>
            {updateConfig.isPending ? "Saving…" : "Save Changes"}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}

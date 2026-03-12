"use client";

import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import {
  useApiKeys,
  useCreateApiKey,
  useRevokeApiKey,
} from "@/hooks/use-tenant";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { format } from "date-fns";

const schema = z.object({
  label: z.string().min(1, "Label is required"),
  role: z.enum(["TenantAdmin", "TenantStaff"]),
  expiresAt: z.string().optional(),
});

type ApiKeyForm = z.infer<typeof schema>;

export default function ApiKeysPage() {
  const { data, isLoading } = useApiKeys();
  const createKey = useCreateApiKey();
  const revokeKey = useRevokeApiKey();
  const [newKey, setNewKey] = useState<string | null>(null);
  const [createError, setCreateError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<ApiKeyForm>({
    resolver: zodResolver(schema),
    defaultValues: { role: "TenantAdmin" },
  });

  const role = watch("role");

  const onSubmit = async (values: ApiKeyForm) => {
    setCreateError(null);
    try {
      const result = await createKey.mutateAsync(values);
      setNewKey(result.key);
      reset();
    } catch (e: unknown) {
      setCreateError(
        e instanceof Error ? e.message : "Failed to create API key",
      );
    }
  };

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>API Keys</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading && <p className="text-sm text-zinc-500">Loading…</p>}
          {data && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Label</TableHead>
                  <TableHead>Role</TableHead>
                  <TableHead>Created</TableHead>
                  <TableHead>Expires</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.items?.length === 0 && (
                  <TableRow>
                    <TableCell
                      colSpan={5}
                      className="text-center text-zinc-400"
                    >
                      No API keys yet.
                    </TableCell>
                  </TableRow>
                )}
                {data.items?.map((k: { id: string; label: string; role: string; createdAt?: string; expiresAt?: string }) => (
                  <TableRow key={k.id}>
                    <TableCell className="font-medium">{k.label}</TableCell>
                    <TableCell>{k.role}</TableCell>
                    <TableCell>
                      {k.createdAt ? format(new Date(k.createdAt), "PP") : "—"}
                    </TableCell>
                    <TableCell>
                      {k.expiresAt
                        ? format(new Date(k.expiresAt), "PP")
                        : "Never"}
                    </TableCell>
                    <TableCell>
                      <AlertDialog>
                        <AlertDialogTrigger asChild>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="text-red-600 hover:text-red-700"
                          >
                            Revoke
                          </Button>
                        </AlertDialogTrigger>
                        <AlertDialogContent>
                          <AlertDialogHeader>
                            <AlertDialogTitle>Revoke API key?</AlertDialogTitle>
                            <AlertDialogDescription>
                              &quot;{k.label}&quot; will be permanently revoked.
                            </AlertDialogDescription>
                          </AlertDialogHeader>
                          <AlertDialogFooter>
                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                            <AlertDialogAction
                              onClick={() => revokeKey.mutate(k.id)}
                            >
                              Revoke
                            </AlertDialogAction>
                          </AlertDialogFooter>
                        </AlertDialogContent>
                      </AlertDialog>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Create API Key</CardTitle>
        </CardHeader>
        <CardContent>
          {createError && (
            <div
              role="alert"
              className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700"
            >
              {createError}
            </div>
          )}
          <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
            <div>
              <Label htmlFor="label">Label</Label>
              <Input
                id="label"
                {...register("label")}
                placeholder="e.g. CI Pipeline"
              />
              {errors.label && (
                <p className="mt-1 text-xs text-red-600">
                  {errors.label.message}
                </p>
              )}
            </div>

            <div>
              <Label htmlFor="role">Role</Label>
              <Select
                value={role}
                onValueChange={(v) =>
                  setValue("role", v as "TenantAdmin" | "TenantStaff")
                }
              >
                <SelectTrigger id="role" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="TenantAdmin">Tenant Admin</SelectItem>
                  <SelectItem value="TenantStaff">Tenant Staff</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label htmlFor="expiresAt">Expires At (optional)</Label>
              <Input id="expiresAt" type="date" {...register("expiresAt")} />
            </div>

            <Button
              type="submit"
              disabled={isSubmitting || createKey.isPending}
            >
              {isSubmitting || createKey.isPending
                ? "Creating…"
                : "Create API Key"}
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Show new key once modal */}
      <Dialog open={!!newKey} onOpenChange={() => setNewKey(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>API Key Created</DialogTitle>
            <DialogDescription>
              Copy this key now. It will not be shown again.
            </DialogDescription>
          </DialogHeader>
          <div className="rounded-md bg-zinc-900 p-4 font-mono text-sm text-green-400 break-all">
            {newKey}
          </div>
          <Button
            onClick={() => {
              if (newKey) navigator.clipboard.writeText(newKey);
            }}
            variant="outline"
          >
            Copy to Clipboard
          </Button>
        </DialogContent>
      </Dialog>
    </div>
  );
}

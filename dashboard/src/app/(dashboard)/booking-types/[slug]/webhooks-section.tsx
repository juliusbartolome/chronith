"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  useWebhooks,
  useCreateWebhook,
  useDeleteWebhook,
  useWebhookDeliveries,
  useRetryWebhookDelivery,
  useTestWebhook,
  type WebhookDto,
  type WebhookDeliveryDto,
} from "@/hooks/use-webhooks";

const AVAILABLE_EVENTS = [
  "booking.confirmed",
  "booking.cancelled",
  "booking.rescheduled",
  "booking.pending_payment",
  "booking.pending_verification",
];

function statusVariant(
  status: string,
): "default" | "destructive" | "secondary" | "outline" {
  if (status === "Success") return "default";
  if (status === "Failed") return "destructive";
  return "secondary";
}

interface CreateWebhookDialogProps {
  bookingTypeSlug: string;
  onCreated?: () => void;
}

function CreateWebhookDialog({
  bookingTypeSlug,
  onCreated,
}: CreateWebhookDialogProps) {
  const [open, setOpen] = useState(false);
  const [url, setUrl] = useState("");
  const [secret, setSecret] = useState("");
  const [selectedEvents, setSelectedEvents] = useState<string[]>([]);
  const [createError, setCreateError] = useState<string | null>(null);
  const create = useCreateWebhook(bookingTypeSlug);

  const toggleEvent = (event: string) => {
    setSelectedEvents((prev) =>
      prev.includes(event) ? prev.filter((e) => e !== event) : [...prev, event],
    );
  };

  const handleCreate = async () => {
    setCreateError(null);
    try {
      await create.mutateAsync({
        url,
        events: selectedEvents,
        secret: secret || undefined,
      });
      setOpen(false);
      setUrl("");
      setSecret("");
      setSelectedEvents([]);
      onCreated?.();
    } catch (err) {
      setCreateError(
        err instanceof Error ? err.message : "Failed to create webhook",
      );
    }
  };

  const isDisabled = !url || selectedEvents.length === 0 || create.isPending;

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm">Add Webhook</Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Webhook</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1">
            <Label htmlFor="webhook-url">URL</Label>
            <Input
              id="webhook-url"
              placeholder="https://example.com/hook"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="webhook-secret">Secret (optional)</Label>
            <Input
              id="webhook-secret"
              type="password"
              placeholder="Signing secret"
              value={secret}
              onChange={(e) => setSecret(e.target.value)}
            />
          </div>
          <div className="space-y-2">
            <Label>Events</Label>
            <div className="space-y-1">
              {AVAILABLE_EVENTS.map((event) => (
                <label
                  key={event}
                  className="flex cursor-pointer items-center gap-2 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={selectedEvents.includes(event)}
                    onChange={() => toggleEvent(event)}
                    className="rounded"
                  />
                  {event}
                </label>
              ))}
            </div>
          </div>
          {createError && (
            <p className="text-sm text-red-600">{createError}</p>
          )}
        </div>
        <DialogFooter>
          <Button onClick={handleCreate} disabled={isDisabled}>
            {create.isPending ? "Creating…" : "Create"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

interface DeliveriesRowProps {
  bookingTypeSlug: string;
  webhookId: string;
}

function DeliveriesRow({ bookingTypeSlug, webhookId }: DeliveriesRowProps) {
  const { data: deliveries = [], isLoading, isError } = useWebhookDeliveries(
    bookingTypeSlug,
    webhookId,
  );
  const retry = useRetryWebhookDelivery(bookingTypeSlug);
  const [pendingDeliveryId, setPendingDeliveryId] = useState<string | null>(
    null,
  );

  if (isLoading) {
    return (
      <TableRow>
        <TableCell colSpan={5} className="text-sm text-zinc-500">
          Loading deliveries…
        </TableCell>
      </TableRow>
    );
  }

  if (isError) {
    return (
      <TableRow>
        <TableCell colSpan={5} className="text-sm text-red-600">
          Failed to load deliveries.
        </TableCell>
      </TableRow>
    );
  }

  if (deliveries.length === 0) {
    return (
      <TableRow>
        <TableCell colSpan={5} className="text-sm text-zinc-500">
          No deliveries yet.
        </TableCell>
      </TableRow>
    );
  }

  return (
    <>
      {deliveries.map((delivery: WebhookDeliveryDto) => (
        <TableRow key={delivery.id} className="bg-zinc-50">
          <TableCell className="pl-8 text-xs">{delivery.event}</TableCell>
          <TableCell>
            <Badge variant={statusVariant(delivery.status)}>
              {delivery.status}
            </Badge>
          </TableCell>
          <TableCell className="text-xs">{delivery.statusCode ?? "—"}</TableCell>
          <TableCell className="text-xs">
            {delivery.deliveredAt
              ? new Date(delivery.deliveredAt).toLocaleString()
              : "—"}
          </TableCell>
          <TableCell>
            {delivery.status === "Failed" && (
              <Button
                size="sm"
                variant="outline"
                onClick={async () => {
                  setPendingDeliveryId(delivery.id);
                  try {
                    await retry.mutateAsync({
                      webhookId,
                      deliveryId: delivery.id,
                    });
                  } finally {
                    setPendingDeliveryId(null);
                  }
                }}
                disabled={pendingDeliveryId === delivery.id}
              >
                Retry
              </Button>
            )}
          </TableCell>
        </TableRow>
      ))}
    </>
  );
}

interface WebhookRowProps {
  webhook: WebhookDto;
  bookingTypeSlug: string;
}

function WebhookRow({ webhook, bookingTypeSlug }: WebhookRowProps) {
  const [showDeliveries, setShowDeliveries] = useState(false);
  const deleteWebhook = useDeleteWebhook(bookingTypeSlug);
  const testWebhook = useTestWebhook(bookingTypeSlug);

  return (
    <>
      <TableRow>
        <TableCell className="font-mono text-sm">{webhook.url}</TableCell>
        <TableCell>
          <div className="flex flex-wrap gap-1">
            {webhook.events.map((event) => (
              <Badge key={event} variant="secondary" className="text-xs">
                {event}
              </Badge>
            ))}
          </div>
        </TableCell>
        <TableCell>
          {webhook.lastDeliveryStatus && (
            <Badge variant={statusVariant(webhook.lastDeliveryStatus)}>
              {webhook.lastDeliveryStatus}
            </Badge>
          )}
        </TableCell>
        <TableCell>
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={() => setShowDeliveries((v) => !v)}
            >
              Deliveries
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={() =>
                testWebhook.mutateAsync(webhook.id).catch(() => {})
              }
              disabled={testWebhook.isPending}
            >
              Test
            </Button>
            <Button
              size="sm"
              variant="destructive"
              onClick={() =>
                deleteWebhook.mutateAsync(webhook.id).catch(() => {})
              }
              disabled={deleteWebhook.isPending}
            >
              Delete
            </Button>
          </div>
        </TableCell>
      </TableRow>
      {showDeliveries && (
        <TableRow>
          <TableCell colSpan={4} className="p-0">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="pl-8">Event</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>HTTP</TableHead>
                  <TableHead>Delivered At</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                <DeliveriesRow
                  bookingTypeSlug={bookingTypeSlug}
                  webhookId={webhook.id}
                />
              </TableBody>
            </Table>
          </TableCell>
        </TableRow>
      )}
    </>
  );
}

interface WebhooksSectionProps {
  bookingTypeSlug: string;
}

export function WebhooksSection({ bookingTypeSlug }: WebhooksSectionProps) {
  const { data: webhooks, isLoading, isError } = useWebhooks(bookingTypeSlug);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Webhooks</CardTitle>
        <CreateWebhookDialog bookingTypeSlug={bookingTypeSlug} />
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <p className="text-sm text-zinc-500">Loading…</p>
        ) : isError ? (
          <p className="text-sm text-red-600">Failed to load webhooks.</p>
        ) : !webhooks || webhooks.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No webhooks configured. Add one to receive event notifications.
          </p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>URL</TableHead>
                <TableHead>Events</TableHead>
                <TableHead>Last Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {webhooks.map((webhook: WebhookDto) => (
                <WebhookRow
                  key={webhook.id}
                  webhook={webhook}
                  bookingTypeSlug={bookingTypeSlug}
                />
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}

"use client";

import { useState } from "react";
import { toast } from "sonner";
import { format } from "date-fns";
import { useSubscription, useChangePlan, useCancelSubscription } from "@/hooks/use-subscription";
import { useUsage } from "@/hooks/use-usage";
import { usePlans } from "@/hooks/use-plans";
import { Progress } from "@/components/ui/progress";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { formatPrice } from "@/lib/format";
import { cn } from "@/lib/utils";
import { AlertTriangle, CreditCard } from "lucide-react";

// ── Usage meter ───────────────────────────────────────────────────────────────

function UsageMeter({
  label,
  used,
  limit,
}: {
  label: string;
  used: number;
  limit: number;
}) {
  const pct = limit > 0 ? Math.min((used / limit) * 100, 100) : 0;
  const isWarning = pct >= 80 && pct < 100;
  const isCritical = pct >= 100;

  return (
    <div className="space-y-1.5">
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium text-zinc-700">{label}</span>
        <span
          className={cn(
            "text-xs font-medium",
            isCritical
              ? "text-red-600"
              : isWarning
                ? "text-yellow-600"
                : "text-zinc-500",
          )}
        >
          {used} / {limit}
          {isCritical && (
            <AlertTriangle className="ml-1 inline h-3.5 w-3.5" />
          )}
        </span>
      </div>
      <Progress
        value={pct}
        className={cn(
          "h-2",
          isCritical
            ? "[&>div]:bg-red-500"
            : isWarning
              ? "[&>div]:bg-yellow-500"
              : "[&>div]:bg-zinc-900",
        )}
      />
      {isWarning && (
        <p className="text-xs text-yellow-600">
          Approaching limit — consider upgrading your plan.
        </p>
      )}
      {isCritical && (
        <p className="text-xs text-red-600">
          Limit reached — upgrade to add more.
        </p>
      )}
    </div>
  );
}

// ── Change plan dialog ─────────────────────────────────────────────────────────

function ChangePlanDialog({
  open,
  onOpenChange,
  currentPlanId,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
  currentPlanId: string;
}) {
  const { data: plans = [], isLoading } = usePlans();
  const { mutate: changePlan, isPending } = useChangePlan();
  const [selected, setSelected] = useState(currentPlanId);

  const sorted = [...plans].sort((a, b) => a.sortOrder - b.sortOrder);

  function handleConfirm() {
    if (!selected || selected === currentPlanId) return;
    changePlan(
      { newPlanId: selected },
      {
        onSuccess: () => {
          toast.success("Plan updated successfully");
          onOpenChange(false);
        },
        onError: (err) => toast.error(err.message),
      },
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Change plan</DialogTitle>
          <DialogDescription>
            Select a new plan. Changes take effect immediately.
          </DialogDescription>
        </DialogHeader>

        {isLoading ? (
          <div className="space-y-2">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2">
            {sorted.map((plan) => (
              <button
                key={plan.id}
                type="button"
                onClick={() => setSelected(plan.id)}
                className={cn(
                  "rounded-lg border p-4 text-left transition-all",
                  selected === plan.id
                    ? "border-zinc-900 bg-zinc-900 text-white"
                    : "border-zinc-200 hover:border-zinc-400",
                  plan.id === currentPlanId && selected !== plan.id
                    ? "opacity-60"
                    : "",
                )}
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold">{plan.name}</span>
                  {plan.id === currentPlanId && (
                    <Badge
                      variant={selected === plan.id ? "outline" : "secondary"}
                      className="text-xs"
                    >
                      Current
                    </Badge>
                  )}
                </div>
                <p className="mt-1 text-sm">
                  {plan.priceCentavos === 0
                    ? "Free"
                    : `${formatPrice(plan.priceCentavos)}/mo`}
                </p>
              </button>
            ))}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={isPending || !selected || selected === currentPlanId}
          >
            {isPending ? "Updating…" : "Confirm change"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Cancel dialog ─────────────────────────────────────────────────────────────

function CancelDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (o: boolean) => void;
}) {
  const { mutate: cancel, isPending } = useCancelSubscription();

  function handleCancel() {
    cancel(
      { reason: "User requested cancellation" },
      {
        onSuccess: () => {
          toast.success("Subscription cancelled");
          onOpenChange(false);
        },
        onError: (err) => toast.error(err.message),
      },
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Cancel subscription</DialogTitle>
          <DialogDescription>
            Your subscription will remain active until the end of the current billing
            period. After that, you&apos;ll be moved to the free plan.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Keep subscription
          </Button>
          <Button variant="destructive" onClick={handleCancel} disabled={isPending}>
            {isPending ? "Cancelling…" : "Confirm cancellation"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function SubscriptionPage() {
  const { data: sub, isLoading: subLoading } = useSubscription();
  const { data: usage, isLoading: usageLoading } = useUsage();
  const [changePlanOpen, setChangePlanOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);

  const isActive = sub?.status === "Active" || sub?.status === "Trial";

  return (
    <div className="space-y-8 max-w-2xl">
      <div>
        <h1 className="text-2xl font-bold text-zinc-900">Subscription</h1>
        <p className="mt-1 text-sm text-zinc-500">
          Manage your plan, usage, and billing.
        </p>
      </div>

      {/* Current plan */}
      <section className="rounded-xl border bg-white p-6 space-y-4">
        <div className="flex items-start justify-between">
          <div>
            <h2 className="font-semibold text-zinc-900">Current plan</h2>
            {subLoading ? (
              <Skeleton className="mt-1 h-5 w-32" />
            ) : (
              <div className="mt-1 flex items-center gap-2">
                <span className="text-xl font-bold text-zinc-900">
                  {sub?.planName ?? "Free"}
                </span>
                {sub?.status && (
                  <Badge
                    variant={isActive ? "default" : "secondary"}
                    className={cn(
                      isActive && "bg-green-100 text-green-800 hover:bg-green-100",
                    )}
                  >
                    {sub.status}
                  </Badge>
                )}
              </div>
            )}
          </div>
          <CreditCard className="h-5 w-5 text-zinc-400" />
        </div>

        {!subLoading && sub && (
          <dl className="space-y-1 text-sm">
            <div className="flex justify-between">
              <dt className="text-zinc-500">Period</dt>
              <dd>
                {format(new Date(sub.currentPeriodStart), "MMM d, yyyy")} –{" "}
                {format(new Date(sub.currentPeriodEnd), "MMM d, yyyy")}
              </dd>
            </div>
            {sub.trialEndsAt && (
              <div className="flex justify-between">
                <dt className="text-zinc-500">Trial ends</dt>
                <dd>{format(new Date(sub.trialEndsAt), "MMM d, yyyy")}</dd>
              </div>
            )}
            {sub.cancelledAt && (
              <div className="flex justify-between">
                <dt className="text-zinc-500">Cancelled on</dt>
                <dd>{format(new Date(sub.cancelledAt), "MMM d, yyyy")}</dd>
              </div>
            )}
          </dl>
        )}

        <div className="flex gap-2 pt-2">
          <Button onClick={() => setChangePlanOpen(true)} disabled={subLoading}>
            Change plan
          </Button>
          {isActive && sub?.status !== "Trial" && (
            <Button
              variant="outline"
              className="text-red-600 hover:text-red-700"
              onClick={() => setCancelOpen(true)}
            >
              Cancel subscription
            </Button>
          )}
        </div>
      </section>

      {/* Usage */}
      <section className="rounded-xl border bg-white p-6 space-y-6">
        <h2 className="font-semibold text-zinc-900">Usage this period</h2>

        {usageLoading ? (
          <div className="space-y-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full" />
            ))}
          </div>
        ) : usage ? (
          <div className="space-y-5">
            <UsageMeter
              label="Booking types"
              used={usage.bookingTypesUsed}
              limit={usage.bookingTypesLimit}
            />
            <UsageMeter
              label="Staff members"
              used={usage.staffMembersUsed}
              limit={usage.staffMembersLimit}
            />
            <UsageMeter
              label="Bookings this month"
              used={usage.bookingsThisMonth}
              limit={usage.bookingsPerMonthLimit}
            />
            <UsageMeter
              label="Customers"
              used={usage.customersUsed}
              limit={usage.customersLimit}
            />
          </div>
        ) : (
          <p className="text-sm text-zinc-500">Usage data unavailable.</p>
        )}
      </section>

      {/* Dialogs */}
      {sub && (
        <ChangePlanDialog
          open={changePlanOpen}
          onOpenChange={setChangePlanOpen}
          currentPlanId={sub.planId}
        />
      )}
      <CancelDialog open={cancelOpen} onOpenChange={setCancelOpen} />
    </div>
  );
}

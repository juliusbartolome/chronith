"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { usePlans, type TenantPlanDto } from "@/hooks/use-plans";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { formatPrice } from "@/lib/format";
import { Check } from "lucide-react";

// ── Schema ───────────────────────────────────────────────────────────────────

const accountSchema = z
  .object({
    tenantName: z.string().min(1, "Business name is required").max(200),
    tenantSlug: z
      .string()
      .min(1, "Slug is required")
      .max(100)
      .regex(/^[a-z0-9-]+$/, "Slug may only contain lowercase letters, digits, and hyphens"),
    email: z.string().email("Valid email required"),
    password: z.string().min(8, "Password must be at least 8 characters").max(128),
    confirmPassword: z.string(),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: "Passwords do not match",
    path: ["confirmPassword"],
  });

type AccountForm = z.infer<typeof accountSchema>;

// ── Helpers ───────────────────────────────────────────────────────────────────

const STEPS = ["Account", "Plan", "Review"] as const;

function StepIndicator({ step }: { step: number }) {
  return (
    <div className="mb-8 flex items-center justify-center gap-2">
      {STEPS.map((label, i) => (
        <div key={label} className="flex items-center gap-2">
          <div
            className={cn(
              "flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium",
              i < step
                ? "bg-zinc-900 text-white"
                : i === step
                  ? "border-2 border-zinc-900 text-zinc-900"
                  : "border-2 border-zinc-300 text-zinc-400",
            )}
          >
            {i < step ? <Check className="h-4 w-4" /> : i + 1}
          </div>
          <span
            className={cn(
              "text-sm font-medium",
              i === step ? "text-zinc-900" : "text-zinc-400",
            )}
          >
            {label}
          </span>
          {i < STEPS.length - 1 && (
            <div className="mx-2 h-px w-8 bg-zinc-200" />
          )}
        </div>
      ))}
    </div>
  );
}

// ── Step 1: Account ───────────────────────────────────────────────────────────

function AccountStep({
  onNext,
  defaultValues,
}: {
  onNext: (data: AccountForm) => void;
  defaultValues?: Partial<AccountForm>;
}) {
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<AccountForm>({
    resolver: zodResolver(accountSchema),
    defaultValues,
  });

  function slugify(name: string) {
    return name
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "");
  }

  return (
    <form onSubmit={handleSubmit(onNext)} className="space-y-4">
      <div className="space-y-1">
        <Label htmlFor="tenantName">Business name</Label>
        <Input
          id="tenantName"
          {...register("tenantName", {
            onChange: (e) => setValue("tenantSlug", slugify(e.target.value)),
          })}
          placeholder="Acme Barbershop"
        />
        {errors.tenantName && (
          <p className="text-sm text-red-500">{errors.tenantName.message}</p>
        )}
      </div>

      <div className="space-y-1">
        <Label htmlFor="tenantSlug">Slug</Label>
        <div className="flex items-center gap-1">
          <span className="text-sm text-zinc-500">chronith.app/</span>
          <Input id="tenantSlug" {...register("tenantSlug")} placeholder="acme-barbershop" />
        </div>
        {errors.tenantSlug && (
          <p className="text-sm text-red-500">{errors.tenantSlug.message}</p>
        )}
      </div>

      <div className="space-y-1">
        <Label htmlFor="email">Admin email</Label>
        <Input
          id="email"
          type="email"
          {...register("email")}
          placeholder="admin@example.com"
        />
        {errors.email && (
          <p className="text-sm text-red-500">{errors.email.message}</p>
        )}
      </div>

      <div className="space-y-1">
        <Label htmlFor="password">Password</Label>
        <Input
          id="password"
          type="password"
          {...register("password")}
          placeholder="••••••••"
        />
        {errors.password && (
          <p className="text-sm text-red-500">{errors.password.message}</p>
        )}
      </div>

      <div className="space-y-1">
        <Label htmlFor="confirmPassword">Confirm password</Label>
        <Input
          id="confirmPassword"
          type="password"
          {...register("confirmPassword")}
          placeholder="••••••••"
        />
        {errors.confirmPassword && (
          <p className="text-sm text-red-500">{errors.confirmPassword.message}</p>
        )}
      </div>

      <Button type="submit" className="w-full">
        Next
      </Button>
    </form>
  );
}

// ── Step 2: Plan selection ─────────────────────────────────────────────────────

function PlanStep({
  onNext,
  onBack,
  selectedPlanId,
}: {
  onNext: (planId: string) => void;
  onBack: () => void;
  selectedPlanId?: string;
}) {
  const { data: plans = [], isLoading } = usePlans();
  const [selected, setSelected] = useState<string>(selectedPlanId ?? "");

  const sorted = [...plans].sort((a, b) => a.sortOrder - b.sortOrder);

  if (isLoading) {
    return <div className="text-center text-sm text-zinc-500">Loading plans…</div>;
  }

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {sorted.map((plan) => (
          <PlanCard
            key={plan.id}
            plan={plan}
            selected={selected === plan.id}
            onSelect={() => setSelected(plan.id)}
          />
        ))}
      </div>

      <div className="flex gap-2">
        <Button variant="outline" onClick={onBack} className="flex-1">
          Back
        </Button>
        <Button
          className="flex-1"
          disabled={!selected}
          onClick={() => onNext(selected)}
        >
          Next
        </Button>
      </div>
    </div>
  );
}

function PlanCard({
  plan,
  selected,
  onSelect,
}: {
  plan: TenantPlanDto;
  selected: boolean;
  onSelect: () => void;
}) {
  const isFree = plan.priceCentavos === 0;

  return (
    <button
      type="button"
      onClick={onSelect}
      className={cn(
        "rounded-lg border p-4 text-left transition-all",
        selected
          ? "border-zinc-900 bg-zinc-900 text-white"
          : "border-zinc-200 hover:border-zinc-400",
      )}
    >
      <div className="mb-2 flex items-center justify-between">
        <span className="font-semibold">{plan.name}</span>
        {isFree && (
          <Badge variant={selected ? "outline" : "secondary"}>Free</Badge>
        )}
      </div>
      <p className="text-2xl font-bold">
        {isFree ? "₱0" : formatPrice(plan.priceCentavos)}
        {!isFree && <span className="text-sm font-normal">/mo</span>}
      </p>
      <ul className="mt-3 space-y-1 text-sm">
        <li>{plan.maxBookingTypes} booking types</li>
        <li>{plan.maxStaffMembers} staff members</li>
        <li>{plan.maxBookingsPerMonth} bookings/month</li>
        {plan.analyticsEnabled && <li>Analytics</li>}
        {plan.notificationsEnabled && <li>Notifications</li>}
        {plan.apiAccessEnabled && <li>API access</li>}
      </ul>
    </button>
  );
}

// ── Step 3: Review & Submit ───────────────────────────────────────────────────

function ReviewStep({
  accountData,
  planId,
  onBack,
  onSubmit,
  isSubmitting,
}: {
  accountData: AccountForm;
  planId: string;
  onBack: () => void;
  onSubmit: () => void;
  isSubmitting: boolean;
}) {
  const { data: plans = [] } = usePlans();
  const plan = plans.find((p) => p.id === planId);

  return (
    <div className="space-y-6">
      <div className="rounded-lg border p-4 space-y-3">
        <h3 className="font-semibold text-zinc-900">Account details</h3>
        <dl className="space-y-1 text-sm">
          <div className="flex justify-between">
            <dt className="text-zinc-500">Business</dt>
            <dd>{accountData.tenantName}</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-zinc-500">Slug</dt>
            <dd>{accountData.tenantSlug}</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-zinc-500">Email</dt>
            <dd>{accountData.email}</dd>
          </div>
        </dl>
      </div>

      {plan && (
        <div className="rounded-lg border p-4 space-y-2">
          <h3 className="font-semibold text-zinc-900">Selected plan</h3>
          <div className="flex items-center justify-between text-sm">
            <span>{plan.name}</span>
            <span className="font-semibold">
              {plan.priceCentavos === 0 ? "Free" : `${formatPrice(plan.priceCentavos)}/mo`}
            </span>
          </div>
        </div>
      )}

      <div className="flex gap-2">
        <Button variant="outline" onClick={onBack} className="flex-1" disabled={isSubmitting}>
          Back
        </Button>
        <Button className="flex-1" onClick={onSubmit} disabled={isSubmitting}>
          {isSubmitting ? "Creating account…" : "Create account"}
        </Button>
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function SignupPage() {
  const router = useRouter();
  const [step, setStep] = useState(0);
  const [accountData, setAccountData] = useState<AccountForm | null>(null);
  const [selectedPlanId, setSelectedPlanId] = useState<string>("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    if (!accountData) return;
    setIsSubmitting(true);
    try {
      const res = await fetch("/api/signup", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          tenantName: accountData.tenantName,
          tenantSlug: accountData.tenantSlug,
          email: accountData.email,
          password: accountData.password,
          timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
        }),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ title: "Signup failed" }));
        toast.error(err.title ?? "Signup failed");
        return;
      }

      toast.success("Account created! Please log in.");
      router.push("/login");
    } catch {
      toast.error("An unexpected error occurred");
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-lg">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-zinc-900">Create your account</h1>
          <p className="mt-2 text-sm text-zinc-500">
            Get started with Chronith — it only takes a minute.
          </p>
        </div>

        <div className="rounded-xl border bg-white p-8 shadow-sm">
          <StepIndicator step={step} />

          {step === 0 && (
            <AccountStep
              defaultValues={accountData ?? undefined}
              onNext={(data) => {
                setAccountData(data);
                setStep(1);
              }}
            />
          )}

          {step === 1 && (
            <PlanStep
              selectedPlanId={selectedPlanId}
              onNext={(planId) => {
                setSelectedPlanId(planId);
                setStep(2);
              }}
              onBack={() => setStep(0)}
            />
          )}

          {step === 2 && accountData && (
            <ReviewStep
              accountData={accountData}
              planId={selectedPlanId}
              onBack={() => setStep(1)}
              onSubmit={handleSubmit}
              isSubmitting={isSubmitting}
            />
          )}
        </div>

        <p className="mt-4 text-center text-sm text-zinc-500">
          Already have an account?{" "}
          <a href="/login" className="font-medium text-zinc-900 hover:underline">
            Sign in
          </a>
        </p>
      </div>
    </div>
  );
}

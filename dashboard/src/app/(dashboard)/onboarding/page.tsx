"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import {
  Building2,
  CalendarDays,
  Users,
  Bell,
  Rocket,
  Check,
} from "lucide-react";

// ── Constants ─────────────────────────────────────────────────────────────────

const STORAGE_KEY = "chronith-onboarding-step";

const STEPS = [
  {
    id: "welcome",
    title: "Welcome to Chronith",
    icon: Building2,
    description:
      "Let's get your booking system set up. This wizard will guide you through the key steps to get up and running quickly.",
  },
  {
    id: "booking-type",
    title: "Create your first booking type",
    icon: CalendarDays,
    description:
      'Booking types define the services you offer — e.g. "Haircut (30 min)" or "Consultation (1 hr)". Create at least one to start accepting bookings.',
    action: {
      label: "Create booking type",
      href: "/booking-types/new",
    },
  },
  {
    id: "staff",
    title: "Add your staff",
    icon: Users,
    description:
      "Add staff members who will be providing services. You can assign them availability windows and link them to booking types.",
    action: {
      label: "Add staff member",
      href: "/staff/new",
    },
  },
  {
    id: "notifications",
    title: "Set up notifications",
    icon: Bell,
    description:
      "Configure email, SMS, or push notifications to keep your customers informed about their bookings. You can customise templates later.",
    action: {
      label: "Configure notifications",
      href: "/notifications",
    },
  },
  {
    id: "done",
    title: "You're all set!",
    icon: Rocket,
    description:
      "Your booking system is ready to go. Share your booking page with customers or explore the dashboard to dive deeper.",
    action: {
      label: "Go to dashboard",
      href: "/bookings",
    },
  },
] as const;

// ── Step indicator ─────────────────────────────────────────────────────────────

function StepList({
  current,
  completed,
}: {
  current: number;
  completed: Set<number>;
}) {
  return (
    <ol className="space-y-2">
      {STEPS.map((s, i) => {
        const Icon = s.icon;
        const done = completed.has(i);
        const active = i === current;
        return (
          <li
            key={s.id}
            className={cn(
              "flex items-center gap-3 rounded-lg px-3 py-2 text-sm",
              active && "bg-zinc-100 font-medium text-zinc-900",
              !active && done && "text-zinc-400",
              !active && !done && "text-zinc-500",
            )}
          >
            <div
              className={cn(
                "flex h-7 w-7 shrink-0 items-center justify-center rounded-full",
                done
                  ? "bg-zinc-900 text-white"
                  : active
                    ? "border-2 border-zinc-900 text-zinc-900"
                    : "border-2 border-zinc-300 text-zinc-400",
              )}
            >
              {done ? (
                <Check className="h-3.5 w-3.5" />
              ) : (
                <Icon className="h-3.5 w-3.5" />
              )}
            </div>
            {s.title}
          </li>
        );
      })}
    </ol>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function OnboardingPage() {
  const router = useRouter();
  const [current, setCurrent] = useState(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved) as {
          step: number;
          completed: number[];
        };
        return parsed.step ?? 0;
      }
    } catch {
      // ignore
    }
    return 0;
  });
  const [completed, setCompleted] = useState<Set<number>>(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved) as {
          step: number;
          completed: number[];
        };
        return new Set(parsed.completed ?? []);
      }
    } catch {
      // ignore
    }
    return new Set();
  });

  // Persist progress to localStorage
  function saveProgress(step: number, done: Set<number>) {
    try {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({ step, completed: Array.from(done) }),
      );
    } catch {
      // ignore
    }
  }

  function advance() {
    const next = current + 1;
    const newCompleted = new Set(completed).add(current);
    if (next >= STEPS.length) {
      // Finished — clear and redirect
      localStorage.removeItem(STORAGE_KEY);
      router.push("/bookings");
      return;
    }
    setCurrent(next);
    setCompleted(newCompleted);
    saveProgress(next, newCompleted);
  }

  const step = STEPS[current];
  const Icon = step.icon;
  const progressPct = Math.round((completed.size / STEPS.length) * 100);

  return (
    <div className="flex min-h-[calc(100vh-3.5rem)] gap-8 p-8">
      {/* Sidebar */}
      <aside className="hidden w-64 shrink-0 lg:block">
        <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-zinc-500">
          Getting started
        </h2>
        <div className="mb-4 h-1.5 w-full overflow-hidden rounded-full bg-zinc-200">
          <div
            className="h-full rounded-full bg-zinc-900 transition-all"
            style={{ width: `${progressPct}%` }}
          />
        </div>
        <p className="mb-4 text-xs text-zinc-500">
          {completed.size} of {STEPS.length} steps completed
        </p>
        <StepList current={current} completed={completed} />
      </aside>

      {/* Main content */}
      <main className="flex flex-1 flex-col items-center justify-center">
        <div className="w-full max-w-lg rounded-xl border bg-white p-10 shadow-sm">
          <div className="mb-6 flex h-14 w-14 items-center justify-center rounded-full bg-zinc-100">
            <Icon className="h-7 w-7 text-zinc-700" />
          </div>

          <h1 className="mb-3 text-2xl font-bold text-zinc-900">
            {step.title}
          </h1>
          <p className="mb-8 text-sm text-zinc-500 leading-relaxed">
            {step.description}
          </p>

          <div className="flex gap-3">
            {"action" in step && step.action && (
              <Button
                variant="outline"
                onClick={() => router.push(step.action!.href)}
                className="flex-1"
              >
                {step.action.label}
              </Button>
            )}
            <Button onClick={advance} className="flex-1">
              {current === STEPS.length - 1 ? "Finish" : "Next"}
            </Button>
          </div>

          {current < STEPS.length - 1 && (
            <button
              type="button"
              onClick={() => {
                localStorage.removeItem(STORAGE_KEY);
                router.push("/bookings");
              }}
              className="mt-4 w-full text-center text-xs text-zinc-400 hover:text-zinc-600"
            >
              Skip setup
            </button>
          )}
        </div>
      </main>
    </div>
  );
}

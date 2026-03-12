"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  CalendarCheck,
  Users,
  Layers,
  BarChart3,
  Bell,
  Settings,
  UserRound,
  ShieldCheck,
  RefreshCw,
} from "lucide-react";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { href: "/bookings", label: "Bookings", icon: CalendarCheck },
  { href: "/customers", label: "Customers", icon: UserRound },
  { href: "/staff", label: "Staff", icon: Users },
  { href: "/booking-types", label: "Booking Types", icon: Layers },
  { href: "/analytics", label: "Analytics", icon: BarChart3 },
  { href: "/audit", label: "Audit Log", icon: ShieldCheck },
  { href: "/recurring", label: "Recurring", icon: RefreshCw },
  { href: "/notifications", label: "Notifications", icon: Bell },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function Sidebar() {
  const pathname = usePathname();

  return (
    <aside className="flex w-60 flex-col border-r bg-white">
      <div className="flex h-14 items-center border-b px-6">
        <span className="font-semibold text-zinc-900">Chronith</span>
      </div>
      <nav className="flex-1 space-y-1 p-3">
        {NAV_ITEMS.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            className={cn(
              "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
              pathname.startsWith(href)
                ? "bg-zinc-100 text-zinc-900"
                : "text-zinc-600 hover:bg-zinc-50 hover:text-zinc-900",
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </Link>
        ))}
      </nav>
    </aside>
  );
}

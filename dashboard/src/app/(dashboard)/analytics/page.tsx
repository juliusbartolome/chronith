"use client";

import { useState } from "react";
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Legend,
} from "recharts";
import { KpiCard } from "@/components/analytics/kpi-card";
import {
  useBookingAnalytics,
  useRevenueAnalytics,
  useUtilizationAnalytics,
} from "@/hooks/use-analytics";

type Tab = "Bookings" | "Revenue" | "Utilization";
const TABS: Tab[] = ["Bookings", "Revenue", "Utilization"];

const PIE_COLORS = ["#6366f1", "#f59e0b", "#10b981", "#ef4444", "#3b82f6"];

function formatPhp(centavos: number): string {
  return `₱${(centavos / 100).toFixed(2)}`;
}

function formatPct(rate: number): string {
  return `${(rate * 100).toFixed(1)}%`;
}

function BookingsTab() {
  const { data, isLoading, error } = useBookingAnalytics();

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;
  if (error || !data)
    return <p className="text-sm text-red-600">Failed to load analytics.</p>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <KpiCard label="Total Bookings" value={data.totalBookings} />
        <KpiCard
          label="Confirmed Rate"
          value={formatPct(data.confirmedRate)}
        />
        <KpiCard
          label="Cancellation Rate"
          value={formatPct(data.cancellationRate)}
        />
        <KpiCard label="Avg / Day" value={data.averagePerDay.toFixed(1)} />
      </div>

      <div className="rounded-lg border bg-card p-6">
        <h2 className="mb-4 text-sm font-semibold">Bookings Over Time</h2>
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={data.overTime}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" tick={{ fontSize: 12 }} />
            <YAxis tick={{ fontSize: 12 }} />
            <Tooltip />
            <Line
              type="monotone"
              dataKey="count"
              stroke="#6366f1"
              strokeWidth={2}
              dot={false}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="rounded-lg border bg-card p-6">
          <h2 className="mb-4 text-sm font-semibold">By Status</h2>
          <ResponsiveContainer width="100%" height={200}>
            <PieChart>
              <Pie
                data={data.byStatus}
                dataKey="count"
                nameKey="status"
                cx="50%"
                cy="50%"
                outerRadius={70}
                label
              >
                {data.byStatus.map((_entry, index) => (
                  <Cell
                    key={`cell-${index}`}
                    fill={PIE_COLORS[index % PIE_COLORS.length]}
                  />
                ))}
              </Pie>
              <Tooltip />
              <Legend />
            </PieChart>
          </ResponsiveContainer>
        </div>

        <div className="rounded-lg border bg-card p-6">
          <h2 className="mb-4 text-sm font-semibold">By Booking Type</h2>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={data.byBookingType} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis type="number" tick={{ fontSize: 12 }} />
              <YAxis dataKey="name" type="category" tick={{ fontSize: 12 }} />
              <Tooltip />
              <Bar dataKey="count" fill="#6366f1" />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="rounded-lg border bg-card p-6">
          <h2 className="mb-4 text-sm font-semibold">By Staff Member</h2>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={data.byStaffMember} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis type="number" tick={{ fontSize: 12 }} />
              <YAxis dataKey="name" type="category" tick={{ fontSize: 12 }} />
              <Tooltip />
              <Bar dataKey="count" fill="#10b981" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}

function RevenueTab() {
  const { data, isLoading, error } = useRevenueAnalytics();

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;
  if (error || !data)
    return <p className="text-sm text-red-600">Failed to load analytics.</p>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4">
        <KpiCard
          label="Total Revenue"
          value={formatPhp(data.totalRevenueCentavos)}
        />
        <KpiCard
          label="Avg Booking Value"
          value={formatPhp(data.averageBookingValueCentavos)}
        />
      </div>

      <div className="rounded-lg border bg-card p-6">
        <h2 className="mb-4 text-sm font-semibold">Revenue Over Time</h2>
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={data.overTime}>
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis dataKey="date" tick={{ fontSize: 12 }} />
            <YAxis
              tickFormatter={(v) => `₱${(Number(v) / 100).toFixed(0)}`}
              tick={{ fontSize: 12 }}
            />
            <Tooltip
              formatter={(v) => formatPhp(Number(v))}
            />
            <Line
              type="monotone"
              dataKey="amountCentavos"
              stroke="#f59e0b"
              strokeWidth={2}
              dot={false}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>

      <div className="rounded-lg border bg-card p-6">
        <h2 className="mb-4 text-sm font-semibold">Revenue by Booking Type</h2>
        <ResponsiveContainer width="100%" height={240}>
          <BarChart data={data.byBookingType} layout="vertical">
            <CartesianGrid strokeDasharray="3 3" />
            <XAxis
              type="number"
              tickFormatter={(v) => `₱${(Number(v) / 100).toFixed(0)}`}
              tick={{ fontSize: 12 }}
            />
            <YAxis dataKey="name" type="category" tick={{ fontSize: 12 }} />
            <Tooltip formatter={(v) => formatPhp(Number(v))} />
            <Bar dataKey="amountCentavos" fill="#f59e0b" />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

function UtilizationTab() {
  const { data, isLoading, error } = useUtilizationAnalytics();

  if (isLoading) return <p className="text-sm text-zinc-500">Loading…</p>;
  if (error || !data)
    return <p className="text-sm text-red-600">Failed to load analytics.</p>;

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-3 gap-4">
        <KpiCard
          label="Overall Utilization"
          value={formatPct(data.overallUtilizationRate)}
        />
        <KpiCard label="Busiest Day" value={data.busiestDayOfWeek} />
        <KpiCard label="Busiest Time Slot" value={data.busiestTimeSlot} />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="rounded-lg border bg-card p-6">
          <h2 className="mb-4 text-sm font-semibold">
            Fill Rate by Booking Type
          </h2>
          <ResponsiveContainer width="100%" height={240}>
            <BarChart data={data.byBookingType} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                type="number"
                domain={[0, 1]}
                tickFormatter={(v) => `${(Number(v) * 100).toFixed(0)}%`}
                tick={{ fontSize: 12 }}
              />
              <YAxis dataKey="name" type="category" tick={{ fontSize: 12 }} />
              <Tooltip formatter={(v) => formatPct(Number(v))} />
              <Bar dataKey="fillRate" fill="#6366f1" />
            </BarChart>
          </ResponsiveContainer>
        </div>

        <div className="rounded-lg border bg-card p-6">
          <h2 className="mb-4 text-sm font-semibold">
            Utilization by Staff Member
          </h2>
          <ResponsiveContainer width="100%" height={240}>
            <BarChart data={data.byStaffMember} layout="vertical">
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                type="number"
                domain={[0, 1]}
                tickFormatter={(v) => `${(Number(v) * 100).toFixed(0)}%`}
                tick={{ fontSize: 12 }}
              />
              <YAxis dataKey="name" type="category" tick={{ fontSize: 12 }} />
              <Tooltip formatter={(v) => formatPct(Number(v))} />
              <Bar dataKey="utilizationRate" fill="#10b981" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
}

export default function AnalyticsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("Bookings");

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold text-zinc-900">Analytics</h1>

      <div role="tablist" className="flex gap-1 border-b">
        {TABS.map((tab) => (
          <button
            key={tab}
            role="tab"
            aria-selected={activeTab === tab}
            onClick={() => setActiveTab(tab)}
            className={`px-4 py-2 text-sm font-medium transition-colors ${
              activeTab === tab
                ? "border-b-2 border-indigo-600 text-indigo-600"
                : "text-zinc-500 hover:text-zinc-900"
            }`}
          >
            {tab}
          </button>
        ))}
      </div>

      {activeTab === "Bookings" && <BookingsTab />}
      {activeTab === "Revenue" && <RevenueTab />}
      {activeTab === "Utilization" && <UtilizationTab />}
    </div>
  );
}

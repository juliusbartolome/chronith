"use client";

import { useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import {
  CheckCircle2,
  Clock,
  XCircle,
  AlertCircle,
  FileImage,
  ExternalLink,
} from "lucide-react";
import { useBookingSession } from "@/lib/booking-session";
import { useBookingStatus } from "@/hooks/use-manual-payment";
import { formatPrice } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";

function buildGoogleCalendarUrl(params: {
  title: string;
  date: string;
  startTime: string;
  durationMinutes: number;
  description?: string;
}): string {
  const start = `${params.date.replace(/-/g, "")}T${params.startTime.replace(/:/g, "")}00`;
  const [sh, sm] = params.startTime.split(":").map(Number);
  const totalMinutes = sh * 60 + sm + params.durationMinutes;
  const endH = Math.floor(totalMinutes / 60) % 24;
  const endM = totalMinutes % 60;
  const end = `${params.date.replace(/-/g, "")}T${String(endH).padStart(2, "0")}${String(endM).padStart(2, "0")}00`;
  const url = new URL("https://calendar.google.com/calendar/render");
  url.searchParams.set("action", "TEMPLATE");
  url.searchParams.set("text", params.title);
  url.searchParams.set("dates", `${start}/${end}`);
  if (params.description) url.searchParams.set("details", params.description);
  return url.toString();
}

function generateIcalContent(params: {
  uid: string;
  title: string;
  date: string;
  startTime: string;
  durationMinutes: number;
  description?: string;
}): string {
  const start = `${params.date.replace(/-/g, "")}T${params.startTime.replace(/:/g, "")}00`;
  const [sh, sm] = params.startTime.split(":").map(Number);
  const totalMinutes = sh * 60 + sm + params.durationMinutes;
  const endH = Math.floor(totalMinutes / 60) % 24;
  const endM = totalMinutes % 60;
  const end = `${params.date.replace(/-/g, "")}T${String(endH).padStart(2, "0")}${String(endM).padStart(2, "0")}00`;

  return [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//Chronith//Chronith Booking//EN",
    "BEGIN:VEVENT",
    `UID:${params.uid}@chronith`,
    `DTSTAMP:${new Date().toISOString().replace(/[-:]/g, "").split(".")[0]}Z`,
    `DTSTART:${start}`,
    `DTEND:${end}`,
    `SUMMARY:${params.title}`,
    params.description ? `DESCRIPTION:${params.description}` : "",
    "END:VEVENT",
    "END:VCALENDAR",
  ]
    .filter(Boolean)
    .join("\r\n");
}

function isSafeHttpUrl(url: string): boolean {
  return /^https?:\/\//i.test(url);
}

function getConfirmedDescription(email?: string): string {
  return email
    ? `Your appointment has been successfully booked. A confirmation email will be sent to ${email}.`
    : "Your appointment has been successfully booked. A confirmation email will be sent shortly.";
}

const statusConfig = {
  Confirmed: {
    icon: CheckCircle2,
    iconBg: "bg-green-100",
    iconColor: "text-green-600",
    title: "Your booking is confirmed!",
    description:
      "Your appointment has been successfully booked. A confirmation email will be sent shortly.",
    badgeVariant: "outline" as const,
    badgeClass: "text-green-600 border-green-300",
    badgeLabel: "Confirmed",
  },
  PendingVerification: {
    icon: Clock,
    iconBg: "bg-blue-100",
    iconColor: "text-blue-600",
    title: "Payment submitted!",
    description:
      "Waiting for staff verification. You'll receive a confirmation once your payment has been reviewed.",
    badgeVariant: "outline" as const,
    badgeClass: "text-blue-600 border-blue-300",
    badgeLabel: "Pending Verification",
  },
  PendingPayment: {
    icon: AlertCircle,
    iconBg: "bg-amber-100",
    iconColor: "text-amber-600",
    title: "Please complete your payment.",
    description:
      "Your booking is reserved but not yet paid. Complete your payment to confirm your booking.",
    badgeVariant: "outline" as const,
    badgeClass: "text-amber-600 border-amber-300",
    badgeLabel: "Awaiting Payment",
  },
  Cancelled: {
    icon: XCircle,
    iconBg: "bg-red-100",
    iconColor: "text-red-600",
    title: "This booking has been cancelled.",
    description:
      "This booking is no longer active. If you believe this is an error, please contact the business.",
    badgeVariant: "destructive" as const,
    badgeClass: "",
    badgeLabel: "Cancelled",
  },
  PaymentFailed: {
    icon: XCircle,
    iconBg: "bg-red-100",
    iconColor: "text-red-600",
    title: "Payment failed. Please try again.",
    description:
      "There was an issue processing your payment. Check your email for the payment link to try again.",
    badgeVariant: "destructive" as const,
    badgeClass: "",
    badgeLabel: "Payment Failed",
  },
} as const;

export default function SuccessPage() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const router = useRouter();

  const session = useBookingSession();
  const resetSession = useBookingSession((s) => s.resetSession);

  const bookingId = session.confirmedBookingId;

  const {
    data: booking,
    isLoading: isStatusLoading,
    error: statusError,
  } = useBookingStatus(tenantSlug, bookingId ?? "", !!bookingId);

  // Guard: no confirmed booking
  useEffect(() => {
    if (!session.confirmedBookingId) {
      router.replace(`/book/${tenantSlug}/${btSlug}`);
    }
  }, [session.confirmedBookingId, router, tenantSlug, btSlug]);

  if (!bookingId) return null;

  const handleBookAnother = () => {
    resetSession();
    router.push(`/book/${tenantSlug}`);
  };

  const handleDownloadIcal = () => {
    if (!session.selectedDate || !session.selectedSlot) return;
    const content = generateIcalContent({
      uid: bookingId ?? "booking",
      title: session.btName,
      date: session.selectedDate,
      startTime: session.selectedSlot,
      durationMinutes: session.durationMinutes,
    });
    const blob = new Blob([content], { type: "text/calendar;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `booking-${bookingId ?? "confirmation"}.ics`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const googleCalUrl =
    session.selectedDate && session.selectedSlot
      ? buildGoogleCalendarUrl({
          title: session.btName,
          date: session.selectedDate,
          startTime: session.selectedSlot,
          durationMinutes: session.durationMinutes,
        })
      : null;

  // Loading state while fetching booking status
  if (isStatusLoading) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <Skeleton className="h-16 w-16 rounded-full" />
        </div>
        <Skeleton className="h-7 w-64 mx-auto mb-2" />
        <Skeleton className="h-4 w-80 mx-auto mb-8" />
        <Skeleton className="h-[140px] w-full rounded-xl mb-8" />
        <Skeleton className="h-10 w-full rounded-md" />
      </div>
    );
  }

  // Error state — fetch failed
  if (statusError && !booking) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6 flex justify-center">
          <div className="h-16 w-16 rounded-full bg-red-100 flex items-center justify-center">
            <XCircle className="h-8 w-8 text-red-600" />
          </div>
        </div>
        <h1 className="text-2xl font-bold text-zinc-900 mb-2">
          Unable to load booking status
        </h1>
        <p className="text-zinc-500 text-sm mb-8">
          We couldn&apos;t retrieve the status of your booking. Please try
          refreshing the page.
        </p>
        <Button onClick={handleBookAnother} className="w-full">
          Book Another Appointment
        </Button>
      </div>
    );
  }

  // Determine which status config to use.
  // If booking data hasn't returned yet (unlikely after loading state), fall
  // back to Confirmed (preserves original behavior — the booking was just created).
  const status = booking?.status ?? "Confirmed";
  const config = statusConfig[status];
  const StatusIcon = config.icon;

  const showCalendarExport = status === "Confirmed";
  const proofOfPaymentUrl =
    booking?.proofOfPaymentUrl &&
    status !== "Cancelled" &&
    isSafeHttpUrl(booking.proofOfPaymentUrl)
      ? booking.proofOfPaymentUrl
      : null;
  const safeCheckoutUrl =
    status === "PendingPayment" &&
    booking?.checkoutUrl &&
    isSafeHttpUrl(booking.checkoutUrl)
      ? booking.checkoutUrl
      : null;

  return (
    <div className="max-w-lg mx-auto px-4 py-16 text-center">
      {/* Status Icon */}
      <div className="mb-6 flex justify-center">
        <div
          className={`h-16 w-16 rounded-full ${config.iconBg} flex items-center justify-center`}
        >
          <StatusIcon className={`h-8 w-8 ${config.iconColor}`} />
        </div>
      </div>

      {/* Status Badge (when we have live data) */}
      {booking && !statusError && (
        <div className="mb-4 flex justify-center">
          <Badge variant={config.badgeVariant} className={config.badgeClass}>
            {config.badgeLabel}
          </Badge>
        </div>
      )}

      {/* Title & Description */}
      <h1 className="text-2xl font-bold text-zinc-900 mb-2">{config.title}</h1>
      <p className="text-zinc-500 text-sm mb-8">
        {status === "Confirmed"
          ? getConfirmedDescription(session.customerInfo?.email)
          : config.description}
      </p>

      {/* Payment Page Link — only for PendingPayment when checkoutUrl is available */}
      {safeCheckoutUrl && (
        <div className="mb-8">
          <a
            href={safeCheckoutUrl}
            className="inline-flex items-center justify-center rounded-md bg-amber-600 px-6 py-2.5 text-sm font-medium text-white hover:bg-amber-700 transition-colors"
          >
            Go to Payment Page
          </a>
        </div>
      )}

      {/* Booking Summary Card */}
      <Card className="text-left mb-8">
        <CardContent className="pt-4 space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-zinc-500">Booking ID</span>
            <span className="font-mono text-xs text-zinc-700">
              {bookingId?.replace(/-/g, "").slice(0, 8)}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Service</span>
            <span className="font-medium text-zinc-900">{session.btName}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Date</span>
            <span className="font-medium text-zinc-900">
              {session.selectedDate}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Time</span>
            <span className="font-medium text-zinc-900">
              {session.selectedSlot}
            </span>
          </div>
          {session.selectedStaffName && (
            <p>
              <span className="text-zinc-500">Staff:</span>{" "}
              {session.selectedStaffName}
            </p>
          )}
          {booking && booking.amountInCentavos > 0 && (
            <div className="flex justify-between">
              <span className="text-zinc-500">Amount</span>
              <span className="font-medium text-zinc-900">
                {formatPrice(booking.amountInCentavos)}
              </span>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Proof of Payment Display */}
      {proofOfPaymentUrl && (
        <Card className="text-left mb-8">
          <CardContent className="pt-4 space-y-3">
            <p className="text-sm font-medium text-zinc-700">
              Proof of Payment
            </p>
            <div className="flex items-center gap-3">
              <div className="rounded-lg border overflow-hidden bg-zinc-50 shrink-0">
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={proofOfPaymentUrl}
                  alt="Proof of payment"
                  className="h-20 w-20 object-cover"
                />
              </div>
              <div className="min-w-0 flex-1">
                {booking?.proofOfPaymentFileName && (
                  <div className="flex items-center gap-1.5 mb-1">
                    <FileImage className="h-3.5 w-3.5 text-zinc-400 shrink-0" />
                    <span className="text-xs text-zinc-600 truncate">
                      {booking.proofOfPaymentFileName}
                    </span>
                  </div>
                )}
                <a
                  href={proofOfPaymentUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1 text-sm font-medium text-blue-600 hover:text-blue-700"
                >
                  View proof
                  <ExternalLink className="h-3.5 w-3.5" />
                </a>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Calendar Export — only for Confirmed bookings */}
      {showCalendarExport && (
        <div className="space-y-2 mb-8">
          <p className="text-sm font-medium text-zinc-700">Add to Calendar</p>
          <div className="flex flex-col sm:flex-row gap-2 justify-center">
            {googleCalUrl && (
              <a
                href={googleCalUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center justify-center rounded-md border px-4 py-2 text-sm font-medium hover:bg-zinc-50"
              >
                Google Calendar
              </a>
            )}
            <Button variant="outline" onClick={handleDownloadIcal}>
              Apple / Outlook (.ics)
            </Button>
          </div>
        </div>
      )}

      <Button onClick={handleBookAnother} className="w-full">
        Book Another Appointment
      </Button>
    </div>
  );
}

"use client";

import { Suspense, useRef, useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import {
  CheckCircle2,
  XCircle,
  AlertCircle,
  Clock,
  Loader2,
  ShieldCheck,
  ImageIcon,
  X,
} from "lucide-react";
import { useBookingStatus } from "@/hooks/use-manual-payment";
import { useStaffVerify } from "@/hooks/use-staff-verify";
import { formatPrice } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from "@/components/ui/dialog";

function formatDateTime(isoString: string): string {
  const date = new Date(isoString);
  return date.toLocaleDateString("en-PH", {
    weekday: "short",
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function isSafeHttpUrl(url: string): boolean {
  return /^https?:\/\//i.test(url);
}

function StatusBadge({ status }: { status: string }) {
  switch (status) {
    case "PendingPayment":
      return (
        <Badge variant="outline" className="text-amber-600 border-amber-300">
          <Clock className="mr-1 h-3 w-3" />
          Awaiting Payment
        </Badge>
      );
    case "PendingVerification":
      return (
        <Badge variant="outline" className="text-blue-600 border-blue-300">
          <Clock className="mr-1 h-3 w-3" />
          Pending Verification
        </Badge>
      );
    case "Confirmed":
      return (
        <Badge variant="outline" className="text-green-600 border-green-300">
          <CheckCircle2 className="mr-1 h-3 w-3" />
          Confirmed
        </Badge>
      );
    case "Cancelled":
      return (
        <Badge variant="destructive">
          <X className="mr-1 h-3 w-3" />
          Cancelled
        </Badge>
      );
    case "PaymentFailed":
      return (
        <Badge variant="destructive">
          <AlertCircle className="mr-1 h-3 w-3" />
          Payment Failed
        </Badge>
      );
    default:
      return <Badge variant="outline">{status}</Badge>;
  }
}

export default function VerifyPage() {
  return (
    <Suspense
      fallback={
        <div className="max-w-lg mx-auto px-4 py-10">
          <Skeleton className="h-6 w-64 mb-6" />
          <Skeleton className="h-[200px] w-full rounded-xl mb-6" />
          <Skeleton className="h-[300px] w-full rounded-xl" />
        </div>
      }
    >
      <VerifyPageContent />
    </Suspense>
  );
}

function VerifyPageContent() {
  const { tenantSlug, bookingId } = useParams<{
    tenantSlug: string;
    bookingId: string;
  }>();
  const searchParams = useSearchParams();

  const expires = searchParams.get("expires") ?? "";
  const sig = searchParams.get("sig") ?? "";

  // Client-side expiry check. Computed once via useState initializer
  // to avoid react-hooks/purity rule issues with useMemo.
  const [isExpired] = useState(
    () => !!expires && Date.now() / 1000 > Number(expires),
  );

  const {
    data: booking,
    isLoading,
    error,
  } = useBookingStatus(tenantSlug, bookingId);

  const { mutateAsync: staffVerify, isPending: isSubmitting } =
    useStaffVerify();

  const [showRejectForm, setShowRejectForm] = useState(false);
  const [rejectNote, setRejectNote] = useState("");
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [actionResult, setActionResult] = useState<{
    action: "approve" | "reject";
    status: string;
  } | null>(null);
  const isSubmittingRef = useRef(false);

  const handleApprove = async () => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    try {
      const result = await staffVerify({
        tenantSlug,
        bookingId,
        expires,
        sig,
        action: "approve",
      });
      setActionResult({ action: "approve", status: result.status });
      toast.success("Booking approved successfully.");
    } catch (err) {
      toast.error(
        err instanceof Error
          ? err.message
          : "Failed to approve booking. Please try again.",
      );
    } finally {
      isSubmittingRef.current = false;
    }
  };

  const handleReject = async () => {
    if (isSubmittingRef.current) return;
    isSubmittingRef.current = true;
    try {
      const result = await staffVerify({
        tenantSlug,
        bookingId,
        expires,
        sig,
        action: "reject",
        note: rejectNote.trim() || undefined,
      });
      setActionResult({ action: "reject", status: result.status });
      toast.success("Booking rejected.");
    } catch (err) {
      toast.error(
        err instanceof Error
          ? err.message
          : "Failed to reject booking. Please try again.",
      );
    } finally {
      isSubmittingRef.current = false;
    }
  };

  // Missing HMAC params
  if (!expires || !sig) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <AlertCircle className="h-12 w-12 text-zinc-400 mx-auto mb-4" />
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          Invalid Verification Link
        </h1>
        <p className="text-sm text-zinc-500">
          This verification link is missing required parameters. Please use the
          link from your notification.
        </p>
      </div>
    );
  }

  // Expired link
  if (isExpired) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <Clock className="h-12 w-12 text-amber-400 mx-auto mb-4" />
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          This Link Has Expired
        </h1>
        <p className="text-sm text-zinc-500">
          This verification link has expired. Please request a new notification
          to verify this booking.
        </p>
      </div>
    );
  }

  // Loading
  if (isLoading) {
    return (
      <div className="max-w-lg mx-auto px-4 py-10">
        <Skeleton className="h-6 w-64 mb-6" />
        <Skeleton className="h-[200px] w-full rounded-xl mb-6" />
        <Skeleton className="h-[300px] w-full rounded-xl" />
      </div>
    );
  }

  // Error
  if (error || !booking) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <AlertCircle className="h-12 w-12 text-red-400 mx-auto mb-4" />
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          Unable to Load Booking
        </h1>
        <p className="text-sm text-zinc-500">
          {error instanceof Error
            ? error.message
            : "Something went wrong. Please try again."}
        </p>
      </div>
    );
  }

  // After action — show success result
  if (actionResult) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        {actionResult.action === "approve" ? (
          <CheckCircle2 className="h-12 w-12 text-green-500 mx-auto mb-4" />
        ) : (
          <XCircle className="h-12 w-12 text-red-500 mx-auto mb-4" />
        )}
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          {actionResult.action === "approve"
            ? "Booking Approved"
            : "Booking Rejected"}
        </h1>
        <p className="text-sm text-zinc-500 mb-6">
          {actionResult.action === "approve"
            ? "The booking has been confirmed. The customer will be notified."
            : "The booking has been rejected. The customer will be notified."}
        </p>
        <Card className="text-left">
          <CardContent className="pt-4 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-zinc-500">Reference</span>
              <span className="font-mono text-xs text-zinc-700">
                {booking.referenceId}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-500">Status</span>
              <StatusBadge status={actionResult.status} />
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Already resolved or not yet ready for verification — read-only status
  if (
    booking.status === "Confirmed" ||
    booking.status === "Cancelled" ||
    booking.status === "PaymentFailed" ||
    booking.status === "PendingPayment"
  ) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6">
          <StatusBadge status={booking.status} />
        </div>
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          {booking.status === "Confirmed"
            ? "Booking Already Confirmed"
            : booking.status === "Cancelled"
              ? "Booking Cancelled"
              : booking.status === "PendingPayment"
                ? "Awaiting Customer Payment"
                : "Payment Failed"}
        </h1>
        <p className="text-sm text-zinc-500 mb-6">
          {booking.status === "Confirmed"
            ? "This booking has already been confirmed. No further action is needed."
            : booking.status === "Cancelled"
              ? "This booking has been cancelled. No further action is needed."
              : booking.status === "PendingPayment"
                ? "The customer has not yet submitted payment. Verification will be available once payment proof is submitted."
                : "Payment for this booking has failed. No further action is needed."}
        </p>
        <Card className="text-left">
          <CardContent className="pt-4 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-zinc-500">Reference</span>
              <span className="font-mono text-xs text-zinc-700">
                {booking.referenceId}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-500">Amount</span>
              <span className="font-medium text-zinc-900">
                {formatPrice(booking.amountInCentavos)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-zinc-500">Date</span>
              <span className="font-medium text-zinc-900">
                {formatDateTime(booking.start)}
              </span>
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Main verification page — PendingVerification
  const proofUrl =
    booking.proofOfPaymentUrl && isSafeHttpUrl(booking.proofOfPaymentUrl)
      ? booking.proofOfPaymentUrl
      : null;

  return (
    <div className="max-w-lg mx-auto px-4 py-10">
      <div className="flex items-center gap-2 mb-1">
        <ShieldCheck className="h-5 w-5 text-zinc-700" />
        <h1 className="text-xl font-bold text-zinc-900">Verify Booking</h1>
      </div>
      <p className="text-sm text-zinc-500 mb-6">
        Review the booking details and payment proof below, then approve or
        reject.
      </p>

      {/* Booking Summary */}
      <Card className="mb-6">
        <CardContent className="pt-4 space-y-2 text-sm">
          <div className="flex justify-between items-center">
            <span className="text-zinc-500">Status</span>
            <StatusBadge status={booking.status} />
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Reference</span>
            <span className="font-mono text-xs text-zinc-700">
              {booking.referenceId}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-zinc-500">Date & Time</span>
            <span className="font-medium text-zinc-900">
              {formatDateTime(booking.start)}
            </span>
          </div>
          {booking.start !== booking.end && (
            <div className="flex justify-between">
              <span className="text-zinc-500">End Time</span>
              <span className="font-medium text-zinc-900">
                {formatDateTime(booking.end)}
              </span>
            </div>
          )}
          <Separator />
          <div className="flex justify-between font-semibold">
            <span className="text-zinc-700">Amount</span>
            <span className="text-zinc-900">
              {formatPrice(booking.amountInCentavos)}
            </span>
          </div>
          {booking.paymentReference && (
            <div className="flex justify-between">
              <span className="text-zinc-500">Payment Ref</span>
              <span className="font-mono text-xs text-zinc-700">
                {booking.paymentReference}
              </span>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Proof of Payment */}
      <Card className="mb-6">
        <CardContent className="pt-4 space-y-4">
          <Label className="text-sm font-semibold text-zinc-900">
            Proof of Payment
          </Label>

          {proofUrl ? (
            <>
              <button
                type="button"
                onClick={() => setLightboxOpen(true)}
                className="block w-full rounded-lg border overflow-hidden cursor-pointer hover:ring-2 hover:ring-zinc-300 transition-shadow"
              >
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img
                  src={proofUrl}
                  alt="Proof of payment"
                  className="w-full max-h-72 object-contain bg-zinc-100"
                />
              </button>
              {booking.proofOfPaymentFileName && (
                <p className="text-xs text-zinc-400 flex items-center gap-1 max-w-full">
                  <ImageIcon className="h-3 w-3 shrink-0" />
                  <span className="truncate">
                    {booking.proofOfPaymentFileName}
                  </span>
                </p>
              )}

              {/* Lightbox */}
              <Dialog open={lightboxOpen} onOpenChange={setLightboxOpen}>
                <DialogContent className="max-w-3xl p-2">
                  <DialogTitle className="sr-only">
                    Proof of Payment Image
                  </DialogTitle>
                  <DialogDescription className="sr-only">
                    Full-size proof of payment image
                  </DialogDescription>
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={proofUrl}
                    alt="Proof of payment — full size"
                    className="w-full rounded-md object-contain max-h-[80vh]"
                  />
                </DialogContent>
              </Dialog>
            </>
          ) : (
            <div className="flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 text-center">
              <ImageIcon className="h-8 w-8 text-zinc-300 mb-2" />
              <p className="text-sm text-zinc-400">
                No proof of payment uploaded
              </p>
            </div>
          )}

          {/* Payment Note */}
          {booking.paymentNote && (
            <div className="rounded-md bg-zinc-50 p-3 text-sm text-zinc-700">
              <p className="text-xs font-medium text-zinc-500 mb-1">
                Customer Note
              </p>
              <p className="whitespace-pre-wrap">{booking.paymentNote}</p>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Action Buttons */}
      <Card className="mb-6">
        <CardContent className="pt-4 space-y-4">
          {!showRejectForm ? (
            <div className="flex gap-3">
              <Button
                className="flex-1 bg-green-600 hover:bg-green-700 text-white"
                onClick={handleApprove}
                disabled={isSubmitting}
              >
                {isSubmitting ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <CheckCircle2 className="mr-2 h-4 w-4" />
                )}
                Approve
              </Button>
              <Button
                variant="destructive"
                className="flex-1"
                onClick={() => setShowRejectForm(true)}
                disabled={isSubmitting}
              >
                <XCircle className="mr-2 h-4 w-4" />
                Reject
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              <Label
                htmlFor="reject-note"
                className="text-sm font-semibold text-zinc-900"
              >
                Rejection Note{" "}
                <span className="font-normal text-zinc-400">(optional)</span>
              </Label>
              <Textarea
                id="reject-note"
                placeholder="e.g. Payment amount does not match, proof is unclear"
                value={rejectNote}
                onChange={(e) => setRejectNote(e.target.value.slice(0, 500))}
                maxLength={500}
                rows={3}
              />
              <p className="text-xs text-zinc-400 text-right">
                {rejectNote.length}/500
              </p>
              <div className="flex gap-3">
                <Button
                  variant="outline"
                  className="flex-1"
                  onClick={() => {
                    setShowRejectForm(false);
                    setRejectNote("");
                  }}
                  disabled={isSubmitting}
                >
                  Cancel
                </Button>
                <Button
                  variant="destructive"
                  className="flex-1"
                  onClick={handleReject}
                  disabled={isSubmitting}
                >
                  {isSubmitting ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <XCircle className="mr-2 h-4 w-4" />
                  )}
                  Confirm Rejection
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

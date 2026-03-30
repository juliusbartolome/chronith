"use client";

import { Suspense, useCallback, useRef, useState } from "react";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { toast } from "sonner";
import {
  Upload,
  FileImage,
  X,
  Loader2,
  CheckCircle2,
  AlertCircle,
  Clock,
} from "lucide-react";
import {
  useBookingStatus,
  useConfirmManualPayment,
} from "@/hooks/use-manual-payment";
import { formatPrice } from "@/lib/format";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
const ACCEPTED_TYPES = ["image/jpeg", "image/png", "image/webp"];

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

export default function PayPage() {
  return (
    <Suspense
      fallback={
        <div className="max-w-lg mx-auto px-4 py-10">
          <Skeleton className="h-6 w-48 mb-6" />
          <Skeleton className="h-[200px] w-full rounded-xl mb-6" />
          <Skeleton className="h-[300px] w-full rounded-xl" />
        </div>
      }
    >
      <PayPageContent />
    </Suspense>
  );
}

function PayPageContent() {
  const { tenantSlug, btSlug } = useParams<{
    tenantSlug: string;
    btSlug: string;
  }>();
  const searchParams = useSearchParams();
  const router = useRouter();

  const bookingId = searchParams.get("bookingId") ?? "";
  const expires = searchParams.get("expires") ?? "";
  const sig = searchParams.get("sig") ?? "";
  // Client-side expiry check to avoid wasted upload on expired links.
  // Computed once on initial render via useState initializer.
  const [isExpired] = useState(
    () => !!expires && Date.now() / 1000 > Number(expires),
  );
  const {
    data: booking,
    isLoading,
    error,
  } = useBookingStatus(tenantSlug, bookingId);

  const { mutateAsync: confirmPayment, isPending: isSubmitting } =
    useConfirmManualPayment();

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [filePreview, setFilePreview] = useState<string | null>(null);
  const [paymentNote, setPaymentNote] = useState("");
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const validateFile = useCallback((file: File): string | null => {
    if (!ACCEPTED_TYPES.includes(file.type)) {
      return "Please select a JPEG, PNG, or WebP image.";
    }
    if (file.size > MAX_FILE_SIZE) {
      return "File size must be under 5MB.";
    }
    return null;
  }, []);

  const handleFileSelect = useCallback(
    (file: File) => {
      const validationError = validateFile(file);
      if (validationError) {
        toast.error(validationError);
        return;
      }

      setSelectedFile(file);
      setFilePreview(URL.createObjectURL(file));
    },
    [validateFile],
  );

  const handleFileInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) handleFileSelect(file);
    },
    [handleFileSelect],
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);
      const file = e.dataTransfer.files[0];
      if (file) handleFileSelect(file);
    },
    [handleFileSelect],
  );

  const clearFile = useCallback(() => {
    if (filePreview) URL.revokeObjectURL(filePreview);
    setSelectedFile(null);
    setFilePreview(null);
    if (fileInputRef.current) fileInputRef.current.value = "";
  }, [filePreview]);

  const handleSubmit = async () => {
    try {
      await confirmPayment({
        tenantSlug,
        bookingId,
        expires,
        sig,
        proofFile: selectedFile ?? undefined,
        paymentNote: paymentNote.trim() || undefined,
      });

      toast.success("Payment confirmation sent! Staff will verify shortly.");
      router.push(`/book/${tenantSlug}/${btSlug}/success`);
    } catch (err) {
      toast.error(
        err instanceof Error
          ? err.message
          : "Failed to submit payment. Please try again.",
      );
    }
  };

  // Missing or invalid params
  if (!bookingId) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <AlertCircle className="h-12 w-12 text-zinc-400 mx-auto mb-4" />
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          Invalid Payment Link
        </h1>
        <p className="text-sm text-zinc-500">
          This payment link is missing required parameters. Please use the link
          from your booking confirmation email.
        </p>
      </div>
    );
  }

  // Expired link
  if (isExpired) {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <Clock className="h-12 w-12 text-amber-400 mx-auto mb-4" />
        <h1 className="text-xl font-bold text-zinc-900 mb-2">Link Expired</h1>
        <p className="text-sm text-zinc-500">
          This payment link has expired. Please contact the business for a new
          link.
        </p>
      </div>
    );
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="max-w-lg mx-auto px-4 py-10">
        <Skeleton className="h-6 w-48 mb-6" />
        <Skeleton className="h-[200px] w-full rounded-xl mb-6" />
        <Skeleton className="h-[300px] w-full rounded-xl" />
      </div>
    );
  }

  // Error state
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

  // Booking is no longer pending payment — show status
  if (booking.status !== "PendingPayment") {
    return (
      <div className="max-w-lg mx-auto px-4 py-16 text-center">
        <div className="mb-6">
          <StatusBadge status={booking.status} />
        </div>
        <h1 className="text-xl font-bold text-zinc-900 mb-2">
          {booking.status === "PendingVerification"
            ? "Payment Submitted"
            : booking.status === "Confirmed"
              ? "Booking Confirmed"
              : "Booking Status"}
        </h1>
        <p className="text-sm text-zinc-500 mb-6">
          {booking.status === "PendingVerification"
            ? "Your payment proof has been submitted. Staff will verify it shortly."
            : booking.status === "Confirmed"
              ? "Your booking has been confirmed. Thank you!"
              : `Current status: ${booking.status}`}
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

  // Main payment page — PendingPayment status
  return (
    <div className="max-w-lg mx-auto px-4 py-10">
      <h1 className="text-xl font-bold text-zinc-900 mb-1">
        Complete Your Payment
      </h1>
      <p className="text-sm text-zinc-500 mb-6">
        Send your payment using the details below, then submit your proof of
        payment.
      </p>

      {/* Booking Summary */}
      <Card className="mb-6">
        <CardContent className="pt-4 space-y-2 text-sm">
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
          <Separator />
          <div className="flex justify-between font-semibold">
            <span className="text-zinc-700">Amount Due</span>
            <span className="text-zinc-900">
              {formatPrice(booking.amountInCentavos)}
            </span>
          </div>
        </CardContent>
      </Card>

      {/* QR Code & Payment Instructions */}
      {booking.manualPaymentOptions && (
        <Card className="mb-6">
          <CardContent className="pt-4 space-y-4">
            <h2 className="text-sm font-semibold text-zinc-900">
              {booking.manualPaymentOptions.label || "Payment Instructions"}
            </h2>

            {booking.manualPaymentOptions.qrCodeUrl && (
              <div className="flex justify-center">
                <div className="rounded-lg border p-3 bg-white">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={booking.manualPaymentOptions.qrCodeUrl}
                    alt="Payment QR code"
                    className="h-48 w-48 object-contain"
                  />
                </div>
              </div>
            )}

            {booking.manualPaymentOptions.publicNote && (
              <div className="rounded-md bg-zinc-50 p-3 text-sm text-zinc-700 whitespace-pre-wrap">
                {booking.manualPaymentOptions.publicNote}
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Proof of Payment Upload */}
      <Card className="mb-6">
        <CardContent className="pt-4 space-y-4">
          <Label className="text-sm font-semibold text-zinc-900">
            Proof of Payment
          </Label>
          <p className="text-xs text-zinc-500">
            Upload a screenshot or photo of your payment receipt (JPEG, PNG, or
            WebP, max 5MB).
          </p>

          {!selectedFile ? (
            <div
              onDragOver={handleDragOver}
              onDragLeave={handleDragLeave}
              onDrop={handleDrop}
              onClick={() => fileInputRef.current?.click()}
              className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 cursor-pointer transition-colors ${
                isDragging
                  ? "border-[var(--color-primary)] bg-[var(--color-primary)]/5"
                  : "border-zinc-300 hover:border-zinc-400 hover:bg-zinc-50"
              }`}
            >
              <Upload className="h-8 w-8 text-zinc-400 mb-2" />
              <p className="text-sm font-medium text-zinc-700">
                Drag & drop your proof here
              </p>
              <p className="text-xs text-zinc-500 mt-1">
                or click to browse files
              </p>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={handleFileInputChange}
                className="hidden"
              />
            </div>
          ) : (
            <div className="rounded-lg border p-3 space-y-3">
              {/* Preview */}
              {filePreview && (
                <div className="flex justify-center">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={filePreview}
                    alt="Payment proof preview"
                    className="max-h-48 rounded-md object-contain"
                  />
                </div>
              )}

              {/* File info */}
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2 min-w-0">
                  <FileImage className="h-4 w-4 text-zinc-400 shrink-0" />
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-zinc-700 truncate">
                      {selectedFile.name}
                    </p>
                    <p className="text-xs text-zinc-500">
                      {(selectedFile.size / 1024).toFixed(0)} KB
                    </p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={clearFile}
                  className="rounded-md p-1 hover:bg-zinc-100 text-zinc-400 hover:text-zinc-600 transition-colors"
                  aria-label="Remove file"
                >
                  <X className="h-4 w-4" />
                </button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Payment Note */}
      <Card className="mb-6">
        <CardContent className="pt-4 space-y-2">
          <Label
            htmlFor="payment-note"
            className="text-sm font-semibold text-zinc-900"
          >
            Payment Note{" "}
            <span className="font-normal text-zinc-400">(optional)</span>
          </Label>
          <Textarea
            id="payment-note"
            placeholder="e.g. Sent via GCash, reference #12345"
            value={paymentNote}
            onChange={(e) => setPaymentNote(e.target.value.slice(0, 500))}
            maxLength={500}
            rows={3}
          />
          <p className="text-xs text-zinc-400 text-right">
            {paymentNote.length}/500
          </p>
        </CardContent>
      </Card>

      {/* Submit */}
      <Button className="w-full" onClick={handleSubmit} disabled={isSubmitting}>
        {isSubmitting ? (
          <>
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Submitting…
          </>
        ) : (
          "I've Sent My Payment"
        )}
      </Button>
    </div>
  );
}

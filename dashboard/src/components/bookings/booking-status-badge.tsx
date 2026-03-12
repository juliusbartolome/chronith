import { Badge } from "@/components/ui/badge";

const STATUS_CONFIG = {
  PendingPayment: { label: "Pending Payment", variant: "outline" as const },
  PendingVerification: {
    label: "Pending Verification",
    variant: "secondary" as const,
  },
  Confirmed: { label: "Confirmed", variant: "default" as const },
  Cancelled: { label: "Cancelled", variant: "destructive" as const },
};

export function BookingStatusBadge({ status }: { status: string }) {
  const config = STATUS_CONFIG[status as keyof typeof STATUS_CONFIG] ?? {
    label: status,
    variant: "outline" as const,
  };
  return <Badge variant={config.variant}>{config.label}</Badge>;
}

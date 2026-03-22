import { redirect } from "next/navigation";

export default function DemoPage() {
  const tenantSlug = process.env.NEXT_PUBLIC_DEFAULT_TENANT_SLUG ?? "nexoflow-automations";
  redirect(`/book/${tenantSlug}`);
}

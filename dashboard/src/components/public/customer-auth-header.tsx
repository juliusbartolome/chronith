"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { useCustomerMe, useCustomerLogout } from "@/hooks/use-customer-auth";

export default function CustomerAuthHeader({
  tenantSlug,
}: {
  tenantSlug: string;
}) {
  const router = useRouter();
  const { data: customer } = useCustomerMe();
  const logoutMutation = useCustomerLogout();

  async function handleLogout() {
    await logoutMutation.mutateAsync();
    router.push(`/book/${tenantSlug}`);
  }

  return (
    <div className="flex justify-end items-center gap-2 px-4 py-2 border-b">
      {customer ? (
        <>
          <Button variant="ghost" size="sm" asChild>
            <Link href={`/book/${tenantSlug}/my-bookings`}>My Bookings</Link>
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={handleLogout}
            disabled={logoutMutation.isPending}
          >
            Logout
          </Button>
        </>
      ) : (
        <Button variant="outline" size="sm" asChild>
          <Link href={`/book/${tenantSlug}/auth/login`}>Sign in</Link>
        </Button>
      )}
    </div>
  );
}

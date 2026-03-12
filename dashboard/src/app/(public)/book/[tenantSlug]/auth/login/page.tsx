"use client";

import { useState } from "react";
import { useRouter, useSearchParams, useParams } from "next/navigation";
import Link from "next/link";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useCustomerLogin } from "@/hooks/use-customer-auth";

export default function CustomerLoginPage() {
  const { tenantSlug } = useParams<{ tenantSlug: string }>();
  const router = useRouter();
  const searchParams = useSearchParams();
  const returnTo = searchParams.get("returnTo") ?? `/book/${tenantSlug}`;

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const loginMutation = useCustomerLogin();

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    try {
      await loginMutation.mutateAsync({ email, password });
      router.push(returnTo);
    } catch {
      toast.error("Invalid email or password.");
    }
  }

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>Sign in</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
            <Button
              type="submit"
              className="w-full"
              disabled={loginMutation.isPending}
            >
              {loginMutation.isPending ? "Signing in…" : "Sign in"}
            </Button>
          </form>
          <p className="text-sm text-center mt-4 text-muted-foreground">
            Don&apos;t have an account?{" "}
            <Link
              href={`/book/${tenantSlug}/auth/register`}
              className="underline"
            >
              Register
            </Link>
          </p>
        </CardContent>
      </Card>
    </div>
  );
}

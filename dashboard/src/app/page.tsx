"use client";

// Root page that handles auth-based routing
// - Authenticated users are redirected to /bookings
// - Unauthenticated users see the landing page

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/stores/auth-store";
import LandingPage from "@/app/(public)/landing-page";
import { Loader2 } from "lucide-react";

export default function HomePage() {
  const router = useRouter();
  const { user } = useAuthStore();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    // Only redirect if user is authenticated (after mount to avoid hydration issues)
    if (mounted && user) {
      router.replace("/bookings");
    }
  }, [mounted, user, router]);

  // Show loading state until mounted to avoid hydration mismatch
  if (!mounted) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-50">
        <Loader2 className="h-8 w-8 animate-spin text-zinc-400" />
      </div>
    );
  }

  // If user is authenticated, they will be redirected via useEffect
  // Show the landing page while waiting for redirect
  if (user) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-zinc-50">
        <Loader2 className="h-8 w-8 animate-spin text-zinc-400" />
      </div>
    );
  }

  // Unauthenticated users see the landing page
  return <LandingPage />;
}

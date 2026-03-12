"use client";

import { useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/stores/auth-store";

export function Header() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  const handleLogout = async () => {
    await fetch("/api/auth/logout", { method: "POST" });
    logout();
    router.push("/login");
    router.refresh();
  };

  return (
    <header className="flex h-14 items-center justify-between border-b bg-white px-6">
      <div />
      <div className="flex items-center gap-3">
        {user && <span className="text-sm text-zinc-600">{user.email}</span>}
        <Button variant="ghost" size="sm" onClick={handleLogout}>
          <LogOut className="h-4 w-4" />
        </Button>
      </div>
    </header>
  );
}

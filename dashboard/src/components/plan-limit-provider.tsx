"use client";

import React, { createContext, useContext, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

interface PlanLimitContextValue {
  showUpgradePrompt: (message?: string) => void;
}

const PlanLimitContext = createContext<PlanLimitContextValue | null>(null);

export function PlanLimitProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState<string>(
    "You've reached your plan's limit for this resource.",
  );

  const showUpgradePrompt = useCallback((msg?: string) => {
    if (msg) setMessage(msg);
    setOpen(true);
  }, []);

  return (
    <PlanLimitContext.Provider value={{ showUpgradePrompt }}>
      {children}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Upgrade your plan</DialogTitle>
            <DialogDescription>{message}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpen(false)}>
              Dismiss
            </Button>
            <Button
              onClick={() => {
                setOpen(false);
                router.push("/settings/subscription");
              }}
            >
              View plans
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </PlanLimitContext.Provider>
  );
}

export function usePlanLimit() {
  const ctx = useContext(PlanLimitContext);
  if (!ctx) {
    throw new Error("usePlanLimit must be used within PlanLimitProvider");
  }
  return ctx;
}

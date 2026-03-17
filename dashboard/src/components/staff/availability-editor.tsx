"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const DAYS = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

export interface AvailabilityWindow {
  dayOfWeek: number;
  startTime: string; // "HH:mm"
  endTime: string; // "HH:mm"
}

interface Props {
  windows: AvailabilityWindow[];
  onChange: (windows: AvailabilityWindow[]) => void;
}

export function AvailabilityEditor({ windows, onChange }: Props) {
  const [day, setDay] = useState(1);
  const [start, setStart] = useState("09:00");
  const [end, setEnd] = useState("17:00");

  const addWindow = () => {
    if (!start || !end || start >= end) return;
    onChange([...windows, { dayOfWeek: day, startTime: start, endTime: end }]);
  };

  const removeWindow = (index: number) => {
    onChange(windows.filter((_, i) => i !== index));
  };

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        {DAYS.map((dayName, dayIndex) => {
          const dayWindows = windows.filter((w) => w.dayOfWeek === dayIndex);
          return (
            <div key={dayName} className="flex items-start gap-4">
              <span className="w-24 pt-1 text-sm font-medium text-zinc-700">
                {dayName}
              </span>
              <div className="flex-1 space-y-1">
                {dayWindows.length === 0 && (
                  <span className="text-sm text-zinc-400">Unavailable</span>
                )}
                {dayWindows.map((w) => {
                  const globalIndex = windows.indexOf(w);
                  return (
                    <div
                      key={globalIndex}
                      className="flex items-center gap-2 rounded-md bg-zinc-50 px-3 py-1 text-sm"
                    >
                      <span>
                        {w.startTime} – {w.endTime}
                      </span>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="h-5 w-5 p-0 text-zinc-400 hover:text-red-600"
                        onClick={() => removeWindow(globalIndex)}
                        aria-label="Remove window"
                      >
                        ×
                      </Button>
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>

      <div className="flex items-end gap-3 border-t pt-4">
        <div>
          <Label htmlFor="day-select">Day</Label>
          <select
            id="day-select"
            value={day}
            onChange={(e) => setDay(Number(e.target.value))}
            className="mt-1 block rounded-md border border-zinc-200 px-2 py-1.5 text-sm"
          >
            {DAYS.map((d, i) => (
              <option key={d} value={i}>
                {d}
              </option>
            ))}
          </select>
        </div>
        <div>
          <Label htmlFor="start-time">Start</Label>
          <Input
            id="start-time"
            type="time"
            value={start}
            onChange={(e) => setStart(e.target.value)}
            className="mt-1 w-32"
          />
        </div>
        <div>
          <Label htmlFor="end-time">End</Label>
          <Input
            id="end-time"
            type="time"
            value={end}
            onChange={(e) => setEnd(e.target.value)}
            className="mt-1 w-32"
          />
        </div>
        <Button type="button" variant="outline" onClick={addWindow}>
          Add Window
        </Button>
      </div>
    </div>
  );
}

"use client";

import { POLLS_CLOSE } from "@/lib/config";
import { useTimeRemaining, unitLabel } from "@/lib/use-time-remaining";
import { cn } from "@/lib/utils";

/** Kompakt nedräkning till att vallokalerna stänger, som en rad text. */
export function PollsCloseCountdown({ className }: { className?: string }) {
  const remaining = useTimeRemaining(POLLS_CLOSE);

  if (!remaining || remaining.total <= 0) {
    // Före hydrering finns inget värde, och efter stängning är nedräkningen inte längre
    // intressant – då har sidan andra tillstånd att visa.
    return null;
  }

  const parts = [
    remaining.days > 0
      ? `${remaining.days} ${unitLabel(remaining.days, "dag", "dagar")}`
      : null,
    remaining.hours > 0 || remaining.days > 0
      ? `${remaining.hours} ${unitLabel(remaining.hours, "timme", "timmar")}`
      : null,
    `${remaining.minutes} ${unitLabel(remaining.minutes, "minut", "minuter")}`,
  ].filter(Boolean);

  return (
    <p className={cn("font-mono text-sm tabular-nums text-muted-foreground", className)}>
      Öppnar om {parts.join(", ")}
    </p>
  );
}

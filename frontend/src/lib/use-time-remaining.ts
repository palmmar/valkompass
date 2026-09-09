"use client";

import { useEffect, useState } from "react";

export interface Remaining {
  days: number;
  hours: number;
  minutes: number;
  /** Millisekunder kvar. Noll eller negativt när tidpunkten passerats. */
  total: number;
}

function compute(target: Date): Remaining {
  const total = target.getTime() - Date.now();
  const clamped = Math.max(0, total);
  return {
    days: Math.floor(clamped / 86_400_000),
    hours: Math.floor((clamped % 86_400_000) / 3_600_000),
    minutes: Math.floor((clamped % 3_600_000) / 60_000),
    total,
  };
}

/**
 * Tid kvar till <paramref name="target" />, uppdaterad varje sekund.
 *
 * Returnerar null fram till första klient-renderingen. Servern och den första
 * klient-renderingen får alltså inget värde, vilket är avsiktligt: tiden skiljer sig mellan
 * server och klient och skulle annars ge en hydreringsmiss. setState sker i en callback
 * (inte synkront i effekten) för att inte trigga react-hooks/set-state-in-effect.
 */
export function useTimeRemaining(target: Date): Remaining | null {
  const [remaining, setRemaining] = useState<Remaining | null>(null);

  useEffect(() => {
    const update = () => setRemaining(compute(target));
    const raf = requestAnimationFrame(update);
    const id = setInterval(update, 1000);
    return () => {
      cancelAnimationFrame(raf);
      clearInterval(id);
    };
  }, [target]);

  return remaining;
}

/** Svensk singular/plural för en tidsenhet. */
export function unitLabel(value: number, one: string, many: string): string {
  return value === 1 ? one : many;
}

"use client";

import { useEffect, useState } from "react";
import { ELECTION_DATE } from "@/lib/config";

interface Remaining {
  days: number;
  hours: number;
  minutes: number;
  total: number; // millisekunder kvar
}

function getRemaining(target: Date): Remaining {
  const total = target.getTime() - Date.now();
  const clamped = Math.max(0, total);
  return {
    days: Math.floor(clamped / 86_400_000),
    hours: Math.floor((clamped % 86_400_000) / 3_600_000),
    minutes: Math.floor((clamped % 3_600_000) / 60_000),
    total,
  };
}

const ELECTION_LABEL = "kvar till valet 13 september 2026";

/** Svensk singular/plural för en tidsenhet. */
function unitLabel(value: number, one: string, many: string): string {
  return value === 1 ? one : many;
}

function Unit({ value, label }: { value: number | string; label: string }) {
  return (
    <div className="flex flex-col items-center">
      <span className="font-heading text-3xl font-semibold tabular-nums tracking-tight sm:text-4xl">
        {value}
      </span>
      <span className="font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
        {label}
      </span>
    </div>
  );
}

/**
 * Nedräknare till valet. Renderas som en klient-ö: servern (och första klient-renderingen)
 * visar bara den statiska etiketten, talen fylls i efter mount. setState sker i en
 * callback (inte synkront i effekten) för att undvika both hydrerings-mismatch och
 * lint-regeln react-hooks/set-state-in-effect.
 */
export function ElectionCountdown() {
  const [remaining, setRemaining] = useState<Remaining | null>(null);

  useEffect(() => {
    const update = () => setRemaining(getRemaining(ELECTION_DATE));
    const raf = requestAnimationFrame(update); // första värdet direkt (asynkront)
    const id = setInterval(update, 1000); // håll minuterna aktuella
    return () => {
      cancelAnimationFrame(raf);
      clearInterval(id);
    };
  }, []);

  if (remaining && remaining.total <= 0) {
    return (
      <p className="mt-8 text-lg font-semibold tracking-tight">Valet är genomfört</p>
    );
  }

  return (
    <div className="mt-10 inline-flex flex-col items-start gap-2 border-t border-border pt-5">
      <div className="flex items-start gap-6 sm:gap-8" aria-hidden={remaining === null}>
        {remaining ? (
          <>
            <Unit value={remaining.days} label={unitLabel(remaining.days, "dag", "dagar")} />
            <Unit value={remaining.hours} label={unitLabel(remaining.hours, "timme", "timmar")} />
            <Unit value={remaining.minutes} label={unitLabel(remaining.minutes, "minut", "minuter")} />
          </>
        ) : (
          // Platshållare med samma struktur/höjd så layouten inte hoppar vid mount.
          <>
            <Unit value="–" label="dagar" />
            <Unit value="–" label="timmar" />
            <Unit value="–" label="minuter" />
          </>
        )}
      </div>
      <p className="text-sm text-muted-foreground">{ELECTION_LABEL}</p>
    </div>
  );
}

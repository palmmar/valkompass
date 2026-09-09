"use client";

import { POLLS_CLOSE } from "@/lib/config";
import { useTimeRemaining, unitLabel } from "@/lib/use-time-remaining";

const ELECTION_LABEL = "kvar tills vallokalerna stänger 13 september 2026";

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
 * Nedräknare till att vallokalerna stänger. Renderas som en klient-ö: servern (och första
 * klient-renderingen) visar bara den statiska etiketten, talen fylls i efter mount.
 *
 * Används i läget före valdagen. Vad som visas därefter styrs av valvakans fas och inte av
 * klockan – se ElectionDayCta.
 */
export function ElectionCountdown() {
  const remaining = useTimeRemaining(POLLS_CLOSE);

  // Nås normalt inte: efter stängning har ElectionDayCta redan bytt till valvake-läget.
  // Finns kvar som skydd om fasen inte gick att hämta.
  if (remaining && remaining.total <= 0) {
    return (
      <p className="mt-8 text-lg font-semibold tracking-tight">
        Vallokalerna har stängt – rösträkningen pågår
      </p>
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

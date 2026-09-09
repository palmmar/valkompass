"use client";

import type { ElectionLive } from "@/lib/election-api";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const timeFmt = new Intl.DateTimeFormat("sv-SE", {
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  timeZone: "Europe/Stockholm",
});

const num = (n: number) => n.toLocaleString("sv-SE");

/** Kort etikett för fasen. Backend äger tillståndet, det här är bara språkdräkten. */
const PHASE_LABEL: Record<ElectionLive["phase"], string> = {
  preElection: "Före valdagen",
  electionDay: "Valdagen",
  waitingForResults: "Väntar på resultat",
  live: "Rösträkning pågår",
  preliminaryPaused: "Preliminärt färdigräknat",
  finalCounting: "Slutlig rösträkning",
  finished: "Slutresultat",
};

function Metric({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="space-y-0.5">
      <dt className="font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
        {label}
      </dt>
      <dd className="text-lg font-semibold tabular-nums leading-tight">{value}</dd>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
    </div>
  );
}

export function StatusBar({ data }: { data: ElectionLive }) {
  const { source, reporting, phase } = data;
  const counting = phase === "live" || phase === "finalCounting";

  return (
    <div className="rounded-lg border bg-card p-4 sm:p-5">
      <div className="flex flex-wrap items-center gap-x-3 gap-y-2">
        <span className="flex items-center gap-2 text-sm font-semibold">
          <span
            className={cn(
              "size-2 rounded-full",
              counting ? "animate-pulse bg-red-600" : "bg-muted-foreground/50",
            )}
            aria-hidden
          />
          {PHASE_LABEL[phase]}
        </span>

        {source?.isTest && <Badge variant="destructive">Testdata – inte verkligt valresultat</Badge>}
        {source?.stale && <Badge variant="secondary">Fördröjd uppdatering</Badge>}

        {source && (
          <span className="ml-auto text-sm text-muted-foreground">
            Senast uppdaterad{" "}
            <time dateTime={source.updatedAt} className="tabular-nums">
              {timeFmt.format(new Date(source.updatedAt))}
            </time>
          </span>
        )}
      </div>

      {reporting && (
        <dl className="mt-4 grid gap-4 border-t pt-4 sm:grid-cols-3">
          <Metric
            label="Valdistrikt"
            value={`${num(reporting.districtsReported)} / ${num(reporting.districtsTotal)}`}
            hint="rapporterade av totalt"
          />
          <Metric
            // Distriktandel och väljarandel är olika saker. Hälften av distrikten kan vara
            // en åttondel av väljarna, så täckningen räknas på röstberättigade.
            label="Täckning"
            value={
              reporting.coveragePercent == null
                ? "–"
                : `${reporting.coveragePercent.toLocaleString("sv-SE", { maximumFractionDigits: 1 })} %`
            }
            hint="av de röstberättigade, inte av rösterna"
          />
          <Metric
            label="Räknade röster"
            value={num(reporting.totalVotes)}
            hint={
              reporting.turnoutPercent == null
                ? undefined
                : `valdeltagande ${reporting.turnoutPercent.toLocaleString("sv-SE", { maximumFractionDigits: 1 })} %`
            }
          />
        </dl>
      )}
    </div>
  );
}

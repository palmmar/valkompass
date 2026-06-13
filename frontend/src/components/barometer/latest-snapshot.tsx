"use client";

import type { BarometerLatest } from "@/lib/barometer-api";
import { PartyLogo } from "@/components/party-logo";
import { partyColor } from "./colors";

const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "long", year: "numeric" });
const fmtDate = (s: string | null) => (s ? dayFmt.format(new Date(`${s}T00:00:00`)) : "okänt");

/**
 * Senaste läget: stapelvy av den färskaste mätningen, med förändring i procentenheter
 * sedan senaste riksdagsval. Sorterad efter stöd (deskriptivt, inte en kvalitetsrangordning).
 */
export function LatestSnapshot({ latest }: { latest: BarometerLatest }) {
  const partyByCode = new Map(latest.parties.map((p) => [p.code, p]));
  const reported = latest.results.filter((r) => r.value != null).sort((a, b) => b.value! - a.value!);
  const unreported = latest.results.filter((r) => r.value == null);
  const max = Math.max(10, ...reported.map((r) => r.value!));
  const poll = latest.poll;

  return (
    <div className="space-y-4">
      <div className="text-sm text-muted-foreground">
        <span className="font-medium text-foreground">{poll.pollsterName}</span> · publicerad {fmtDate(poll.publishedAt)}
        {poll.fieldStart && poll.fieldEnd && <> · fältperiod {poll.fieldStart}–{poll.fieldEnd}</>}
        {poll.sampleSize != null && <> · n = {poll.sampleSize.toLocaleString("sv-SE")}</>}
      </div>

      <ul className="space-y-2">
        {reported.map((r) => {
          const party = partyByCode.get(r.partyCode);
          const color = partyColor(party?.color);
          const delta = r.electionDelta;
          return (
            <li key={r.partyCode} className="flex items-center gap-3">
              <PartyLogo code={r.partyCode} color={party?.color} size={24} />
              <span className="w-7 shrink-0 text-sm font-medium">{r.partyCode}</span>
              <div className="relative h-6 flex-1 overflow-hidden rounded bg-muted">
                <div
                  className="h-full rounded"
                  style={{ width: `${(r.value! / max) * 100}%`, backgroundColor: color }}
                />
              </div>
              <span className="w-20 shrink-0 text-right text-sm tabular-nums">
                {r.value!.toFixed(1)} %
                {r.marginOfError != null && (
                  <span className="text-muted-foreground"> ±{r.marginOfError.toFixed(1)}</span>
                )}
              </span>
              <span className="w-16 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
                {delta == null ? "" : `${delta > 0 ? "▲ +" : delta < 0 ? "▼ " : "± "}${delta.toFixed(1)}`}
              </span>
            </li>
          );
        })}
      </ul>

      {unreported.length > 0 && (
        <p className="text-xs text-muted-foreground">
          Redovisas ej i denna mätning: {unreported.map((r) => r.partyCode).join(", ")} (under institutets
          redovisningsgräns – visas aldrig som 0&nbsp;%).
        </p>
      )}

      {latest.electionYear != null && (
        <p className="text-xs text-muted-foreground">
          Förändring (▲/▼) visas i procentenheter mot riksdagsvalet {latest.electionYear} – ett faktiskt
          valresultat, inte en opinionsmätning.
        </p>
      )}
    </div>
  );
}

"use client";

import type { BarometerPoll } from "@/lib/barometer-api";

const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "short", year: "numeric" });
const fmt = (s: string | null) => (s ? dayFmt.format(new Date(`${s}T00:00:00`)) : "–");

/**
 * Proveniens per mätning: institut, fältperiod, antal svar (n) och käll-URL. Gör varje
 * datapunkt spårbar – samma proveniensprincip som partipositionerna i kompassen.
 */
export function PollList({ polls }: { polls: BarometerPoll[] }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
            <th className="py-2 pr-3 font-medium">Institut</th>
            <th className="py-2 pr-3 font-medium">Publicerad</th>
            <th className="py-2 pr-3 font-medium">Fältperiod</th>
            <th className="py-2 pr-3 text-right font-medium">n</th>
            <th className="py-2 font-medium">Källa</th>
          </tr>
        </thead>
        <tbody>
          {polls.map((p) => (
            <tr key={p.externalKey} className="border-b last:border-0">
              <td className="py-2 pr-3">{p.pollsterName}</td>
              <td className="py-2 pr-3 whitespace-nowrap text-muted-foreground">{fmt(p.publishedAt)}</td>
              <td className="py-2 pr-3 whitespace-nowrap text-muted-foreground">
                {p.fieldStart && p.fieldEnd ? `${fmt(p.fieldStart)}–${fmt(p.fieldEnd)}` : "–"}
              </td>
              <td className="py-2 pr-3 text-right tabular-nums text-muted-foreground">
                {p.sampleSize != null ? p.sampleSize.toLocaleString("sv-SE") : "–"}
              </td>
              <td className="py-2">
                {p.sourceUrl ? (
                  <a
                    href={p.sourceUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-primary underline underline-offset-2 hover:no-underline"
                    title={p.sourceCitation ?? undefined}
                  >
                    Källa
                  </a>
                ) : (
                  <span className="text-muted-foreground" title={p.sourceCitation ?? undefined}>
                    {p.sourceCitation ?? "–"}
                  </span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

"use client";

import type { ElectionLive } from "@/lib/election-api";
import { PartyLogo } from "@/components/party-logo";
import { cn } from "@/lib/utils";

const dec = (n: number) => n.toLocaleString("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

const pct = (n: number) => `${n.toLocaleString("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} %`;

/** Förändring i procentenheter mot 2022, med tecken. */
function Change({ points }: { points: number | null }) {
  if (points == null) return <span className="text-muted-foreground">–</span>;
  const rounded = Math.round(points * 10) / 10;
  return (
    <span
      className={cn(
        "tabular-nums",
        rounded > 0 && "text-emerald-700 dark:text-emerald-400",
        rounded < 0 && "text-red-700 dark:text-red-400",
        rounded === 0 && "text-muted-foreground",
      )}
    >
      {rounded > 0 ? "+" : rounded < 0 ? "−" : "±"}
      {Math.abs(rounded).toLocaleString("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}
    </span>
  );
}

/**
 * Räknat resultat per parti, med prognos i en egen kolumn när det finns en.
 *
 * Prognoskolumnen ritas bara när API:t faktiskt levererar en prognos. Finns ingen ritas den
 * inte alls – hellre det än en tom kolumn som får läsaren att undra vad som saknas. När den
 * finns är den visuellt avskild med en egen ram, eftersom det är vår uppskattning och inte
 * Valmyndighetens siffra.
 */
export function ResultsTable({ data }: { data: ElectionLive }) {
  const mandateByParty = new Map(data.officialMandates.map((m) => [m.partyCode, m]));
  const forecastByParty = new Map(data.forecast?.parties.map((p) => [p.partyCode, p]) ?? []);
  const threshold = data.thresholds.nationalPercent;
  const hasMandates = data.officialMandates.length > 0;
  const hasForecast = forecastByParty.size > 0;
  const max = Math.max(10, ...data.results.map((r) => r.sharePercent));

  return (
    <div className="overflow-x-auto">
      {/* På mobil döljs stapeln och partinamnet, så alla kolumner ryms utan sidled-scroll.
          overflow-x-auto finns kvar som skydd för riktigt smala skärmar. */}
      <table className="w-full min-w-[19rem] border-collapse text-sm">
        <caption className="sr-only">
          Räknat valresultat per parti med jämförelse mot valet 2022 och officiell preliminär
          mandatfördelning. Källa: Valmyndigheten.
        </caption>
        <thead>
          <tr className="border-b text-left font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
            <th scope="col" className="py-2 pr-2 font-medium">Parti</th>
            <th scope="col" className="py-2 pr-2 text-right font-medium">Räknat</th>
            {hasForecast && (
              // Prognosen får en egen, avvikande kolumngrupp. Att den ser annorlunda ut är
              // hela poängen: den är vår uppskattning, inte Valmyndighetens siffra.
              <th
                scope="col"
                className="border-l border-dashed py-2 pl-3 pr-2 text-right font-medium text-foreground"
              >
                Prognos
              </th>
            )}
            <th scope="col" className="hidden py-2 pr-2 text-right font-medium sm:table-cell">
              Mot 2022
            </th>
            {hasMandates && (
              <th scope="col" className="hidden py-2 pl-2 text-right font-medium sm:table-cell">
                Mandat
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {data.results.map((r) => {
            const mandate = mandateByParty.get(r.partyCode);
            const forecast = forecastByParty.get(r.partyCode);
            const belowThreshold = r.sharePercent < threshold;
            return (
              <tr key={r.partyCode} className="border-b last:border-0">
                <th scope="row" className="py-2.5 pr-2 text-left font-normal">
                  <span className="flex items-center gap-2.5">
                    <PartyLogo code={r.partyCode} size={22} />
                    <span className="font-medium">{r.partyCode}</span>
                    <span className="hidden text-muted-foreground sm:inline">{r.name}</span>
                  </span>
                </th>
                <td className="py-2.5 pr-2 text-right">
                  <span className="flex items-center justify-end gap-3">
                    {/* Stapeln ger storleksordningen på ett ögonkast; talet är det exakta. */}
                    <span className="hidden h-2 w-24 overflow-hidden rounded-full bg-muted sm:block" aria-hidden>
                      <span
                        className={cn(
                          "block h-full rounded-full",
                          belowThreshold ? "bg-muted-foreground/40" : "bg-foreground/70",
                        )}
                        style={{ width: `${(r.sharePercent / max) * 100}%` }}
                      />
                    </span>
                    <span
                      className={cn(
                        "w-16 font-semibold tabular-nums",
                        belowThreshold && "text-muted-foreground",
                      )}
                    >
                      {pct(r.sharePercent)}
                    </span>
                  </span>
                </td>
                {hasForecast && (
                  <td className="border-l border-dashed py-2.5 pl-3 pr-2 text-right">
                    {forecast ? (
                      <>
                        <span className="block font-semibold tabular-nums">
                          {pct(forecast.forecastShare)}
                        </span>
                        <span className="block whitespace-nowrap text-xs tabular-nums text-muted-foreground">
                          {dec(forecast.lower90)}–{dec(forecast.upper90)}
                        </span>
                      </>
                    ) : (
                      <span className="text-muted-foreground">–</span>
                    )}
                  </td>
                )}
                <td className="hidden py-2.5 pr-2 text-right sm:table-cell">
                  <Change points={r.shareChangePoints} />
                </td>
                {hasMandates && (
                  <td className="hidden py-2.5 pl-2 text-right tabular-nums sm:table-cell">
                    {mandate ? (
                      <>
                        <span className="font-semibold">{mandate.mandates}</span>
                        {mandate.change != null && mandate.change !== 0 && (
                          <span className="ml-1 text-xs text-muted-foreground">
                            ({mandate.change > 0 ? "+" : "−"}
                            {Math.abs(mandate.change)})
                          </span>
                        )}
                      </>
                    ) : (
                      <span className="text-muted-foreground">–</span>
                    )}
                  </td>
                )}
              </tr>
            );
          })}
        </tbody>
      </table>

      <p className="mt-3 text-xs text-muted-foreground">
        Partier under riksdagsspärren på {threshold.toLocaleString("sv-SE")} % är gråmarkerade.
        {hasMandates && " Mandaten är Valmyndighetens officiella preliminära fördelning."}
        {hasForecast
          && " Prognoskolumnen är vår egen uppskattning med 90 %-intervall under. Allt annat i"
            + " tabellen är faktiskt räknat."}
      </p>
    </div>
  );
}

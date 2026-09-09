"use client";

import type { ElectionForecast, ElectionThresholds } from "@/lib/election-api";
import { CONFIDENCE_LABEL } from "@/lib/election-api";

const dec = (n: number) => n.toLocaleString("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/**
 * Partier vars riksdagsspärr faktiskt är en öppen fråga. För ett parti som modellen ger 99 %
 * eller 1 % är sannolikheten inte information, den är brus – och att skriva ut den inbjuder
 * till att läsa in dramatik som inte finns.
 */
const UNCERTAIN_LOW = 0.03;
const UNCERTAIN_HIGH = 0.97;

export function ForecastSummary({
  forecast,
  thresholds,
}: {
  forecast: ElectionForecast;
  thresholds: ElectionThresholds;
}) {
  const nearThreshold = forecast.parties.filter(
    (p) => p.probabilityAboveThreshold > UNCERTAIN_LOW && p.probabilityAboveThreshold < UNCERTAIN_HIGH,
  );

  return (
    <section className="space-y-4 rounded-lg border bg-card p-4 sm:p-5">
      <div className="flex flex-wrap items-baseline gap-x-6 gap-y-2">
        <div>
          <p className="font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
            Prognosläge
          </p>
          <p className="text-lg font-semibold">{CONFIDENCE_LABEL[forecast.confidence]}</p>
        </div>
        <div>
          <p className="font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
            Typisk osäkerhet
          </p>
          <p className="text-lg font-semibold tabular-nums">
            ±{dec(forecast.typicalUncertaintyPoints)} procentenheter
          </p>
        </div>
      </div>

      {nearThreshold.length > 0 && (
        <div className="space-y-2 border-t pt-4">
          <h3 className="text-sm font-semibold">
            Nära riksdagsspärren på {thresholds.nationalPercent.toLocaleString("sv-SE")} %
          </h3>
          <ul className="space-y-1.5">
            {nearThreshold.map((p) => (
              <li key={p.partyCode} className="flex items-baseline gap-2 text-sm">
                <span className="w-8 shrink-0 font-semibold">{p.partyCode}</span>
                <span className="tabular-nums">
                  prognos {dec(p.forecastShare)} %
                </span>
                <span className="text-muted-foreground">·</span>
                <span className="tabular-nums">
                  {Math.round(p.probabilityAboveThreshold * 100)} % sannolikhet att nå spärren
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <p className="border-t pt-4 text-xs leading-relaxed text-muted-foreground">
        Prognosen är vår egen, inte Valmyndighetens. Den utgår från hur rösterna förändrats
        sedan 2022 i de {forecast.comparableDistrictsUsed.toLocaleString("sv-SE")} jämförbara
        valdistrikt som rapporterat, och uppskattar resten av landet därifrån. Intervallet är
        ett 90-procentsintervall från {forecast.draws.toLocaleString("sv-SE")} simuleringar:
        utfallet väntas hamna inom det i nio fall av tio.
        {forecast.regionalSkewPercent > 15 && (
          <>
            {" "}
            De distrikt som rapporterat är just nu ojämnt fördelade över landet, vilket gör
            osäkerheten större än täckningen ensam antyder.
          </>
        )}{" "}
        Opinionsmätningar ingår inte i modellen.
      </p>
    </section>
  );
}

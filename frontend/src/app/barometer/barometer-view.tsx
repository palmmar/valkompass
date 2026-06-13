"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  fetchBarometerLatest,
  fetchBarometerPolls,
  fetchBarometerTimeseries,
} from "@/lib/barometer-api";
import { TimeSeriesChart } from "@/components/barometer/time-series-chart";
import { LatestSnapshot } from "@/components/barometer/latest-snapshot";
import { PollList } from "@/components/barometer/poll-list";
import { PartyToggles } from "@/components/barometer/party-toggles";
import { BlockChart } from "@/components/barometer/block-chart";
import { cn } from "@/lib/utils";

type Tab = "trend" | "block" | "latest" | "pollofpolls";
type Range = "mandate" | "recent" | "all";

const TABS: { id: Tab; label: string }[] = [
  { id: "trend", label: "Trend över tid" },
  { id: "block", label: "Blockläge" },
  { id: "latest", label: "Senaste läget" },
  { id: "pollofpolls", label: "Glidande snitt" },
];

const RANGES: { id: Range; label: string }[] = [
  { id: "mandate", label: "Mandatperiod (2022–)" },
  { id: "recent", label: "Senaste 2 åren" },
  { id: "all", label: "Hela perioden" },
];

const day = (s: string) => new Date(`${s}T00:00:00`);

export function BarometerView() {
  const [tab, setTab] = useState<Tab>("trend");
  const [range, setRange] = useState<Range>("mandate");
  const [visible, setVisible] = useState<Set<string>>(new Set());
  const initialized = useRef(false);

  const tsQuery = useQuery({ queryKey: ["barometer", "timeseries"], queryFn: fetchBarometerTimeseries });
  const latestQuery = useQuery({ queryKey: ["barometer", "latest"], queryFn: fetchBarometerLatest });
  const pollsQuery = useQuery({ queryKey: ["barometer", "polls"], queryFn: () => fetchBarometerPolls() });

  const ts = tsQuery.data;
  const parties = useMemo(() => ts?.parties ?? [], [ts]);

  useEffect(() => {
    if (!initialized.current && parties.length) {
      setVisible(new Set(parties.map((p) => p.code)));
      initialized.current = true;
    }
  }, [parties]);

  const pollsterNames = useMemo(
    () => new Map((pollsQuery.data ?? []).map((p) => [p.pollsterCode, p.pollsterName])),
    [pollsQuery.data],
  );

  const maxDate = useMemo(() => {
    let m = 0;
    for (const s of ts?.series ?? []) for (const p of s.points) m = Math.max(m, day(p.date).getTime());
    return m ? new Date(m) : new Date();
  }, [ts]);

  const { fromDate, toDate } = useMemo(() => {
    const to = new Date(maxDate.getTime() + 10 * 864e5);
    const from =
      range === "mandate"
        ? new Date("2022-09-11T00:00:00")
        : range === "recent"
          ? new Date(maxDate.getTime() - 2 * 365 * 864e5)
          : new Date("2014-09-01T00:00:00");
    return { fromDate: from, toDate: to };
  }, [range, maxDate]);

  const toggle = (code: string) =>
    setVisible((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  const setAll = (on: boolean) => setVisible(on ? new Set(parties.map((p) => p.code)) : new Set());

  const disclaimer = ts?.disclaimer ?? latestQuery.data?.disclaimer;

  return (
    <div className="space-y-6">
      {/* Tydlig metodisk åtskillnad: opinion ≠ prognos ≠ valresultat. */}
      <div className="rounded-lg border border-amber-300/60 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-400/30 dark:bg-amber-950/40 dark:text-amber-200">
        <strong>Opinion, inte prognos.</strong> Detta visar publicerade opinionsmätningar – inte en
        förutsägelse av valresultatet. Faktiska valresultat visas separat och tydligt märkta.
      </div>

      {/* Flikar */}
      <div className="flex flex-wrap gap-1 border-b">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setTab(t.id)}
            className={cn(
              "-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors",
              tab === t.id
                ? "border-foreground text-foreground"
                : "border-transparent text-muted-foreground hover:text-foreground",
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tsQuery.isError || latestQuery.isError ? (
        <p className="text-sm text-destructive">Kunde inte hämta barometerdata. Försök igen senare.</p>
      ) : null}

      {tab === "latest" ? (
        latestQuery.isLoading ? (
          <Loading />
        ) : latestQuery.data ? (
          <LatestSnapshot latest={latestQuery.data} />
        ) : null
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2">
            {RANGES.map((r) => (
              <button
                key={r.id}
                type="button"
                onClick={() => setRange(r.id)}
                className={cn(
                  "rounded-full border px-3 py-1 text-xs transition-colors",
                  range === r.id
                    ? "border-foreground bg-foreground text-background"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                {r.label}
              </button>
            ))}
          </div>

          {tsQuery.isLoading ? (
            <Loading />
          ) : ts ? (
            <>
              <PartyToggles parties={parties} visible={visible} onToggle={toggle} onSetAll={setAll} />
              {tab === "block" ? (
                pollsQuery.data ? (
                  <BlockChart
                    polls={pollsQuery.data}
                    elections={ts.elections}
                    visible={visible}
                    fromDate={fromDate}
                    toDate={toDate}
                    pollsterNames={pollsterNames}
                  />
                ) : (
                  <Loading />
                )
              ) : (
                <>
                  <TimeSeriesChart
                    parties={parties}
                    visible={visible}
                    dots={ts.series}
                    line={tab === "trend" ? ts.monthlyAverage : ts.rollingAverage}
                    elections={ts.elections}
                    pollsterNames={pollsterNames}
                    showBand
                    emphasizeLine={tab === "pollofpolls"}
                    fromDate={fromDate}
                    toDate={toDate}
                  />
                  <p className="text-xs text-muted-foreground">
                    {tab === "trend" ? (
                      <>
                        Varje prick är en enskild mätning; linjen är ett <strong>månadssnitt</strong> och
                        den skuggade ytan visar mätningarnas genomsnittliga felmarginal.
                      </>
                    ) : (
                      <>
                        Linjen är ett <strong>glidande {ts.rollingWindowDays}-dagarssnitt</strong> av alla
                        institut (poll-of-polls), med felmarginalband. Ett snitt för läsbarhet – inte en prognos.
                      </>
                    )}
                  </p>
                </>
              )}
            </>
          ) : null}
        </div>
      )}

      {disclaimer && <p className="text-xs leading-relaxed text-muted-foreground">{disclaimer}</p>}

      {/* Proveniens: senaste mätningarna med institut, fältperiod, n och käll-URL. */}
      <section className="space-y-3">
        <h2 className="text-lg font-semibold tracking-tight">Källor &amp; mätningar</h2>
        <p className="text-sm text-muted-foreground">
          Primärkälla är respektive institut; data levereras via{" "}
          <a href="https://github.com/MansMeg/SwedishPolls" target="_blank" rel="noopener noreferrer" className="underline">
            SwedishPolls
          </a>{" "}
          (CC0) och{" "}
          <a href="https://www.scb.se/vara-tjanster/oppna-data/pxwebapi/" target="_blank" rel="noopener noreferrer" className="underline">
            SCB:s öppna API
          </a>{" "}
          (PSU). De 25 senaste mätningarna:
        </p>
        {pollsQuery.isLoading ? (
          <Loading />
        ) : pollsQuery.data ? (
          <PollList polls={pollsQuery.data.slice(0, 25)} />
        ) : null}
      </section>
    </div>
  );
}

function Loading() {
  return <div className="h-64 animate-pulse rounded-lg bg-muted" />;
}

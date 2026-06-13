"use client";

import { useMemo } from "react";
import type { BarometerElection, BarometerPoll } from "@/lib/barometer-api";
import { TimeSeriesChart } from "./time-series-chart";
import {
  DEFAULT_BLOCKS,
  activeMembers,
  blockSum,
  buildBlockDots,
  latestElectionBefore,
  rollingAverage,
} from "./blocks";

interface BlockChartProps {
  polls: BarometerPoll[];
  elections: BarometerElection[];
  visible: Set<string>;
  fromDate: Date;
  toDate: Date;
  pollsterNames: Map<string, string>;
}

const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "long", year: "numeric" });

/**
 * Blockläge: två valbara grupperingar (default S·V·MP·C mot M·KD·L·SD) som linjer med
 * felmarginalband. Summorna räknas om direkt när man klickar ur partier i listan ovanför.
 */
export function BlockChart({ polls, elections, visible, fromDate, toDate, pollsterNames }: BlockChartProps) {
  const opinion = useMemo(() => polls.filter((p) => !p.pollsterCode.startsWith("val-")), [polls]);

  const dots = useMemo(
    () => DEFAULT_BLOCKS.map((b) => ({ partyCode: b.id, points: buildBlockDots(opinion, b.members, visible) })),
    [opinion, visible],
  );
  const line = useMemo(
    () => dots.map((d) => ({ partyCode: d.partyCode, points: rollingAverage(d.points) })),
    [dots],
  );

  const blockParties = DEFAULT_BLOCKS.map((b, i) => ({
    code: b.id,
    name: activeMembers(b, visible).join(" · ") || "—",
    color: b.color,
    displayOrder: i + 1,
  }));
  const allBlocksVisible = useMemo(() => new Set(DEFAULT_BLOCKS.map((b) => b.id)), []);

  const latest = useMemo(
    () => (opinion.length ? opinion.reduce((a, b) => (a.publishedAt >= b.publishedAt ? a : b)) : null),
    [opinion],
  );
  const election = useMemo(
    () => latestElectionBefore(elections, latest?.publishedAt ?? null),
    [elections, latest],
  );

  const summary = DEFAULT_BLOCKS.map((b) => {
    const members = activeMembers(b, visible);
    const now = latest ? blockSum(latest.results, b.members, visible) : null;
    const base = election ? blockSum(election.results, b.members, visible) : null;
    const delta = now != null && base != null ? Math.round((now - base) * 10) / 10 : null;
    return { block: b, members, now, delta };
  });

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-4">
        {summary.map((s) => (
          <div
            key={s.block.id}
            className="flex-1 rounded-lg border-l-4 bg-muted/40 px-4 py-3"
            style={{ borderColor: s.block.color }}
          >
            <div className="text-sm font-medium" style={{ color: s.block.color }}>
              {s.members.length ? s.members.join(" · ") : "Inga partier valda"}
            </div>
            <div className="text-2xl font-bold tabular-nums">
              {s.now != null ? `${s.now.toFixed(1)} %` : "–"}
              {s.delta != null && (
                <span className="ml-2 text-sm font-normal text-muted-foreground">
                  ({s.delta > 0 ? "+" : ""}
                  {s.delta.toFixed(1)} pe)
                </span>
              )}
            </div>
          </div>
        ))}
      </div>

      <TimeSeriesChart
        parties={blockParties}
        visible={allBlocksVisible}
        dots={dots}
        line={line}
        elections={elections}
        pollsterNames={pollsterNames}
        showBand
        emphasizeLine={false}
        fromDate={fromDate}
        toDate={toDate}
      />

      <p className="text-xs leading-relaxed text-muted-foreground">
        Varje prick är summan av blockets ikryssade partier i en mätning; linjen är ett glidande
        30-dagarssnitt med felmarginalband. <strong>Grupperingen är valbar</strong> – klicka ur ett
        parti i listan ovan så tas det bort ur sitt block och summan justeras. Ingen påtvingad
        blockindelning.
        {latest && (
          <>
            {" "}Senaste mätning: {pollsterNames.get(latest.pollsterCode) ?? latest.pollsterCode},{" "}
            {dayFmt.format(new Date(`${latest.publishedAt}T00:00:00`))}.
          </>
        )}
        {election && <> Förändring (pe) visas mot {election.label}.</>}
      </p>
    </div>
  );
}

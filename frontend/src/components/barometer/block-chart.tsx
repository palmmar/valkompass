"use client";

import { useMemo, useState } from "react";
import type { BarometerElection, BarometerParty, BarometerPoll } from "@/lib/barometer-api";
import { TimeSeriesChart } from "./time-series-chart";
import { BlockAssignment } from "./block-assignment";
import {
  DEFAULT_BLOCKS,
  type Group,
  blockSum,
  buildBlockDots,
  defaultAssignment,
  latestElectionBefore,
  rollingAverage,
} from "./blocks";

interface BlockChartProps {
  parties: BarometerParty[];
  polls: BarometerPoll[];
  elections: BarometerElection[];
  fromDate: Date;
  toDate: Date;
  pollsterNames: Map<string, string>;
}

const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "long", year: "numeric" });
const monthLongFmt = new Intl.DateTimeFormat("sv-SE", { month: "long", year: "numeric" });
const asDate = (s: string) => new Date(`${s}T00:00:00`);
const COLOR_A = DEFAULT_BLOCKS[0].color;
const COLOR_B = DEFAULT_BLOCKS[1].color;

/**
 * Blockläge: två fritt sammansatta grupperingar (default S·V·MP·C mot M·KD·L·SD) som linjer
 * med felmarginalband. Användaren placerar själv partierna i Block 1, utanför eller Block 2,
 * och summorna + de stora talen räknas om direkt. Talen följer hårkorset.
 */
export function BlockChart({ parties, polls, elections, fromDate, toDate, pollsterNames }: BlockChartProps) {
  const [assignment, setAssignment] = useState<Record<string, Group>>(() => defaultAssignment());
  const [hoverDate, setHoverDate] = useState<string | null>(null);

  const opinion = useMemo(() => polls.filter((p) => !p.pollsterCode.startsWith("val-")), [polls]);

  // Medlemmar per block, i partiernas displayOrder.
  const blocks = useMemo(
    () =>
      DEFAULT_BLOCKS.map((b) => ({
        id: b.id,
        color: b.color,
        members: parties.filter((p) => (assignment[p.code] ?? "none") === b.id).map((p) => p.code),
      })),
    [parties, assignment],
  );

  const dots = useMemo(
    () => blocks.map((b) => ({ partyCode: b.id, points: buildBlockDots(opinion, b.members) })),
    [blocks, opinion],
  );
  const line = useMemo(() => dots.map((d) => ({ partyCode: d.partyCode, points: rollingAverage(d.points) })), [dots]);

  const blockParties = blocks.map((b, i) => ({
    code: b.id,
    name: b.members.join(" · ") || "—",
    color: b.color,
    displayOrder: i + 1,
  }));
  const allBlocksVisible = useMemo(() => new Set(DEFAULT_BLOCKS.map((b) => b.id)), []);

  // Datum som siffrorna avser: hårkorset om man hovrar, annars senaste mätningen.
  const latestDate = useMemo(
    () => dots.flatMap((d) => d.points).reduce<string | null>((m, p) => (m && m >= p.date ? m : p.date), null),
    [dots],
  );
  const shownDate = hoverDate ?? latestDate;
  const election = useMemo(() => latestElectionBefore(elections, shownDate), [elections, shownDate]);

  const summary = blocks.map((b, i) => {
    const pts = dots[i].points;
    const point = shownDate ? pts.find((p) => p.date === shownDate) : pts.at(-1);
    const now = point?.value ?? null;
    const base = election ? blockSum(election.results, b.members) : null;
    const delta = now != null && base != null ? Math.round((now - base) * 10) / 10 : null;
    return { block: b, members: b.members, now, delta };
  });

  return (
    <div className="space-y-4">
      <BlockAssignment
        parties={parties}
        assignment={assignment}
        colorA={COLOR_A}
        colorB={COLOR_B}
        onChange={(code, group) => setAssignment((prev) => ({ ...prev, [code]: group }))}
        onReset={() => setAssignment(defaultAssignment())}
      />

      <div className="text-sm text-muted-foreground">
        {shownDate ? (
          <>
            {hoverDate ? "Vid " : "Senaste mätning · "}
            <span className="font-medium text-foreground">
              {hoverDate ? monthLongFmt.format(asDate(shownDate)) : dayFmt.format(asDate(shownDate))}
            </span>
          </>
        ) : null}
      </div>

      <div className="flex flex-wrap gap-4">
        {summary.map((s) => (
          <div key={s.block.id} className="flex-1 rounded-lg border-l-4 bg-muted/40 px-4 py-3" style={{ borderColor: s.block.color }}>
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
        onHoverDate={setHoverDate}
      />

      <p className="text-xs leading-relaxed text-muted-foreground">
        Varje prick är summan av blockets partier i en mätning; linjen är ett glidande
        30-dagarssnitt med felmarginalband. <strong>Du väljer själv vilka partier som ingår i
        respektive block</strong> – default är S·V·MP·C mot M·KD·L·SD, men placera om dem som du vill
        (t.ex. C till Block 2, eller S+M mot övriga). Ingen påtvingad blockindelning. Håll musen över
        diagrammet för att läsa av ett visst datum; förändring (pe) visas mot{" "}
        {election ? election.label : "senaste riksdagsval"}.
      </p>
    </div>
  );
}

"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { scaleLinear, scaleTime } from "@visx/scale";
import { LinePath } from "@visx/shape";
import { Group } from "@visx/group";
import { AxisBottom, AxisLeft } from "@visx/axis";
import { GridRows } from "@visx/grid";
import { curveMonotoneX } from "@visx/curve";
import { useTooltip, TooltipWithBounds, defaultStyles } from "@visx/tooltip";
import { localPoint } from "@visx/event";
import type {
  BarometerAvgSeries,
  BarometerElection,
  BarometerParty,
  BarometerSeries,
} from "@/lib/barometer-api";
import { partyColor, withAlpha } from "./colors";

const MARGIN = { top: 16, right: 16, bottom: 28, left: 34 };
const HEIGHT = 440;

interface TooltipDatum {
  partyName: string;
  color: string;
  date: string;
  value: number;
  marginOfError: number | null;
  pollster: string;
}

interface TimeSeriesChartProps {
  parties: BarometerParty[];
  visible: Set<string>;
  dots: BarometerSeries[];
  line: BarometerAvgSeries[];
  elections: BarometerElection[];
  pollsterNames: Map<string, string>;
  /** Visa felmarginalband runt den utjämnade linjen. */
  showBand: boolean;
  /** Poll-of-polls-läge: kraftig linje, nedtonade prickar. */
  emphasizeLine: boolean;
  fromDate: Date;
  toDate: Date;
}

const d = (s: string) => new Date(`${s}T00:00:00`);
const yearFmt = new Intl.DateTimeFormat("sv-SE", { year: "numeric" });
const monthFmt = new Intl.DateTimeFormat("sv-SE", { month: "short", year: "2-digit" });
const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "short", year: "numeric" });

/** Mäter containerns bredd (med en rimlig default före mätning) så diagrammet alltid ritas. */
function useContainerWidth(defaultWidth = 800) {
  const ref = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(defaultWidth);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const update = () => setWidth(el.clientWidth || defaultWidth);
    update();
    const ro = new ResizeObserver(update);
    ro.observe(el);
    return () => ro.disconnect();
  }, [defaultWidth]);
  return [ref, width] as const;
}

export function TimeSeriesChart(props: TimeSeriesChartProps) {
  const [ref, width] = useContainerWidth();
  return (
    <div ref={ref} style={{ height: HEIGHT }} className="w-full">
      <Chart {...props} width={width} />
    </div>
  );
}

function Chart({
  parties,
  visible,
  dots,
  line,
  elections,
  pollsterNames,
  showBand,
  emphasizeLine,
  fromDate,
  toDate,
  width,
}: TimeSeriesChartProps & { width: number }) {
  const { tooltipData, tooltipLeft, tooltipTop, tooltipOpen, showTooltip, hideTooltip } =
    useTooltip<TooltipDatum>();

  const innerW = Math.max(0, width - MARGIN.left - MARGIN.right);
  const innerH = HEIGHT - MARGIN.top - MARGIN.bottom;

  const dotsByParty = useMemo(() => new Map(dots.map((s) => [s.partyCode, s.points])), [dots]);
  const lineByParty = useMemo(() => new Map(line.map((s) => [s.partyCode, s.points])), [line]);
  const inRange = (s: string) => {
    const t = d(s).getTime();
    return t >= fromDate.getTime() && t <= toDate.getTime();
  };

  const xScale = useMemo(
    () => scaleTime({ domain: [fromDate, toDate], range: [0, innerW] }),
    [fromDate, toDate, innerW],
  );

  // y-max från synliga partiers värden i intervallet (prickar + bandets övre kant).
  const yMax = useMemo(() => {
    let max = 10;
    for (const code of visible) {
      for (const p of dotsByParty.get(code) ?? []) if (inRange(p.date)) max = Math.max(max, p.value);
      for (const p of lineByParty.get(code) ?? [])
        if (inRange(p.date)) max = Math.max(max, p.value + (p.marginOfError ?? 0));
    }
    return Math.ceil((max + 2) / 5) * 5;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, dotsByParty, lineByParty, fromDate, toDate]);

  const yScale = useMemo(
    () => scaleLinear({ domain: [0, yMax], range: [innerH, 0] }),
    [yMax, innerH],
  );

  const rangeYears = (toDate.getTime() - fromDate.getTime()) / (365 * 864e5);
  const tickFmt = rangeYears > 3 ? yearFmt : monthFmt;
  const numXTicks = Math.max(2, Math.min(8, Math.floor(innerW / 90)));

  const orderedVisible = parties.filter((p) => visible.has(p.code));

  return (
    <div className="relative">
      <svg width={width} height={HEIGHT} role="img" aria-label="Opinionstrend per parti">
        <Group left={MARGIN.left} top={MARGIN.top}>
          <GridRows scale={yScale} width={innerW} numTicks={6} stroke="currentColor" strokeOpacity={0.08} />

          {/* Valresultat som åtskild referens (vertikal streckad linje + etikett). */}
          {elections
            .filter((e) => inRange(e.date))
            .map((e) => {
              const x = xScale(d(e.date));
              return (
                <g key={e.date}>
                  <line x1={x} x2={x} y1={0} y2={innerH} stroke="currentColor" strokeOpacity={0.35} strokeDasharray="4 3" />
                  <text x={x + 3} y={11} fontSize={10} className="fill-muted-foreground">
                    {e.label}
                  </text>
                </g>
              );
            })}

          {/* Felmarginalband runt linjen. */}
          {showBand &&
            orderedVisible.map((party) => {
              const pts = (lineByParty.get(party.code) ?? []).filter((p) => inRange(p.date) && p.marginOfError != null);
              if (pts.length < 2) return null;
              const upper = pts.map((p) => `${xScale(d(p.date))},${yScale(p.value + (p.marginOfError ?? 0))}`);
              const lower = [...pts].reverse().map((p) => `${xScale(d(p.date))},${yScale(Math.max(0, p.value - (p.marginOfError ?? 0)))}`);
              return (
                <path
                  key={`band-${party.code}`}
                  d={`M${upper.join("L")}L${lower.join("L")}Z`}
                  fill={withAlpha(party.color, 0.12)}
                  stroke="none"
                />
              );
            })}

          {/* Utjämnad linje (månadssnitt eller glidande snitt). */}
          {orderedVisible.map((party) => {
            const pts = (lineByParty.get(party.code) ?? []).filter((p) => inRange(p.date));
            if (pts.length < 2) return null;
            return (
              <LinePath
                key={`line-${party.code}`}
                data={pts}
                x={(p) => xScale(d(p.date))}
                y={(p) => yScale(p.value)}
                stroke={partyColor(party.color)}
                strokeWidth={emphasizeLine ? 2.5 : 1.5}
                strokeOpacity={emphasizeLine ? 1 : 0.9}
                curve={curveMonotoneX}
              />
            );
          })}

          {/* Enskilda mätningar som prickar. */}
          {orderedVisible.map((party) => {
            const pts = (dotsByParty.get(party.code) ?? []).filter((p) => inRange(p.date));
            const color = partyColor(party.color);
            return (
              <g key={`dots-${party.code}`}>
                {pts.map((p, i) => (
                  <circle
                    key={i}
                    cx={xScale(d(p.date))}
                    cy={yScale(p.value)}
                    r={emphasizeLine ? 1.8 : 2.8}
                    fill={color}
                    fillOpacity={emphasizeLine ? 0.22 : 0.7}
                    stroke="white"
                    strokeWidth={0.5}
                    onMouseMove={(ev) => {
                      const local = localPoint(ev.currentTarget.ownerSVGElement!, ev);
                      showTooltip({
                        tooltipLeft: (local?.x ?? 0),
                        tooltipTop: (local?.y ?? 0),
                        tooltipData: {
                          partyName: party.name,
                          color,
                          date: p.date,
                          value: p.value,
                          marginOfError: p.marginOfError,
                          pollster: pollsterNames.get(p.pollsterCode) ?? p.pollsterCode,
                        },
                      });
                    }}
                    onMouseLeave={hideTooltip}
                  />
                ))}
              </g>
            );
          })}

          <AxisLeft
            scale={yScale}
            numTicks={6}
            tickFormat={(v) => `${v}`}
            tickStroke="currentColor"
            hideAxisLine
            tickClassName="opacity-30"
            tickLabelProps={() => ({ fontSize: 10, dx: -4, dy: 3, textAnchor: "end", className: "fill-muted-foreground" })}
          />
          <AxisBottom
            top={innerH}
            scale={xScale}
            numTicks={numXTicks}
            tickFormat={(v) => tickFmt.format(v as Date)}
            stroke="currentColor"
            tickStroke="currentColor"
            axisLineClassName="opacity-20"
            tickClassName="opacity-30"
            tickLabelProps={() => ({ fontSize: 10, dy: 2, textAnchor: "middle", className: "fill-muted-foreground" })}
          />
        </Group>
      </svg>

      {tooltipOpen && tooltipData && (
        <TooltipWithBounds
          top={tooltipTop}
          left={tooltipLeft}
          style={{ ...defaultStyles, padding: "6px 8px", fontSize: 12, lineHeight: 1.4 }}
        >
          <div className="flex items-center gap-1.5 font-medium">
            <span style={{ width: 8, height: 8, borderRadius: 9999, background: tooltipData.color, display: "inline-block" }} />
            {tooltipData.partyName} {tooltipData.value.toFixed(1)} %
            {tooltipData.marginOfError != null && (
              <span style={{ color: "#6b7280" }}>±{tooltipData.marginOfError.toFixed(1)}</span>
            )}
          </div>
          <div style={{ color: "#6b7280" }}>
            {tooltipData.pollster} · {dayFmt.format(d(tooltipData.date))}
          </div>
        </TooltipWithBounds>
      )}
    </div>
  );
}

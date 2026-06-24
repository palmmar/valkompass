"use client";

import { useMemo } from "react";
import { scaleLinear, scaleTime } from "@visx/scale";
import { LinePath } from "@visx/shape";
import { Group } from "@visx/group";
import { AxisBottom, AxisLeft } from "@visx/axis";
import { GridRows } from "@visx/grid";
import { curveMonotoneX } from "@visx/curve";
import { useContainerWidth, STARTED_COLOR, COMPLETED_COLOR } from "./use-container-width";

const MARGIN = { top: 12, right: 14, bottom: 26, left: 30 };
const HEIGHT = 260;
const dateFmt = new Intl.DateTimeFormat("sv-SE", { month: "short", day: "numeric" });
const fullFmt = new Intl.DateTimeFormat("sv-SE", { dateStyle: "medium" });
const parse = (s: string) => new Date(`${s}T00:00:00`);

export interface DailyDatum {
  date: string;
  started: number;
  completed: number;
}

const SERIES = [
  { key: "started", label: "Påbörjade", color: STARTED_COLOR },
  { key: "completed", label: "Slutförda", color: COMPLETED_COLOR },
] as const;

/** Linjediagram över påbörjade och slutförda kompasser per dag. */
export function DailyLineChart({ data }: { data: DailyDatum[] }) {
  const [ref, width] = useContainerWidth();
  const innerW = Math.max(0, width - MARGIN.left - MARGIN.right);
  const innerH = HEIGHT - MARGIN.top - MARGIN.bottom;

  const points = useMemo(
    () => data.map((d) => ({ ...d, t: parse(d.date) })),
    [data],
  );

  const xScale = useMemo(() => {
    const times = points.map((p) => p.t.getTime());
    let min = Math.min(...times);
    let max = Math.max(...times);
    if (min === max) {
      // En enda dag: bredda domänen ±1 dag så linjen/punkten får plats.
      min -= 864e5;
      max += 864e5;
    }
    return scaleTime({ domain: [new Date(min), new Date(max)], range: [0, innerW] });
  }, [points, innerW]);

  const yMax = useMemo(
    () => Math.max(1, ...points.flatMap((p) => [p.started, p.completed])),
    [points],
  );
  const yScale = useMemo(
    () => scaleLinear({ domain: [0, yMax], range: [innerH, 0], nice: true }),
    [yMax, innerH],
  );
  const numXTicks = Math.max(2, Math.min(8, Math.floor(innerW / 80)));

  return (
    <div>
      <div ref={ref} style={{ height: HEIGHT }} className="w-full">
        <svg width={width} height={HEIGHT} role="img" aria-label="Kompasser per dag">
          <Group left={MARGIN.left} top={MARGIN.top}>
            <GridRows scale={yScale} width={innerW} numTicks={4} stroke="currentColor" strokeOpacity={0.08} />
            {SERIES.map((s) => (
              <Group key={s.key}>
                <LinePath
                  data={points}
                  x={(p) => xScale(p.t)}
                  y={(p) => yScale(p[s.key])}
                  stroke={s.color}
                  strokeWidth={2}
                  curve={curveMonotoneX}
                />
                {points.map((p) => (
                  <circle key={`${s.key}-${p.date}`} cx={xScale(p.t)} cy={yScale(p[s.key])} r={2.5} fill={s.color}>
                    <title>{`${fullFmt.format(p.t)} – ${s.label}: ${p[s.key]}`}</title>
                  </circle>
                ))}
              </Group>
            ))}
            <AxisLeft
              scale={yScale}
              numTicks={4}
              tickStroke="currentColor"
              hideAxisLine
              tickClassName="opacity-30"
              tickLabelProps={() => ({ fontSize: 10, dx: -4, dy: 3, textAnchor: "end", className: "fill-muted-foreground" })}
            />
            <AxisBottom
              top={innerH}
              scale={xScale}
              numTicks={numXTicks}
              tickFormat={(v) => dateFmt.format(v as Date)}
              stroke="currentColor"
              tickStroke="currentColor"
              axisLineClassName="opacity-20"
              tickClassName="opacity-30"
              tickLabelProps={() => ({ fontSize: 10, dy: 2, textAnchor: "middle", className: "fill-muted-foreground" })}
            />
          </Group>
        </svg>
      </div>
      <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
        {SERIES.map((s) => (
          <span key={s.key} className="flex items-center gap-1.5">
            <span className="inline-block size-2.5 rounded-sm" style={{ backgroundColor: s.color }} />
            {s.label}
          </span>
        ))}
      </div>
    </div>
  );
}

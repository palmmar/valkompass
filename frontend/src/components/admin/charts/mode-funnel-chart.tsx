"use client";

import { useMemo } from "react";
import { scaleBand, scaleLinear } from "@visx/scale";
import { Bar } from "@visx/shape";
import { Group } from "@visx/group";
import { AxisBottom, AxisLeft } from "@visx/axis";
import { GridRows } from "@visx/grid";
import { useContainerWidth, STARTED_COLOR, COMPLETED_COLOR } from "./use-container-width";

const MARGIN = { top: 8, right: 8, bottom: 28, left: 30 };
const HEIGHT = 240;
const SERIES = [
  { key: "started", label: "Påbörjade", color: STARTED_COLOR },
  { key: "completed", label: "Slutförda", color: COMPLETED_COLOR },
] as const;

export interface ModeDatum {
  label: string;
  started: number;
  completed: number;
}

/** Grupperat stapeldiagram: påbörjade vs slutförda per läge/variant. */
export function ModeFunnelChart({ data }: { data: ModeDatum[] }) {
  const [ref, width] = useContainerWidth();
  const innerW = Math.max(0, width - MARGIN.left - MARGIN.right);
  const innerH = HEIGHT - MARGIN.top - MARGIN.bottom;

  const groupScale = useMemo(
    () => scaleBand({ domain: data.map((d) => d.label), range: [0, innerW], padding: 0.3 }),
    [data, innerW],
  );
  const barScale = useMemo(
    () => scaleBand({ domain: SERIES.map((s) => s.key), range: [0, groupScale.bandwidth()], padding: 0.15 }),
    [groupScale],
  );
  const yMax = useMemo(
    () => Math.max(1, ...data.flatMap((d) => [d.started, d.completed])),
    [data],
  );
  const yScale = useMemo(
    () => scaleLinear({ domain: [0, yMax], range: [innerH, 0], nice: true }),
    [yMax, innerH],
  );

  return (
    <div>
      <div ref={ref} style={{ height: HEIGHT }} className="w-full">
        <svg width={width} height={HEIGHT} role="img" aria-label="Påbörjade och slutförda per läge">
          <Group left={MARGIN.left} top={MARGIN.top}>
            <GridRows scale={yScale} width={innerW} numTicks={4} stroke="currentColor" strokeOpacity={0.08} />
            {data.map((d) => {
              const gx = groupScale(d.label) ?? 0;
              return (
                <Group key={d.label} left={gx}>
                  {SERIES.map((s) => {
                    const value = d[s.key];
                    return (
                      <Bar
                        key={s.key}
                        x={barScale(s.key) ?? 0}
                        y={yScale(value)}
                        width={barScale.bandwidth()}
                        height={Math.max(0, innerH - yScale(value))}
                        fill={s.color}
                        rx={2}
                      >
                        <title>{`${d.label} – ${s.label}: ${value}`}</title>
                      </Bar>
                    );
                  })}
                </Group>
              );
            })}
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
              scale={groupScale}
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

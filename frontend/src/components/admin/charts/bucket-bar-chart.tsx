"use client";

import { useMemo } from "react";
import { scaleBand, scaleLinear } from "@visx/scale";
import { Bar } from "@visx/shape";
import { Group } from "@visx/group";
import { AxisBottom, AxisLeft } from "@visx/axis";
import { GridRows } from "@visx/grid";
import { useContainerWidth, COMPLETED_COLOR } from "./use-container-width";

const MARGIN = { top: 8, right: 8, bottom: 26, left: 30 };

export interface BucketDatum {
  label: string;
  value: number;
  /** Längre beskrivning till hover-titeln (annars används label). */
  title?: string;
}

interface Props {
  data: BucketDatum[];
  height?: number;
  color?: string;
  ariaLabel: string;
}

/** Enkelt stapeldiagram för en kategorisk serie (t.ex. slutförda per veckodag/timme). */
export function BucketBarChart({ data, height = 200, color = COMPLETED_COLOR, ariaLabel }: Props) {
  const [ref, width] = useContainerWidth();
  const innerW = Math.max(0, width - MARGIN.left - MARGIN.right);
  const innerH = height - MARGIN.top - MARGIN.bottom;

  const xScale = useMemo(
    () => scaleBand({ domain: data.map((d) => d.label), range: [0, innerW], padding: 0.25 }),
    [data, innerW],
  );
  const yMax = useMemo(() => Math.max(1, ...data.map((d) => d.value)), [data]);
  const yScale = useMemo(
    () => scaleLinear({ domain: [0, yMax], range: [innerH, 0], nice: true }),
    [yMax, innerH],
  );

  return (
    <div ref={ref} style={{ height }} className="w-full">
      <svg width={width} height={height} role="img" aria-label={ariaLabel}>
        <Group left={MARGIN.left} top={MARGIN.top}>
          <GridRows scale={yScale} width={innerW} numTicks={4} stroke="currentColor" strokeOpacity={0.08} />
          {data.map((d) => {
            const barH = Math.max(0, innerH - yScale(d.value));
            return (
              <Bar
                key={d.label}
                x={xScale(d.label) ?? 0}
                y={yScale(d.value)}
                width={xScale.bandwidth()}
                height={barH}
                fill={color}
                rx={2}
              >
                <title>{`${d.title ?? d.label}: ${d.value}`}</title>
              </Bar>
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
            scale={xScale}
            tickStroke="currentColor"
            axisLineClassName="opacity-20"
            tickClassName="opacity-30"
            tickLabelProps={() => ({ fontSize: 10, dy: 2, textAnchor: "middle", className: "fill-muted-foreground" })}
          />
        </Group>
      </svg>
    </div>
  );
}

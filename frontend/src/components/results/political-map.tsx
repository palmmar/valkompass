"use client";

import { useMemo } from "react";
import type { ResultPartyRef, ResultQuestion } from "@/lib/types";
import { partyColor } from "@/lib/scale";
import { AXIS_META, PARTY_AXES, placeUser } from "@/lib/political-axes";

const VIEW = 380;
const MARGIN = 52;
const PLOT = VIEW - 2 * MARGIN;

const x = (econ: number) => MARGIN + (econ / 10) * PLOT;
const y = (galtan: number) => MARGIN + (1 - galtan / 10) * PLOT; // TAN (10) överst

/** Svart eller vit text beroende på bakgrundsfärgens ljushet. */
function readableText(hex: string): string {
  const h = hex.replace("#", "");
  const r = parseInt(h.slice(0, 2), 16);
  const g = parseInt(h.slice(2, 4), 16);
  const b = parseInt(h.slice(4, 6), 16);
  return (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.62 ? "#1a1a1a" : "#ffffff";
}

function starPoints(cx: number, cy: number, outer: number, inner: number): string {
  const pts: string[] = [];
  for (let i = 0; i < 10; i++) {
    const a = (-90 + i * 36) * (Math.PI / 180);
    const r = i % 2 === 0 ? outer : inner;
    pts.push(`${(cx + r * Math.cos(a)).toFixed(1)},${(cy + r * Math.sin(a)).toFixed(1)}`);
  }
  return pts.join(" ");
}

export function PoliticalMap({
  questions,
  parties,
}: {
  questions: ResultQuestion[];
  parties: ResultPartyRef[];
}) {
  const refByCode = new Map(parties.map((p) => [p.code, p]));
  const user = useMemo(() => placeUser(questions), [questions]);
  const placed = user.econ != null && user.galtan != null;

  const nearest = useMemo(() => {
    if (!placed) return null;
    let best: { code: string; d: number } | null = null;
    for (const [code, p] of Object.entries(PARTY_AXES)) {
      const d = Math.hypot(p.econ - user.econ!, p.galtan - user.galtan!);
      if (!best || d < best.d) best = { code, d };
    }
    return best ? refByCode.get(best.code)?.name ?? best.code : null;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [placed, user.econ, user.galtan]);

  const desc = placed
    ? `Din position: ekonomiskt ${user.econ! < 5 ? "vänster" : "höger"}, ${
        user.galtan! < 5 ? "GAL" : "TAN"
      }${nearest ? `. Närmast ${nearest}.` : ""}`
    : "Du har svarat på för få frågor för att placeras på kartan.";

  return (
    <div className="space-y-3">
      <div className="rounded-lg border bg-card p-2 sm:p-4">
        <svg
          viewBox={`0 0 ${VIEW} ${VIEW}`}
          className="h-auto w-full"
          role="img"
          aria-label={`Politisk karta. ${desc}`}
        >
          <desc>{desc}</desc>

          {/* Plotyta + kvadrantkryss */}
          <rect
            x={MARGIN}
            y={MARGIN}
            width={PLOT}
            height={PLOT}
            rx={8}
            className="fill-muted/30 stroke-border"
          />
          <g className="stroke-border" strokeDasharray="4 4">
            <line x1={MARGIN} y1={MARGIN + PLOT / 2} x2={MARGIN + PLOT} y2={MARGIN + PLOT / 2} />
            <line x1={MARGIN + PLOT / 2} y1={MARGIN} x2={MARGIN + PLOT / 2} y2={MARGIN + PLOT} />
          </g>

          {/* Axeletiketter vid kryssets armar */}
          <g className="fill-muted-foreground" fontSize="12">
            <text x={MARGIN - 6} y={MARGIN + PLOT / 2} textAnchor="end" dominantBaseline="middle">
              {AXIS_META.econ.low}
            </text>
            <text
              x={MARGIN + PLOT + 6}
              y={MARGIN + PLOT / 2}
              textAnchor="start"
              dominantBaseline="middle"
            >
              {AXIS_META.econ.high}
            </text>
            <text x={MARGIN + PLOT / 2} y={MARGIN - 10} textAnchor="middle">
              {AXIS_META.galtan.high}
            </text>
            <text x={MARGIN + PLOT / 2} y={MARGIN + PLOT + 18} textAnchor="middle">
              {AXIS_META.galtan.low}
            </text>
          </g>

          {/* Partier */}
          {Object.entries(PARTY_AXES).map(([code, p]) => {
            const ref = refByCode.get(code);
            const fill = partyColor(ref?.color);
            return (
              <g key={code}>
                <title>{`${ref?.name ?? code} — ekonomi ${p.econ.toFixed(1)}, GAL–TAN ${p.galtan.toFixed(1)}`}</title>
                <circle
                  cx={x(p.econ)}
                  cy={y(p.galtan)}
                  r={11}
                  fill={fill}
                  className="stroke-card"
                  strokeWidth={2}
                />
                <text
                  x={x(p.econ)}
                  y={y(p.galtan)}
                  textAnchor="middle"
                  dominantBaseline="central"
                  fontSize="9.5"
                  fontWeight="700"
                  fill={readableText(fill)}
                  style={{ pointerEvents: "none" }}
                >
                  {code}
                </text>
              </g>
            );
          })}

          {/* Användaren (överst) */}
          {placed && (
            <g>
              <title>Du</title>
              <polygon
                points={starPoints(x(user.econ!), y(user.galtan!), 13, 5.5)}
                className="fill-foreground stroke-card"
                strokeWidth={2}
              />
              <text
                x={x(user.econ!) + 15}
                y={y(user.galtan!)}
                dominantBaseline="middle"
                fontSize="12"
                fontWeight="700"
                className="fill-foreground"
              >
                Du
              </text>
            </g>
          )}
        </svg>
      </div>

      {/* Teckenförklaring */}
      <div className="flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <svg width="14" height="14" viewBox="-13 -13 26 26" aria-hidden>
            <polygon points={starPoints(0, 0, 12, 5)} className="fill-foreground" />
          </svg>
          Du
        </span>
        {parties.map((p) => (
          <span key={p.code} className="flex items-center gap-1.5">
            <span
              className="inline-block size-2.5 rounded-full"
              style={{ backgroundColor: partyColor(p.color) }}
            />
            {p.code} – {p.name}
          </span>
        ))}
      </div>

      {!placed && (
        <p className="text-sm text-muted-foreground">
          Du har svarat på för få av frågorna som mäter axlarna för att placeras på kartan.
        </p>
      )}

      <p className="text-xs text-muted-foreground">
        Axlarna är härledda ur partiernas källsatta svar och kalibrerade mot forskningens
        partiplaceringar (Chapel Hill Expert Survey). Din prick beräknas ur dina egna svar med
        samma metod. En förenkling av en mångfacetterad verklighet.
      </p>
    </div>
  );
}

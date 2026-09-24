"use client";

import type { BarometerPoll } from "@/lib/barometer-api";
import { cn } from "@/lib/utils";

const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "short", year: "numeric" });
const fmt = (s: string | null) => (s ? dayFmt.format(new Date(`${s}T00:00:00`)) : "–");

interface PollListProps {
  polls: BarometerPoll[];
  /** externalKey för mätningen som är markerad i grafen. */
  selectedKey?: string | null;
  /** Klick på en rad markerar (eller avmarkerar) mätningen i grafen. */
  onSelect?: (poll: BarometerPoll) => void;
}

/**
 * Proveniens per mätning: institut, fältperiod, antal svar (n) och käll-URL. Gör varje
 * datapunkt spårbar – samma proveniensprincip som partipositionerna i kompassen.
 */
export function PollList({ polls, selectedKey, onSelect }: PollListProps) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
            <th className="py-2 pr-3 font-medium">Institut</th>
            <th className="py-2 pr-3 font-medium">Publicerad</th>
            <th className="py-2 pr-3 font-medium">Fältperiod</th>
            <th className="py-2 pr-3 text-right font-medium">n</th>
            <th className="py-2 font-medium">Källa</th>
          </tr>
        </thead>
        <tbody>
          {polls.map((p) => {
            const selected = p.externalKey === selectedKey;
            return (
              <tr
                key={p.externalKey}
                onClick={onSelect ? () => onSelect(p) : undefined}
                onKeyDown={
                  onSelect
                    ? (e) => {
                        if (e.target !== e.currentTarget) return;
                        if (e.key === "Enter" || e.key === " ") {
                          e.preventDefault();
                          onSelect(p);
                        }
                      }
                    : undefined
                }
                tabIndex={onSelect ? 0 : undefined}
                aria-selected={onSelect ? selected : undefined}
                title={onSelect ? (selected ? "Ta bort markering i grafen" : "Markera mätningen i grafen") : undefined}
                className={cn(
                  "border-b last:border-0",
                  onSelect &&
                    "cursor-pointer transition-colors hover:bg-muted/60 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-ring",
                  selected && "bg-muted shadow-[inset_3px_0_0_var(--color-foreground)]",
                )}
              >
                <td className={cn("py-2 pr-3", onSelect && "pl-2", selected && "font-medium")}>{p.pollsterName}</td>
                <td className="py-2 pr-3 whitespace-nowrap text-muted-foreground">{fmt(p.publishedAt)}</td>
                <td className="py-2 pr-3 whitespace-nowrap text-muted-foreground">
                  {p.fieldStart && p.fieldEnd ? `${fmt(p.fieldStart)}–${fmt(p.fieldEnd)}` : "–"}
                </td>
                <td className="py-2 pr-3 text-right tabular-nums text-muted-foreground">
                  {p.sampleSize != null ? p.sampleSize.toLocaleString("sv-SE") : "–"}
                </td>
                <td className="py-2">
                  {p.sourceUrl ? (
                    <a
                      href={p.sourceUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      onClick={(e) => e.stopPropagation()}
                      className="text-primary underline underline-offset-2 hover:no-underline"
                      title={p.sourceCitation ?? undefined}
                    >
                      Källa
                    </a>
                  ) : (
                    <span className="text-muted-foreground" title={p.sourceCitation ?? undefined}>
                      {p.sourceCitation ?? "–"}
                    </span>
                  )}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

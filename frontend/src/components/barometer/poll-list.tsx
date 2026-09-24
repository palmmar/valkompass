"use client";

import { useMemo, useState } from "react";
import type { BarometerPoll } from "@/lib/barometer-api";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 20;

const dayFmt = new Intl.DateTimeFormat("sv-SE", { day: "numeric", month: "short", year: "numeric" });
const fmt = (s: string | null) => (s ? dayFmt.format(new Date(`${s}T00:00:00`)) : "–");
const yearOf = (s: string) => Number(s.slice(0, 4));
// Valresultat ligger i samma tabell men ritas som vertikala linjer, inte prickar – de kan inte markeras.
const isElection = (p: BarometerPoll) => p.pollsterCode.startsWith("val-");

const chipClass = (active: boolean) =>
  cn(
    "rounded-full border px-3 py-1 text-xs transition-colors",
    active ? "border-foreground bg-foreground text-background" : "text-muted-foreground hover:text-foreground",
  );

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
 * Filtrerbar på institut och år, med sidbläddring.
 */
export function PollList({ polls, selectedKey, onSelect }: PollListProps) {
  const [pollsters, setPollsters] = useState<Set<string>>(new Set());
  const [year, setYear] = useState<number | null>(null);
  const [page, setPage] = useState(0);

  const sorted = useMemo(
    () => [...polls].sort((a, b) => b.publishedAt.localeCompare(a.publishedAt)),
    [polls],
  );

  // Institut i ordning efter antal mätningar, så de vanligaste hamnar först.
  const pollsterOptions = useMemo(() => {
    const counts = new Map<string, { name: string; count: number }>();
    for (const p of sorted) {
      const c = counts.get(p.pollsterCode);
      if (c) c.count++;
      else counts.set(p.pollsterCode, { name: p.pollsterName, count: 1 });
    }
    return [...counts.entries()]
      .map(([code, { name, count }]) => ({ code, name, count }))
      .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name, "sv"));
  }, [sorted]);

  const years = useMemo(() => [...new Set(sorted.map((p) => yearOf(p.publishedAt)))], [sorted]);

  const filtered = useMemo(
    () =>
      sorted.filter(
        (p) => (pollsters.size === 0 || pollsters.has(p.pollsterCode)) && (year == null || yearOf(p.publishedAt) === year),
      ),
    [sorted, pollsters, year],
  );

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const current = Math.min(page, pageCount - 1);
  const rows = filtered.slice(current * PAGE_SIZE, (current + 1) * PAGE_SIZE);

  const togglePollster = (code: string) => {
    setPollsters((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
    setPage(0);
  };
  const clearFilters = () => {
    setPollsters(new Set());
    setYear(null);
    setPage(0);
  };
  const filtering = pollsters.size > 0 || year != null;

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2" role="group" aria-label="Filtrera på institut">
        <button
          type="button"
          onClick={() => {
            setPollsters(new Set());
            setPage(0);
          }}
          aria-pressed={pollsters.size === 0}
          className={chipClass(pollsters.size === 0)}
        >
          Alla institut
        </button>
        {pollsterOptions.map((o) => (
          <button
            key={o.code}
            type="button"
            onClick={() => togglePollster(o.code)}
            aria-pressed={pollsters.has(o.code)}
            className={chipClass(pollsters.has(o.code))}
          >
            {o.name}
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-3 text-sm">
        <label className="flex items-center gap-2 text-muted-foreground">
          År
          <select
            className="h-8 rounded-md border border-input bg-transparent px-2 text-sm text-foreground shadow-xs outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
            value={year ?? ""}
            onChange={(e) => {
              setYear(e.target.value ? Number(e.target.value) : null);
              setPage(0);
            }}
          >
            <option value="">Alla år</option>
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </label>
        <span className="text-muted-foreground" aria-live="polite">
          {filtered.length.toLocaleString("sv-SE")} {filtered.length === 1 ? "mätning" : "mätningar"}
        </span>
        {filtering && (
          <button type="button" onClick={clearFilters} className="text-xs text-muted-foreground underline hover:text-foreground">
            Rensa filter
          </button>
        )}
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <th className="py-2 pr-3 pl-2 font-medium">Institut</th>
              <th className="py-2 pr-3 font-medium">Publicerad</th>
              <th className="py-2 pr-3 font-medium">Fältperiod</th>
              <th className="py-2 pr-3 text-right font-medium">n</th>
              <th className="py-2 font-medium">Källa</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((p) => {
              const selectable = onSelect != null && !isElection(p);
              const selected = p.externalKey === selectedKey;
              return (
                <tr
                  key={p.externalKey}
                  onClick={selectable ? () => onSelect(p) : undefined}
                  onKeyDown={
                    selectable
                      ? (e) => {
                          if (e.target !== e.currentTarget) return;
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            onSelect(p);
                          }
                        }
                      : undefined
                  }
                  tabIndex={selectable ? 0 : undefined}
                  aria-selected={selectable ? selected : undefined}
                  title={selectable ? (selected ? "Ta bort markering i grafen" : "Markera mätningen i grafen") : undefined}
                  className={cn(
                    "border-b last:border-0",
                    selectable &&
                      "cursor-pointer transition-colors hover:bg-muted/60 focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-ring",
                    selected && "bg-muted shadow-[inset_3px_0_0_var(--color-foreground)]",
                  )}
                >
                  <td className={cn("py-2 pr-3 pl-2", selected && "font-medium")}>{p.pollsterName}</td>
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
            {rows.length === 0 && (
              <tr>
                <td colSpan={5} className="py-6 text-center text-muted-foreground">
                  Inga mätningar matchar filtret.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {filtered.length > PAGE_SIZE && (
        <nav className="flex items-center justify-between gap-3 text-sm" aria-label="Sidbläddring">
          <span className="text-muted-foreground tabular-nums">
            {current * PAGE_SIZE + 1}–{Math.min((current + 1) * PAGE_SIZE, filtered.length)} av{" "}
            {filtered.length.toLocaleString("sv-SE")}
          </span>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" disabled={current === 0} onClick={() => setPage(current - 1)}>
              Föregående
            </Button>
            <span className="text-muted-foreground tabular-nums">
              Sida {current + 1} av {pageCount}
            </span>
            <Button variant="outline" size="sm" disabled={current >= pageCount - 1} onClick={() => setPage(current + 1)}>
              Nästa
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}

"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  getAnswerStats,
  getPartyMatchStats,
  getQuizStats,
  listParties,
} from "@/lib/admin-api";
import type { QuestionAnswerStats } from "@/lib/admin-types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

const dateTimeFmt = new Intl.DateTimeFormat("sv-SE", {
  dateStyle: "short",
  timeStyle: "short",
  timeZone: "Europe/Stockholm",
});
const rtf = new Intl.RelativeTimeFormat("sv-SE", { numeric: "auto" });

function relativeTime(iso: string, now: number): string {
  const diffSec = Math.round((new Date(iso).getTime() - now) / 1000);
  const abs = Math.abs(diffSec);
  if (abs < 60) return rtf.format(diffSec, "second");
  if (abs < 3600) return rtf.format(Math.round(diffSec / 60), "minute");
  if (abs < 86400) return rtf.format(Math.round(diffSec / 3600), "hour");
  return rtf.format(Math.round(diffSec / 86400), "day");
}

// Fyrgradig skala: Håller inte med → Håller helt med.
const ANSWER_LEVELS = [
  { key: "stronglyDisagree", label: "Håller inte med", cls: "bg-rose-500" },
  { key: "partlyDisagree", label: "Håller delvis inte med", cls: "bg-orange-400" },
  { key: "partlyAgree", label: "Håller delvis med", cls: "bg-lime-500" },
  { key: "stronglyAgree", label: "Håller helt med", cls: "bg-emerald-600" },
] as const;

function SegmentedBar({ q }: { q: QuestionAnswerStats }) {
  return (
    <div className="flex h-3 w-full overflow-hidden rounded-full bg-muted">
      {ANSWER_LEVELS.map((lvl) => {
        const n = q[lvl.key];
        if (n === 0) return null;
        const pct = (n / q.answered) * 100;
        return (
          <div
            key={lvl.key}
            className={lvl.cls}
            style={{ width: `${pct}%` }}
            title={`${lvl.label}: ${n} (${pct.toFixed(0)} %)`}
          />
        );
      })}
    </div>
  );
}

function groupByCategory(questions: QuestionAnswerStats[]) {
  const groups: { category: string; items: QuestionAnswerStats[] }[] = [];
  for (const q of questions) {
    const last = groups[groups.length - 1];
    if (last && last.category === q.categoryName) last.items.push(q);
    else groups.push({ category: q.categoryName, items: [q] });
  }
  return groups;
}

export default function StatistikPage() {
  const [tab, setTab] = useState("latest");
  const [now] = useState(() => Date.now());

  const stats = useQuery({ queryKey: ["quiz-stats"], queryFn: getQuizStats });
  const answerStats = useQuery({
    queryKey: ["answer-stats"],
    queryFn: getAnswerStats,
    enabled: tab === "answers",
  });
  const partyStats = useQuery({
    queryKey: ["party-match-stats"],
    queryFn: getPartyMatchStats,
    enabled: tab === "party",
  });
  const parties = useQuery({
    queryKey: ["parties"],
    queryFn: listParties,
    enabled: tab === "party",
  });

  const cards = [
    { title: "Totalt", count: stats.data?.total },
    { title: "Senaste dygnet", count: stats.data?.last24h },
    { title: "Senaste 7 dagarna", count: stats.data?.last7d },
  ];

  const latest = stats.data?.latest ?? [];
  const answerGroups = groupByCategory(answerStats.data?.questions ?? []);
  const partyMeta = new Map((parties.data ?? []).map((p) => [p.code, p]));
  const partyTotal = partyStats.data?.sessions ?? 0;

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">Statistik</h1>
      <p className="text-sm text-muted-foreground">
        Antal genomförda kompasser och aggregerad analys. Visar bara sammanställd statistik –
        inga enskilda svar eller resultat.
      </p>

      <div className="grid gap-4 sm:grid-cols-3">
        {cards.map((c) => (
          <Card key={c.title}>
            <CardHeader>
              <CardTitle className="text-base text-muted-foreground">{c.title}</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-3xl font-bold tabular-nums">{c.count ?? "–"}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <Tabs value={tab} onValueChange={(v) => setTab(v as string)}>
        <TabsList>
          <TabsTrigger value="latest">Senaste</TabsTrigger>
          <TabsTrigger value="answers">Svarsfördelning</TabsTrigger>
          <TabsTrigger value="party">Partimatchning</TabsTrigger>
        </TabsList>

        {/* Senaste resultat */}
        <TabsContent value="latest" className="space-y-3 pt-2">
          <h2 className="text-lg font-medium">Senaste resultat</h2>
          {latest.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {stats.isLoading ? "Laddar…" : "Inga kompasser har genomförts än."}
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Tidpunkt</TableHead>
                  <TableHead className="text-right">När</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {latest.map((s) => (
                  <TableRow key={s.id}>
                    <TableCell className="tabular-nums">
                      {dateTimeFmt.format(new Date(s.completedAt))}
                    </TableCell>
                    <TableCell className="text-right text-muted-foreground">
                      {relativeTime(s.completedAt, now)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </TabsContent>

        {/* Svarsfördelning per fråga */}
        <TabsContent value="answers" className="space-y-6 pt-2">
          {answerStats.isLoading ? (
            <p className="text-sm text-muted-foreground">Laddar…</p>
          ) : answerGroups.length === 0 ? (
            <p className="text-sm text-muted-foreground">Inga svar har registrerats än.</p>
          ) : (
            <>
              <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                {ANSWER_LEVELS.map((lvl) => (
                  <span key={lvl.key} className="flex items-center gap-1.5">
                    <span className={`inline-block size-2.5 rounded-sm ${lvl.cls}`} />
                    {lvl.label}
                  </span>
                ))}
              </div>
              {answerGroups.map((group) => (
                <div key={group.category} className="space-y-3">
                  <h3 className="text-sm font-semibold text-muted-foreground">
                    {group.category}
                  </h3>
                  <div className="space-y-4">
                    {group.items.map((q) => (
                      <div key={q.questionId} className="space-y-1.5">
                        <div className="flex items-baseline justify-between gap-3">
                          <span className="text-sm">{q.text}</span>
                          <span className="shrink-0 text-xs text-muted-foreground tabular-nums">
                            n = {q.total}
                          </span>
                        </div>
                        {q.suppressed ? (
                          <p className="text-xs text-muted-foreground italic">För få svar</p>
                        ) : (
                          <>
                            <SegmentedBar q={q} />
                            <p className="text-xs text-muted-foreground">
                              hoppat över: {q.skipped} · viktigast: {q.important}
                            </p>
                          </>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </>
          )}
        </TabsContent>

        {/* Partimatchning */}
        <TabsContent value="party" className="space-y-3 pt-2">
          <h2 className="text-lg font-medium">Bästa partimatchning</h2>
          {partyStats.isLoading ? (
            <p className="text-sm text-muted-foreground">Laddar…</p>
          ) : partyTotal === 0 ? (
            <p className="text-sm text-muted-foreground">Inga kompasser har genomförts än.</p>
          ) : (partyStats.data?.slices.length ?? 0) === 0 ? (
            <p className="text-sm text-muted-foreground">
              För få svar för att visa fördelning.
            </p>
          ) : (
            <>
              <div className="space-y-2">
                {partyStats.data?.slices.map((s) => {
                  const meta = partyMeta.get(s.partyCode);
                  const pct = (s.count / partyTotal) * 100;
                  return (
                    <div key={s.partyCode} className="flex items-center gap-3 text-sm">
                      <span className="w-10 shrink-0 font-medium">
                        {meta?.code ?? s.partyCode}
                      </span>
                      <div className="h-3 flex-1 overflow-hidden rounded-full bg-muted">
                        <div
                          className="h-full rounded-full"
                          style={{
                            width: `${pct}%`,
                            backgroundColor: meta?.color ?? "var(--color-muted-foreground)",
                          }}
                          title={meta?.name ?? s.partyCode}
                        />
                      </div>
                      <span className="w-20 shrink-0 text-right text-muted-foreground tabular-nums">
                        {s.count} ({pct.toFixed(0)} %)
                      </span>
                    </div>
                  );
                })}
              </div>
              {(partyStats.data?.tied ?? 0) > 0 && (
                <p className="text-xs text-muted-foreground">
                  {partyStats.data?.tied} kompasser hade oavgjord topplacering och räknas inte
                  med.
                </p>
              )}
              <p className="text-xs text-muted-foreground italic">
                Aggregerad fördelning över alla kompasser – kan inte kopplas till enskilda
                användare.
              </p>
            </>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}

"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getQuizStats } from "@/lib/admin-api";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

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

export default function StatistikPage() {
  const stats = useQuery({ queryKey: ["quiz-stats"], queryFn: getQuizStats });
  // Snapshot vid sidladdning – de relativa tiderna behöver inte ticka i realtid.
  const [now] = useState(() => Date.now());

  const cards = [
    { title: "Totalt", count: stats.data?.total },
    { title: "Senaste dygnet", count: stats.data?.last24h },
    { title: "Senaste 7 dagarna", count: stats.data?.last7d },
  ];

  const latest = stats.data?.latest ?? [];

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold">Statistik</h1>
      <p className="text-sm text-muted-foreground">
        Antal genomförda kompasser och tidpunkt för de senaste. Visar bara aktivitet – inga
        svar eller partiresultat.
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

      <div className="space-y-3">
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
      </div>
    </div>
  );
}

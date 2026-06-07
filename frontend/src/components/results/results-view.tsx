"use client";

import { useState } from "react";
import { toast } from "sonner";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Check, Info, Share2, Star } from "lucide-react";
import type { ResultDocument, ResultPartyScore } from "@/lib/types";
import { formatPct, partyColor, scaleShort } from "@/lib/scale";

export function ResultsView({ doc }: { doc: ResultDocument }) {
  const partyByCode = new Map(doc.parties.map((p) => [p.code, p]));
  const name = (code: string) => partyByCode.get(code)?.name ?? code;
  const color = (code: string) => partyColor(partyByCode.get(code)?.color);

  const top = doc.overall.find((p) => p.agreementPct != null);

  return (
    <div className="mx-auto max-w-3xl space-y-8 px-4 py-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-bold tracking-tight">Ditt resultat</h1>
        <ShareButton />
      </div>

      {top && (
        <Card>
          <CardContent className="flex items-center gap-4 pt-6">
            <div
              className="flex size-14 shrink-0 items-center justify-center rounded-full text-lg font-bold text-white"
              style={{ backgroundColor: color(top.partyCode) }}
            >
              {top.partyCode}
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Störst överensstämmelse</p>
              <p className="text-lg font-semibold">
                {name(top.partyCode)} – {formatPct(top.agreementPct)}
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Överensstämmelse per parti</h2>
        <div className="space-y-3">
          {doc.overall.map((p) => (
            <ScoreRow key={p.partyCode} label={name(p.partyCode)} score={p} color={color(p.partyCode)} />
          ))}
        </div>
      </section>

      <Tabs defaultValue="categories">
        <TabsList>
          <TabsTrigger value="categories">Per område</TabsTrigger>
          <TabsTrigger value="questions">Fråga för fråga</TabsTrigger>
        </TabsList>

        <TabsContent value="categories" className="pt-4">
          <Accordion className="w-full">
            {doc.categories.map((cat) => (
              <AccordionItem key={cat.slug} value={cat.slug}>
                <AccordionTrigger>{cat.name}</AccordionTrigger>
                <AccordionContent className="space-y-3">
                  {cat.parties.map((p) => (
                    <ScoreRow
                      key={p.partyCode}
                      label={name(p.partyCode)}
                      score={p}
                      color={color(p.partyCode)}
                    />
                  ))}
                </AccordionContent>
              </AccordionItem>
            ))}
          </Accordion>
        </TabsContent>

        <TabsContent value="questions" className="pt-4">
          <Accordion className="w-full">
            {doc.questions.map((q) => {
              const parties = [...q.parties].sort(
                (a, b) => (b.agreementPct ?? -1) - (a.agreementPct ?? -1),
              );
              return (
                <AccordionItem key={q.questionId} value={String(q.questionId)}>
                  <AccordionTrigger className="text-left">
                    <span className="flex flex-col gap-1">
                      <span>{q.text}</span>
                      <span className="flex items-center gap-2 text-xs font-normal text-muted-foreground">
                        Ditt svar: {q.skipped ? "Hoppade över" : scaleShort(q.userValue)}
                        {q.isImportant && (
                          <Badge variant="secondary" className="gap-1">
                            <Star className="size-3" /> Extra viktig
                          </Badge>
                        )}
                      </span>
                    </span>
                  </AccordionTrigger>
                  <AccordionContent className="space-y-3">
                    {q.explanation && (
                      <p className="text-sm text-muted-foreground">{q.explanation}</p>
                    )}
                    <ul className="divide-y rounded-md border">
                      {parties.map((p) => (
                        <li key={p.partyCode} className="space-y-1 p-3">
                          <div className="flex items-center justify-between gap-2">
                            <span className="flex items-center gap-2 font-medium">
                              <span
                                className="inline-block size-3 rounded-full"
                                style={{ backgroundColor: color(p.partyCode) }}
                              />
                              {name(p.partyCode)}
                            </span>
                            <span className="text-sm text-muted-foreground">
                              {scaleShort(p.partyValue)} · {formatPct(p.agreementPct)}
                            </span>
                          </div>
                          {p.motivation && (
                            <p className="text-sm text-muted-foreground">{p.motivation}</p>
                          )}
                          {p.sourceCitation && (
                            <p className="text-xs text-muted-foreground/80">
                              Källa:{" "}
                              {p.sourceUrl ? (
                                <a
                                  href={p.sourceUrl}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className="underline"
                                >
                                  {p.sourceCitation}
                                </a>
                              ) : (
                                p.sourceCitation
                              )}
                            </p>
                          )}
                        </li>
                      ))}
                    </ul>
                  </AccordionContent>
                </AccordionItem>
              );
            })}
          </Accordion>
        </TabsContent>
      </Tabs>

      <Alert>
        <Info className="size-4" />
        <AlertTitle>Om resultatet</AlertTitle>
        <AlertDescription>{doc.disclaimer}</AlertDescription>
      </Alert>

      <div className="text-center">
        <Button render={<Link href="/quiz" />} nativeButton={false} variant="outline">
          Gör om testet
        </Button>
      </div>
    </div>
  );
}

function ScoreRow({
  label,
  score,
  color,
}: {
  label: string;
  score: ResultPartyScore;
  color: string;
}) {
  const pct = score.agreementPct;
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span className="font-medium">{label}</span>
        <span className="tabular-nums text-muted-foreground">
          {pct == null ? "Otillräckligt underlag" : formatPct(pct)}
        </span>
      </div>
      <div className="h-2.5 w-full overflow-hidden rounded-full bg-muted">
        <div
          className="h-full rounded-full transition-all"
          style={{ width: `${pct ?? 0}%`, backgroundColor: color }}
        />
      </div>
    </div>
  );
}

function ShareButton() {
  const [copied, setCopied] = useState(false);
  async function share() {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      toast.success("Länk kopierad – dela ditt resultat!");
      setTimeout(() => setCopied(false), 2000);
    } catch {
      toast.error("Kunde inte kopiera länken.");
    }
  }
  return (
    <Button variant="outline" size="sm" onClick={share}>
      {copied ? <Check className="size-4" /> : <Share2 className="size-4" />}
      {copied ? "Kopierad" : "Dela resultat"}
    </Button>
  );
}

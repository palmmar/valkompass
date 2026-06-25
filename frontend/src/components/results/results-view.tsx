"use client";

import { useEffect, useState } from "react";
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
import { Check, ChevronDown, Info, Share2, Star, FlaskConical } from "lucide-react";
import { PartyLogo } from "@/components/party-logo";
import { PoliticalMap } from "@/components/results/political-map";
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

      {doc.experimental && (
        <Alert>
          <FlaskConical className="size-4" />
          <AlertTitle>Experimentellt resultat</AlertTitle>
          <AlertDescription>
            Det här resultatet kommer från det förenklade swajp-testet, där varje fråga bara
            besvaras med håller med eller inte. Det ger en grövre matchning och kan vara mindre
            träffsäkert än det vanliga testet.
          </AlertDescription>
        </Alert>
      )}

      {top && (
        <WinnerCallout
          code={top.partyCode}
          label={name(top.partyCode)}
          pct={top.agreementPct ?? 0}
          color={color(top.partyCode)}
        />
      )}

      <section className="space-y-4">
        <div>
          <h2 className="text-lg font-semibold">Politisk karta</h2>
          <p className="text-sm text-muted-foreground">
            Var du och partierna står på ekonomi och GAL–TAN.
          </p>
        </div>
        <PoliticalMap questions={doc.questions} parties={doc.parties} />
        <details className="group rounded-lg border bg-card/50 px-4 py-3 text-sm">
          <summary className="flex cursor-pointer list-none items-center justify-between gap-2 font-medium [&::-webkit-details-marker]:hidden">
            Vad betyder GAL–TAN?
            <ChevronDown className="size-4 shrink-0 text-muted-foreground transition-transform group-open:rotate-180" />
          </summary>
          <div className="space-y-2 pt-3 text-muted-foreground">
            <p>
              GAL–TAN är en värderingsskala som kompletterar den ekonomiska vänster–höger-skalan.
              Där vänster–höger handlar om ekonomi och fördelning – hur mycket staten ska styra och
              omfördela – fångar GAL–TAN sociala och kulturella värderingar. De flesta partier och
              personer hamnar någonstans mellan ytterligheterna.
            </p>
            <p>
              <span className="font-medium text-foreground">GAL</span> står för{" "}
              <span className="font-medium text-foreground">Grön, Alternativ och Libertär</span>{" "}
              (libertariansk) – en frihetlig sida som betonar miljö och klimat, öppenhet,
              individens fri- och rättigheter, mångfald och tolerans.
            </p>
            <p>
              <span className="font-medium text-foreground">TAN</span> står för{" "}
              <span className="font-medium text-foreground">
                Traditionell, Auktoritär och Nationalistisk
              </span>{" "}
              – en sida som betonar ordning och trygghet, fasta normer och traditioner, starkare
              statlig auktoritet och nationell gemenskap.
            </p>
          </div>
        </details>
      </section>

      <section className="space-y-4">
        <h2 className="text-lg font-semibold">Överensstämmelse per parti</h2>
        <div className="space-y-3">
          {doc.overall.map((p) => (
            <ScoreRow
              key={p.partyCode}
              code={p.partyCode}
              label={name(p.partyCode)}
              score={p}
              color={color(p.partyCode)}
            />
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
            {doc.categories.map((cat) => {
              // Partiet som stämmer bäst i området plus alla som ligger inom 2 %-enheter
              // från det – visas som logotyper redan i den hopfällda rubriken, så att man
              // ser sina toppmatchningar per område utan att fälla ut.
              const ranked = cat.parties.filter((p) => p.agreementPct != null);
              const maxPct = ranked.reduce((m, p) => Math.max(m, p.agreementPct!), -Infinity);
              const bestMatches = ranked.filter((p) => maxPct - p.agreementPct! <= 2);
              return (
                <AccordionItem key={cat.slug} value={cat.slug}>
                  <AccordionTrigger className="text-left">
                    <span className="flex flex-col gap-1">
                      <span>{cat.name}</span>
                      <MatchLogos
                        label="Stämmer bäst:"
                        parties={bestMatches}
                        name={name}
                        color={color}
                      />
                    </span>
                  </AccordionTrigger>
                  <AccordionContent className="space-y-3">
                    {cat.parties.map((p) => (
                      <ScoreRow
                        key={p.partyCode}
                        code={p.partyCode}
                        label={name(p.partyCode)}
                        score={p}
                        color={color(p.partyCode)}
                      />
                    ))}
                  </AccordionContent>
                </AccordionItem>
              );
            })}
          </Accordion>
        </TabsContent>

        <TabsContent value="questions" className="pt-4">
          <Accordion className="w-full">
            {doc.questions.map((q) => {
              const parties = [...q.parties].sort(
                (a, b) => (b.agreementPct ?? -1) - (a.agreementPct ?? -1),
              );
              // Partier som svarat exakt som användaren (100 % överensstämmelse) – visas
              // som logotyper redan i den hopfällda rubriken, så att man slipper öppna
              // varje fråga för att se vilka man matchar fullt ut.
              const fullMatches = parties.filter(
                (p) => p.agreementPct != null && p.agreementPct >= 99.95,
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
                      <MatchLogos label="Samma svar:" parties={fullMatches} name={name} color={color} />
                    </span>
                  </AccordionTrigger>
                  <AccordionContent className="space-y-3">
                    {(q.explanation || q.explanationSourceUrl) && (
                      <p className="text-sm text-muted-foreground">
                        {q.explanation}
                        {q.explanationSourceUrl && (
                          <>
                            {q.explanation ? " " : ""}
                            <a
                              href={q.explanationSourceUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="whitespace-nowrap underline underline-offset-2"
                            >
                              Läs mer ›
                            </a>
                          </>
                        )}
                      </p>
                    )}
                    <ul className="divide-y rounded-md border">
                      {parties.map((p) => (
                        <li key={p.partyCode} className="space-y-1 p-3">
                          <div className="flex items-center justify-between gap-2">
                            <span className="flex items-center gap-2 font-medium">
                              <PartyLogo code={p.partyCode} color={color(p.partyCode)} size={20} />
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

/**
 * En rad med partilogotyper i en accordion-rubrik (t.ex. "Samma svar:" per fråga eller
 * "Stämmer bäst:" per område). Döljs helt när det inte finns några partier att visa.
 */
function MatchLogos({
  label,
  parties,
  name,
  color,
}: {
  label: string;
  parties: { partyCode: string }[];
  name: (code: string) => string;
  color: (code: string) => string;
}) {
  if (parties.length === 0) return null;
  return (
    <span className="flex flex-wrap items-center gap-1.5 text-xs font-normal text-muted-foreground">
      <span>{label}</span>
      {parties.map((p) => (
        <span key={p.partyCode} title={name(p.partyCode)} className="inline-flex">
          <PartyLogo code={p.partyCode} color={color(p.partyCode)} size={20} />
        </span>
      ))}
    </span>
  );
}

/** Räknar upp 0 → target med ease-out när komponenten monteras. */
function useCountUp(target: number, duration = 1200) {
  const [value, setValue] = useState(0);
  useEffect(() => {
    let raf = 0;
    const start = performance.now();
    const tick = (now: number) => {
      const p = Math.min(1, (now - start) / duration);
      setValue(target * (1 - Math.pow(1 - p, 3)));
      if (p < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [target, duration]);
  return value;
}

/** Redaktionell "vinnar"-callout: störst överensstämmelse, med uppräknande procent. */
function WinnerCallout({
  code,
  label,
  pct,
  color,
}: {
  code: string;
  label: string;
  pct: number;
  color: string;
}) {
  const count = useCountUp(pct);
  return (
    <Card className="overflow-hidden">
      <div className="h-1.5 w-full" style={{ backgroundColor: color }} />
      <CardContent className="pt-6">
        <div className="font-mono text-xs font-medium uppercase tracking-[0.16em] text-primary">
          Störst överensstämmelse
        </div>
        <div className="mt-4 flex items-center gap-4 sm:gap-5">
          <PartyLogo code={code} color={color} size={56} />
          <div className="min-w-0 flex-1">
            <p className="font-heading text-xl font-semibold leading-tight sm:text-3xl">
              {label}
            </p>
            <p className="mt-1 text-sm text-muted-foreground">
              av dina svar sammanfaller med partiets källsatta positioner
            </p>
          </div>
          <div className="font-heading text-4xl font-semibold leading-none tabular-nums sm:text-6xl">
            {Math.round(count)}
            <span className="ml-0.5 text-xl text-muted-foreground sm:text-2xl">%</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function ScoreRow({
  code,
  label,
  score,
  color,
}: {
  code: string;
  label: string;
  score: ResultPartyScore;
  color: string;
}) {
  const pct = score.agreementPct;
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span className="flex items-center gap-2 font-medium">
          <PartyLogo code={code} color={color} size={22} />
          {label}
        </span>
        <span className="font-mono text-xs tabular-nums text-muted-foreground sm:text-sm">
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

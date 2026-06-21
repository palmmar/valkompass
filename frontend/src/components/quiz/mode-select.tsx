"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Zap, ListChecks, Layers, History, FlaskConical } from "lucide-react";
import { useQuizStore } from "@/stores/quiz-store";
import { useHydrated } from "@/hooks/use-hydrated";
import type { QuizMode } from "@/lib/api";

const MODES = [
  {
    mode: 25,
    icon: Zap,
    title: "Snabb",
    duration: "ca 5 min",
    body: "De 25 viktigaste frågorna – en snabb överblick av var du står.",
  },
  {
    mode: 50,
    icon: ListChecks,
    title: "Standard",
    duration: "ca 10 min",
    body: "50 frågor som täcker alla områden – en balanserad matchning.",
  },
  {
    mode: 75,
    icon: Layers,
    title: "Fördjupning",
    duration: "ca 15 min",
    body: "Alla 75 frågor – den mest träffsäkra bilden av hur du matchar partierna.",
  },
];

export function ModeSelect() {
  const mode = useQuizStore((s) => s.mode);
  const variant = useQuizStore((s) => s.variant);
  const answers = useQuizStore((s) => s.answers);
  const start = useQuizStore((s) => s.start);
  const reset = useQuizStore((s) => s.reset);

  // Vänta in att persist rehydrerat innan vi läser sparat framsteg, annars
  // skiljer sig server- och klientrenderingen åt (hydration-mismatch).
  const hydrated = useHydrated();

  const answeredCount = Object.values(answers).filter(
    (a) => a.value != null || a.isSkipped,
  ).length;
  const hasProgress = hydrated && mode != null && answeredCount > 0;
  const resumeHref =
    variant === "swipe" ? "/quiz?mode=25&format=swipe" : `/quiz?mode=${mode}`;

  return (
    <div className="mx-auto max-w-3xl px-4 py-12">
      <div className="text-center">
        <h1 className="text-balance text-3xl font-bold tracking-tight">
          Hur grundlig vill du vara?
        </h1>
        <p className="mx-auto mt-3 max-w-xl text-balance text-muted-foreground">
          Välj hur många påståenden du vill ta ställning till. Fler frågor ger en
          mer träffsäker matchning – du kan hoppa över frågor du är osäker på.
        </p>
        <p className="mx-auto mt-3 max-w-xl text-balance text-xs text-muted-foreground">
          Dina svar sparas lokalt i din webbläsare så att du kan fortsätta senare.
          Inget skickas till oss förrän du väljer att se ditt resultat.{" "}
          <Link href="/om" className="underline underline-offset-2">
            Läs mer
          </Link>
        </p>
      </div>

      {hasProgress && (
        <Card className="mt-8 border-primary/40 bg-primary/5">
          <CardContent className="flex flex-col gap-3 pt-6 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-start gap-3">
              <History className="mt-0.5 size-5 text-muted-foreground" />
              <div>
                <h2 className="font-semibold">Du har en påbörjad kompass</h2>
                <p className="text-sm text-muted-foreground">
                  {answeredCount} av {mode} frågor besvarade{variant === "swipe" ? " (swajp)" : ""}.
                </p>
              </div>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button variant="ghost" onClick={() => reset()}>
                Börja om
              </Button>
              <Button render={<Link href={resumeHref} />} nativeButton={false}>
                Fortsätt
              </Button>
            </div>
          </CardContent>
        </Card>
      )}

      <div className="mt-8 grid gap-4 sm:grid-cols-3">
        {MODES.map((m) => (
          <Card key={m.mode}>
            <CardContent className="flex h-full flex-col gap-2 pt-6">
              <m.icon className="size-6 text-muted-foreground" />
              <h2 className="font-semibold">
                {m.title}
                <span className="ml-2 text-sm font-normal text-muted-foreground">
                  {m.mode} frågor · {m.duration}
                </span>
              </h2>
              <p className="flex-1 text-sm text-muted-foreground">{m.body}</p>
              <Button
                render={<Link href={`/quiz?mode=${m.mode}`} />}
                nativeButton={false}
                onClick={() => start(m.mode as QuizMode)}
                className="mt-2"
                variant={m.mode === 50 ? "default" : "outline"}
              >
                {hasProgress ? "Starta ny" : "Starta"}
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>

      <Card className="mt-4 border-dashed">
        <CardContent className="flex flex-col gap-3 pt-6 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-start gap-3">
            <FlaskConical className="mt-0.5 size-6 shrink-0 text-muted-foreground" />
            <div>
              <h2 className="flex items-center gap-2 font-semibold">
                Swajpa
                <Badge variant="secondary">Experimentell</Badge>
              </h2>
              <p className="text-sm text-muted-foreground">
                Snabbtestets 25 frågor som ett förenklat swajp-test – håller med eller inte,
                klart på nolltid. Kan ge en mindre träffsäker bild.
              </p>
            </div>
          </div>
          <Button
            render={<Link href="/quiz?mode=25&format=swipe" />}
            nativeButton={false}
            onClick={() => start(25, "swipe")}
            variant="outline"
            className="shrink-0"
          >
            Prova
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

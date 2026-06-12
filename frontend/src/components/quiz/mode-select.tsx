"use client";

import { useEffect } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Zap, ListChecks, Layers } from "lucide-react";
import { useQuizStore } from "@/stores/quiz-store";

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
  const reset = useQuizStore((s) => s.reset);

  // Ny omgång: rensa ev. svar och position från en tidigare påbörjad kompass.
  useEffect(() => reset(), [reset]);

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
      </div>

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
                className="mt-2"
                variant={m.mode === 50 ? "default" : "outline"}
              >
                Starta
              </Button>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}

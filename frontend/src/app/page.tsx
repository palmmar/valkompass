import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { ElectionCountdown } from "@/components/election-countdown";
import { Info, ListChecks, Scale, Share2 } from "lucide-react";

const STEPS = [
  {
    icon: ListChecks,
    title: "Svara på påståenden",
    body: "Ta ställning till frågor inom de områden väljarna tycker är viktigast just nu.",
  },
  {
    icon: Scale,
    title: "Få din matchning",
    body: "Se hur väl dina åsikter stämmer överens med de åtta riksdagspartierna – totalt och per område.",
  },
  {
    icon: Share2,
    title: "Jämför och dela",
    body: "Granska parti för parti och fråga för fråga, med källor. Dela ditt resultat med en länk.",
  },
];

function Kicker({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex items-center gap-3 font-mono text-xs font-medium uppercase tracking-[0.16em] text-primary">
      <span className="h-px w-10 bg-primary" />
      {children}
    </div>
  );
}

export default function Home() {
  return (
    <div className="mx-auto max-w-5xl px-4">
      <section className="py-16 sm:py-24">
        <Kicker>Valet 13 september 2026</Kicker>
        <h1 className="mt-6 max-w-3xl text-balance text-4xl font-semibold leading-[1.04] tracking-tight sm:text-6xl">
          Vilket parti tycker som du inför valet 2026?
        </h1>
        <p className="mt-6 max-w-2xl text-balance text-lg leading-relaxed text-muted-foreground">
          Svara på ett antal påståenden och se hur väl dina åsikter matchar
          riksdagspartiernas. Frågorna utgår från de samhällsfrågor väljarna
          anser är viktigast just nu.
        </p>
        <div className="mt-8">
          <Button render={<Link href="/quiz" />} nativeButton={false} size="lg">
            Starta valkompassen
          </Button>
        </div>
        <ElectionCountdown />
      </section>

      <section className="border-t pt-12">
        <Kicker>Så funkar det</Kicker>
        <div className="mt-6 grid gap-4 sm:grid-cols-3">
          {STEPS.map((s, i) => (
            <Card key={s.title}>
              <CardContent className="space-y-3 pt-6">
                <div className="flex items-center justify-between">
                  <span className="flex size-14 items-center justify-center rounded-xl bg-primary/10 text-primary">
                    <s.icon className="size-7" />
                  </span>
                  <span className="font-mono text-xs text-muted-foreground">
                    {String(i + 1).padStart(2, "0")}
                  </span>
                </div>
                <h2 className="text-lg font-semibold">{s.title}</h2>
                <p className="text-sm leading-relaxed text-muted-foreground">{s.body}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      <Alert className="my-12">
        <Info className="size-4" />
        <AlertTitle>Ett obundet verktyg</AlertTitle>
        <AlertDescription>
          Valkompassen är inte knuten till något parti. Partiernas positioner
          bygger på primärkällor och källhänvisas. Resultatet är vägledande, inte
          en rekommendation.
        </AlertDescription>
      </Alert>
    </div>
  );
}

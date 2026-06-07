import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
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

export default function Home() {
  return (
    <div className="mx-auto max-w-5xl px-4">
      <section className="py-16 text-center sm:py-24">
        <h1 className="text-balance text-4xl font-bold tracking-tight sm:text-5xl">
          Vilket parti tycker som du inför valet 2026?
        </h1>
        <p className="mx-auto mt-5 max-w-2xl text-balance text-lg text-muted-foreground">
          Svara på ett antal påståenden och se hur väl dina åsikter matchar
          riksdagspartiernas. Frågorna utgår från de samhällsfrågor väljarna
          anser är viktigast just nu.
        </p>
        <div className="mt-8">
          <Button render={<Link href="/quiz" />} nativeButton={false} size="lg">
            Starta valkompassen
          </Button>
        </div>
      </section>

      <section className="grid gap-4 sm:grid-cols-3">
        {STEPS.map((s) => (
          <Card key={s.title}>
            <CardContent className="space-y-2 pt-6">
              <s.icon className="size-6 text-muted-foreground" />
              <h2 className="font-semibold">{s.title}</h2>
              <p className="text-sm text-muted-foreground">{s.body}</p>
            </CardContent>
          </Card>
        ))}
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

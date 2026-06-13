import type { Metadata } from "next";
import { BarometerQueryProvider } from "@/components/barometer/query-provider";
import { BarometerView } from "./barometer-view";

export const metadata: Metadata = {
  title: "Valbarometer – Valkompass 2026",
  description:
    "Oberoende, källbelagd opinionsdata inför riksdagsvalet 2026: riksdagspartiernas stöd över tid och i nuläget, med felmarginaler och full proveniens. Opinion – inte prognos.",
};

export default function BarometerPage() {
  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-8">
      <header className="space-y-2">
        <h1 className="text-2xl font-bold tracking-tight">Valbarometer</h1>
        <p className="max-w-3xl text-muted-foreground">
          Oberoende, källbelagd opinionsdata inför riksdagsvalet 2026. Här ser du de åtta
          riksdagspartiernas stöd över tid och i nuläget – välj själv vilka partier som visas.
          Alla partier behandlas lika; ingen rangordnas efter kvalitet. Detta är fristående från
          testet och vet ingenting om dina svar.
        </p>
      </header>
      <BarometerQueryProvider>
        <BarometerView />
      </BarometerQueryProvider>
    </div>
  );
}

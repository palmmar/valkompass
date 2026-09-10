import type { Metadata } from "next";
import { ElectionQueryProvider } from "@/components/valvaka/query-provider";
import { ValvakaView } from "./valvaka-view";

export const metadata: Metadata = {
  title: "Valvaka 2026 – Valkompass 2026",
  description:
    "Följ rösträkningen i riksdagsvalet 2026 live. Räknat resultat från Valmyndigheten, "
    + "rapporteringsgrad och officiell preliminär mandatfördelning – uppdaterat löpande.",
};

export default function ValvakaPage() {
  return (
    <div className="mx-auto max-w-4xl space-y-6 px-4 py-8">
      <header className="space-y-2">
        <h1 className="text-2xl font-bold tracking-tight">Valvaka 2026</h1>
        {/* Texten får inte påstå att sidan saknar prognos – om den finns eller inte avgörs av
            API-svaret, och sägs i tabellens fotnot och under Källa och metod. */}
        <p className="max-w-3xl text-muted-foreground">
          Rösträkningen i riksdagsvalet, som den rapporteras av Valmyndigheten – uppdaterad
          löpande under valkvällen.
        </p>
      </header>
      <ElectionQueryProvider>
        <ValvakaView />
      </ElectionQueryProvider>
    </div>
  );
}

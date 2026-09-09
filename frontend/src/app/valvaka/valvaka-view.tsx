"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { StatusBar } from "@/components/valvaka/status-bar";
import { ResultsTable } from "@/components/valvaka/results-table";
import { PollsCloseCountdown } from "@/components/valvaka/polls-close-countdown";
import { fetchElectionLive, hasResults, isCounting } from "@/lib/election-api";
import { AlertTriangle, Info } from "lucide-react";

const dateTimeFmt = new Intl.DateTimeFormat("sv-SE", {
  dateStyle: "long",
  timeStyle: "short",
  timeZone: "Europe/Stockholm",
});

export function ValvakaView() {
  const { data, isPending, isError, error, dataUpdatedAt } = useQuery({
    queryKey: ["election", "live"],
    queryFn: fetchElectionLive,
    // Tätt medan rösträkningen pågår, glest annars. Backend hämtar från Valmyndigheten på
    // sitt eget intervall oavsett, så det här påverkar inte lasten uppströms.
    refetchInterval: (query) => {
      const phase = query.state.data?.phase;
      return phase && isCounting(phase) ? 15_000 : 60_000;
    },
  });

  if (isPending) {
    return (
      <div className="space-y-4" aria-busy>
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError) {
    return (
      <Alert variant="destructive">
        <AlertTriangle className="size-4" />
        <AlertTitle>Kunde inte hämta valvakan</AlertTitle>
        <AlertDescription>
          {error instanceof Error ? error.message : "Okänt fel."} Sidan försöker igen automatiskt.
        </AlertDescription>
      </Alert>
    );
  }

  const showResults = hasResults(data.phase) && data.results.length > 0;

  return (
    <div className="space-y-6">
      <StatusBar data={data} />

      {data.source?.stale && (
        <Alert>
          <Info className="size-4" />
          <AlertTitle>Uppdateringen är fördröjd</AlertTitle>
          <AlertDescription>
            Senaste tillgängliga resultat visas. Uppdateringen från Valmyndigheten är
            tillfälligt fördröjd.
          </AlertDescription>
        </Alert>
      )}

      {(data.phase === "preElection" || data.phase === "electionDay") && (
        <section className="rounded-lg border border-dashed p-6 text-center">
          <h2 className="text-lg font-semibold">Valvakan öppnar när vallokalerna stänger</h2>
          <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
            Söndag 13 september kl. 20:00 börjar rösträkningen redovisas. Då visas resultatet
            här, uppdaterat löpande.
          </p>
          <PollsCloseCountdown className="mt-5" />
        </section>
      )}

      {data.phase === "waitingForResults" && (
        <section className="rounded-lg border border-dashed p-6 text-center">
          <h2 className="text-lg font-semibold">Vallokalerna har stängt</h2>
          <p className="mx-auto mt-2 max-w-md text-sm text-muted-foreground">
            Väntar på det första resultatet från Valmyndigheten. Sidan uppdaterar sig själv –
            du behöver inte ladda om.
          </p>
        </section>
      )}

      {showResults && <ResultsTable data={data} />}

      {data.phase === "preliminaryPaused" && (
        <Alert>
          <Info className="size-4" />
          <AlertTitle>Preliminärt färdigräknat – men inte klart</AlertTitle>
          <AlertDescription>
            Alla valdistrikt är preliminärt räknade. På onsdagen räknas sent inkomna
            förtidsröster, och därefter gör länsstyrelserna den slutliga rösträkningen.
            Siffrorna kan alltså ändras.
          </AlertDescription>
        </Alert>
      )}

      {data.phase === "finalCounting" && (
        <Alert>
          <Info className="size-4" />
          <AlertTitle>Slutlig rösträkning pågår</AlertTitle>
          <AlertDescription>
            Länsstyrelserna kontrollräknar rösterna. Siffrorna kan justeras innan resultatet
            fastställs.
          </AlertDescription>
        </Alert>
      )}

      <section className="space-y-3 border-t pt-6 text-sm text-muted-foreground">
        <h2 className="font-semibold text-foreground">Källa och metod</h2>
        <p>
          Rösträkningen kommer från{" "}
          <a
            href="https://www.val.se/valresultat-och-statistik/statistik-och-data/radata-val-2026"
            className="underline underline-offset-4 hover:text-foreground"
            target="_blank"
            rel="noreferrer"
          >
            Valmyndigheten
          </a>
          . Vi hämtar deras publicerade resultatfiler, verifierar dem mot Valmyndighetens
          checksumma och digitala signatur, och visar siffrorna som de är.
        </p>
        <p>
          <strong className="text-foreground">Allt du ser här är faktiskt räknat resultat.</strong>{" "}
          Ingen prognos och inga uppskattningar ingår. Andelen räknade valdistrikt är inte
          detsamma som andelen räknade röster – täckningen ovan utgår därför från antalet
          röstberättigade i de distrikt som rapporterat.
        </p>
        <p>
          Mandaten är Valmyndighetens officiella preliminära fördelning. Vi räknar inte mandat
          själva.
        </p>
        <p>
          Vill du jämföra med opinionsläget före valet finns{" "}
          <Link href="/barometer" className="underline underline-offset-4 hover:text-foreground">
            Valbarometern
          </Link>
          . Opinionsmätningar är en historisk jämförelse – de påverkar ingenting på den här sidan.
        </p>
        {data.source && (
          <p className="text-xs">
            Datakälla uppdaterad{" "}
            <time dateTime={data.source.updatedAt}>
              {dateTimeFmt.format(new Date(data.source.updatedAt))}
            </time>
            , hämtad av oss{" "}
            <time dateTime={data.source.ingestedAt}>
              {dateTimeFmt.format(new Date(data.source.ingestedAt))}
            </time>
            . Sidan uppdaterades senast {dateTimeFmt.format(new Date(dataUpdatedAt))}.
          </p>
        )}
      </section>
    </div>
  );
}

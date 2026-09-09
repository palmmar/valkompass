"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { ElectionCountdown } from "@/components/election-countdown";
import { ElectionQueryProvider } from "@/components/valvaka/query-provider";
import { POLLS_CLOSE } from "@/lib/config";
import { fetchElectionLive, isCounting, type ElectionPhase } from "@/lib/election-api";
import { useTimeRemaining, unitLabel } from "@/lib/use-time-remaining";
import { ArrowRight } from "lucide-react";

/**
 * Startsidans valdagstillstånd. Ersätter den fasta nedräkningen med rätt läge för dagen:
 * nedräkning före valdagen, "vallokalerna stänger om …" på valdagen, och en ingång till
 * valvakan så fort rösträkningen börjar.
 *
 * Fasen kommer från backend (#85) så att datumreglerna bor på ett ställe. Går anropet inte
 * igenom faller vi tillbaka på nedräkningen, som är rätt svar fram till valdagen och aldrig
 * påstår något om rösträkningen.
 */
function ElectionDayCtaInner() {
  const { data } = useQuery({
    queryKey: ["election", "live"],
    queryFn: fetchElectionLive,
    refetchInterval: (query) => {
      const phase = query.state.data?.phase;
      return phase && isCounting(phase) ? 30_000 : 5 * 60_000;
    },
  });

  const phase: ElectionPhase = data?.phase ?? "preElection";

  if (phase === "electionDay") {
    return <PollsClosingSoon />;
  }

  if (phase === "preElection") {
    return <ElectionCountdown />;
  }

  const finished = phase === "finished";

  return (
    <div className="mt-10 rounded-lg border bg-card p-5">
      <p className="font-mono text-xs font-medium uppercase tracking-[0.16em] text-primary">
        Valvaka 2026
      </p>
      <p className="mt-2 max-w-xl text-balance text-muted-foreground">
        {finished
          ? "Riksdagsvalet är räknat. Se det slutliga resultatet och hur mandaten fördelades."
          : "Följ rösträkningen live – räknat resultat från Valmyndigheten, uppdaterat löpande under kvällen."}
      </p>
      <Button
        render={<Link href="/valvaka" />}
        nativeButton={false}
        className="mt-4"
      >
        {finished ? "Se valresultatet" : "Följ valvakan"}
        <ArrowRight className="size-4" />
      </Button>
    </div>
  );
}

/** Valdagen före 20:00: räkna ned till att vallokalerna stänger, inte till valvakan. */
function PollsClosingSoon() {
  const remaining = useTimeRemaining(POLLS_CLOSE);

  return (
    <div className="mt-10 border-t border-border pt-5">
      <p className="text-lg font-semibold tracking-tight">Valdagen är här</p>
      <p className="mt-1 text-muted-foreground">
        {remaining && remaining.total > 0 ? (
          <>
            Vallokalerna stänger om{" "}
            <span className="tabular-nums">
              {remaining.hours > 0 && (
                <>
                  {remaining.hours} {unitLabel(remaining.hours, "timme", "timmar")} och{" "}
                </>
              )}
              {remaining.minutes} {unitLabel(remaining.minutes, "minut", "minuter")}
            </span>
            .
          </>
        ) : (
          "Vallokalerna stänger klockan 20:00."
        )}
      </p>
    </div>
  );
}

export function ElectionDayCta() {
  return (
    <ElectionQueryProvider>
      <ElectionDayCtaInner />
    </ElectionQueryProvider>
  );
}

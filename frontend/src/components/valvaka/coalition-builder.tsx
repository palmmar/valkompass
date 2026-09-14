"use client";

import { useMemo, useState } from "react";
import type { ElectionLive } from "@/lib/election-api";
import { Button } from "@/components/ui/button";
import { partyColor, readableTextColor } from "./colors";
import { cn } from "@/lib/utils";

/**
 * Stapelytans höjd i pixlar. Ett fast tal (i stället för en CSS-höjd) så att varje segments
 * höjd går att räkna ut i förväg – texten i ett segment måste anpassas efter hur högt det är,
 * och ett parti på 16 mandat får inte ärva samma typsnittsgrad som ett på 109.
 */
const CHART_HEIGHT = 300;

/** Under den här höjden ryms varken sifferhörn eller stor partikod – då blir det en rad. */
const COMPACT_BELOW = 34;

/**
 * Grupperingar som ligger ett klick bort. De är uppräkningar av partier, inget annat: att de
 * står här är ingen bedömning av vilka som skulle eller borde regera ihop.
 */
const QUICK_PICKS = [
  ["S", "V", "MP", "C"],
  ["M", "KD", "L", "SD"],
];

interface PartySeats {
  code: string;
  name: string;
  color: string;
  mandates: number;
}

const pct = (n: number) =>
  n.toLocaleString("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/**
 * Låter läsaren lägga ihop partier till ett regeringsunderlag och se om det når majoritet.
 *
 * Räknar bara ihop Valmyndighetens officiella preliminära mandat – ingen egen mandatmotor,
 * ingen prognos och inget påstående om vilka partier som skulle kunna eller vilja samarbeta.
 * Allt utom vilka partier som ligger i vilken hög kommer från rösträkningen.
 */
export function CoalitionBuilder({ data }: { data: ElectionLive }) {
  const parties = useMemo<PartySeats[]>(() => {
    const resultByCode = new Map(data.results.map((r) => [r.partyCode, r]));
    return data.officialMandates
      // Partier under spärren har noll mandat och kan varken ritas eller flytta något.
      .filter((m) => m.mandates > 0)
      .map((m) => {
        const result = resultByCode.get(m.partyCode);
        return {
          code: m.partyCode,
          name: result?.name ?? m.partyCode,
          color: partyColor(result?.color),
          mandates: m.mandates,
        };
      })
      // Minst först = störst underst. Stapeln växer uppåt mot majoritetsgränsen, och det är
      // där uppe de små partierna avgör om underlaget räcker.
      .sort((a, b) => a.mandates - b.mandates || b.code.localeCompare(a.code, "sv"));
  }, [data.results, data.officialMandates]);

  const [government, setGovernment] = useState<ReadonlySet<string>>(new Set());

  const total = parties.reduce((sum, p) => sum + p.mandates, 0);
  const majority = Math.floor(total / 2) + 1;

  const inGovernment = parties.filter((p) => government.has(p.code));
  const inOpposition = parties.filter((p) => !government.has(p.code));
  const governmentSeats = inGovernment.reduce((sum, p) => sum + p.mandates, 0);
  const oppositionSeats = total - governmentSeats;

  // Den största högen fyller ytan; majoritetsstrecket hamnar då där det faktiskt hör hemma i
  // förhållande till staplarna. Gränsen tas med i maxet så att strecket alltid syns.
  const scale = Math.max(governmentSeats, oppositionSeats, majority);

  const move = (code: string) =>
    setGovernment((prev) => {
      const next = new Set(prev);
      if (!next.delete(code)) next.add(code);
      return next;
    });

  const available = new Set(parties.map((p) => p.code));
  const quickPicks = QUICK_PICKS.map((codes) => codes.filter((c) => available.has(c))).filter(
    (codes) => codes.length > 1,
  );

  const verdict = (() => {
    if (governmentSeats === 0) return "Välj ett eller flera partier för att räkna ihop ett underlag.";
    if (governmentSeats >= majority) {
      const margin = governmentSeats - majority;
      return margin === 0
        ? `Egen majoritet, med minsta möjliga marginal: ${governmentSeats} av ${total} mandat.`
        : `Egen majoritet: ${governmentSeats} av ${total} mandat, ${margin} mer än de ${majority} som krävs.`;
    }
    return `Saknar ${majority - governmentSeats} mandat för egen majoritet.`;
  })();

  return (
    <section className="space-y-4 rounded-lg border bg-card p-4 sm:p-5">
      <div className="space-y-1">
        <h2 className="text-lg font-semibold">Bygg ett regeringsunderlag</h2>
        <p className="text-sm text-muted-foreground">
          Klicka på ett parti för att flytta det mellan högarna. Siffrorna är Valmyndighetens
          officiella preliminära mandat – de ändras medan rösterna räknas.
        </p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {quickPicks.length > 0 && (
          <span className="font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
            Snabbval
          </span>
        )}
        {quickPicks.map((codes) => {
          // Markera snabbvalet när det är exakt den hög som ligger på bordet – annars går det
          // inte att se om man har ändrat något sedan man tryckte.
          const active = codes.length === government.size && codes.every((c) => government.has(c));
          return (
            <Button
              key={codes.join()}
              variant={active ? "secondary" : "outline"}
              size="sm"
              aria-pressed={active}
              className={cn(active && "border-primary/50")}
              onClick={() => setGovernment(new Set(codes))}
            >
              {codes.join(" · ")}
            </Button>
          );
        })}
        <Button
          variant="ghost"
          size="sm"
          className="ml-auto"
          disabled={governmentSeats === 0}
          onClick={() => setGovernment(new Set())}
        >
          Rensa
        </Button>
      </div>

      <div className="grid grid-cols-2 gap-2 sm:gap-3">
        <ColumnHeading
          label="Regering"
          seats={governmentSeats}
          total={total}
          highlight={governmentSeats >= majority}
        />
        <ColumnHeading label="Övriga" seats={oppositionSeats} total={total} />
      </div>

      <div className="relative">
        <div className="grid grid-cols-2 gap-2 sm:gap-3" style={{ height: CHART_HEIGHT }}>
          <Stack
            members={inGovernment}
            scale={scale}
            targetLabel="Övriga"
            empty="Klicka på ett parti till höger"
            onMove={move}
          />
          <Stack
            members={inOpposition}
            scale={scale}
            targetLabel="Regering"
            empty="Alla partier är valda"
            onMove={move}
          />
        </div>

        {/* Majoritetsgränsen ligger i samma koordinatsystem som båda staplarna, så det går att
            läsa av med ögat vilken hög som passerar den. */}
        <div
          className="pointer-events-none absolute inset-x-0 border-t border-dashed border-foreground/50"
          style={{ bottom: (majority / scale) * CHART_HEIGHT }}
          aria-hidden
        >
          <span className="absolute right-0 top-0 -translate-y-1/2 rounded-sm bg-foreground px-1.5 py-px font-mono text-[0.6rem] uppercase tracking-wider text-background">
            {majority} = majoritet
          </span>
        </div>
      </div>

      <p
        aria-live="polite"
        className={cn(
          "text-sm tabular-nums",
          governmentSeats >= majority ? "font-medium text-foreground" : "text-muted-foreground",
        )}
      >
        {verdict}
      </p>

      <p className="border-t pt-4 text-xs leading-relaxed text-muted-foreground">
        Det här är en räknesnurra, inte en förutsägelse. Den säger ingenting om vilka partier
        som vill eller kan regera ihop – bara vad mandaten blir om man lägger ihop dem.{" "}
        <strong className="text-foreground">
          En regering behöver inte egen majoritet för att tillträda.
        </strong>{" "}
        Riksdagen röstar om statsministern, och förslaget faller bara om minst {majority}{" "}
        ledamöter röstar nej – den som lägger ned sin röst stoppar alltså ingen. Ett underlag
        under strecket kan därför ändå räcka. Partier som ligger under riksdagsspärren har
        inga mandat och finns inte med här.
      </p>
    </section>
  );
}

function ColumnHeading({
  label,
  seats,
  total,
  highlight,
}: {
  label: string;
  seats: number;
  total: number;
  highlight?: boolean;
}) {
  return (
    <div className={cn("border-t-2 pt-2", highlight ? "border-primary" : "border-border")}>
      <p className="font-mono text-[0.65rem] uppercase tracking-[0.12em] text-muted-foreground">
        {label}
      </p>
      <p
        className={cn(
          "font-heading text-3xl font-bold leading-none tabular-nums sm:text-4xl",
          highlight && "text-primary",
        )}
      >
        {seats}
      </p>
      <p className="mt-0.5 text-xs tabular-nums text-muted-foreground">
        {total > 0 ? `${pct((seats / total) * 100)} % av mandaten` : "–"}
      </p>
    </div>
  );
}

function Stack({
  members,
  scale,
  targetLabel,
  empty,
  onMove,
}: {
  members: PartySeats[];
  scale: number;
  targetLabel: string;
  empty: string;
  onMove: (code: string) => void;
}) {
  return (
    <div className="flex h-full flex-col justify-end">
      {members.length === 0 ? (
        // Hela höjden, inte bara en remsa längst ned: en tom hög ska se ut som en plats att
        // fylla, inte som ett diagram som inte laddat klart. Texten hamnar där stapeln skulle
        // ha börjat växa – och därmed aldrig i vägen för majoritetsstrecket.
        <p className="flex h-full items-end justify-center rounded-md border border-dashed bg-muted/30 px-4 pb-4 text-center text-xs text-balance text-muted-foreground">
          {empty}
        </p>
      ) : (
        members.map((party) => (
          <Segment
            key={party.code}
            party={party}
            height={(party.mandates / scale) * CHART_HEIGHT}
            targetLabel={targetLabel}
            onMove={() => onMove(party.code)}
          />
        ))
      )}
    </div>
  );
}

function Segment({
  party,
  height,
  targetLabel,
  onMove,
}: {
  party: PartySeats;
  height: number;
  targetLabel: string;
  onMove: () => void;
}) {
  const compact = height < COMPACT_BELOW;
  return (
    <button
      type="button"
      onClick={onMove}
      title={`${party.name} · ${party.mandates} mandat`}
      aria-label={`${party.name}, ${party.mandates} mandat. Flytta till ${targetLabel}.`}
      // border-b i stället för gap: ramen ryms inom segmentets höjd (border-box), så
      // avgränsningen mellan partierna kostar inget av proportionerna.
      className="relative flex w-full shrink-0 items-center justify-center overflow-hidden border-b-2 border-card text-center transition-[filter] last:border-b-0 hover:brightness-110 focus-visible:z-10 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-current"
      style={{
        height,
        backgroundColor: party.color,
        color: readableTextColor(party.color),
      }}
    >
      {compact ? (
        // Ett litet parti i en hög med alla 349 mandaten får bara ett par pixlar. Graden
        // följer höjden så att texten ryms i stället för att klippas – siffran finns kvar i
        // aria-etiketten och verktygstipset.
        <span
          className="px-1 font-semibold leading-none tabular-nums"
          style={{ fontSize: Math.min(10.5, Math.max(7.5, height - 3.5)) }}
        >
          {party.code} {party.mandates}
        </span>
      ) : (
        <>
          <span className="absolute left-1 top-0.5 font-mono text-[0.6rem] leading-none tabular-nums opacity-80">
            {party.mandates}
          </span>
          <span
            className="font-bold leading-none"
            style={{ fontSize: Math.min(32, Math.max(15, height * 0.42)) }}
          >
            {party.code}
          </span>
        </>
      )}
    </button>
  );
}

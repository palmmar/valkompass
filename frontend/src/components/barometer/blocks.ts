// Blockläge: summerar partiers stöd till två grupperingar. Användaren placerar själv varje
// parti i Block 1, Block 2 eller utanför (default = S·V·MP·C mot M·KD·L·SD). Aggregeringen
// sker på klienten (en presentationsfråga) så summorna räknas om direkt vid omplacering.
// Ingen påtvingad blockindelning.

import type { BarometerElection, BarometerPoll } from "@/lib/barometer-api";

/** Vilket block ett parti tillhör, eller "none" = utanför. */
export type Group = "a" | "b" | "none";

export interface BlockDef {
  id: "a" | "b";
  /** Default-medlemmar (partikoder) – utgångspunkten innan användaren omplacerar. */
  defaultMembers: string[];
  color: string;
}

// Block 1 (rött) förblir rött och Block 2 (blått) blått oavsett vilka partier som ligger där.
export const DEFAULT_BLOCKS: BlockDef[] = [
  { id: "a", defaultMembers: ["S", "V", "MP", "C"], color: "#d64550" },
  { id: "b", defaultMembers: ["M", "KD", "L", "SD"], color: "#3b7dd8" },
];

/** Default-placering: varje default-medlem i sitt block, övriga "none". */
export function defaultAssignment(): Record<string, Group> {
  const a: Record<string, Group> = {};
  for (const block of DEFAULT_BLOCKS) for (const code of block.defaultMembers) a[code] = block.id;
  return a;
}

export interface BlockPoint {
  date: string;
  value: number;
  marginOfError: number | null;
  pollsterCode: string;
  sampleSize: number | null;
  sourceUrl: string | null;
}

const time = (s: string) => new Date(`${s}T00:00:00`).getTime();
const round1 = (n: number) => Math.round(n * 10) / 10;

/** 95%-felmarginal för blockets andel (enproportionsschablon ur n). */
function blockMoe(valuePct: number, n: number | null): number | null {
  if (n == null || n <= 0) return null;
  const p = valuePct / 100;
  if (p <= 0 || p >= 1) return null;
  return round1(1.96 * Math.sqrt((p * (1 - p)) / n) * 100);
}

/** En blockprick per mätning = summan av blockets medlemspartiers stöd. */
export function buildBlockDots(opinionPolls: BarometerPoll[], members: string[]): BlockPoint[] {
  if (members.length === 0) return [];
  const points: BlockPoint[] = [];
  for (const poll of opinionPolls) {
    const byCode = new Map(poll.results.map((r) => [r.partyCode, r.value]));
    let sum = 0;
    let any = false;
    for (const m of members) {
      const v = byCode.get(m);
      if (v != null) {
        sum += v;
        any = true;
      }
    }
    if (!any) continue;
    points.push({
      date: poll.publishedAt,
      value: round1(sum),
      marginOfError: blockMoe(sum, poll.sampleSize),
      pollsterCode: poll.pollsterCode,
      sampleSize: poll.sampleSize,
      sourceUrl: poll.sourceUrl,
    });
  }
  points.sort((a, b) => a.date.localeCompare(b.date));
  return points;
}

/** Glidande snitt (samma 30-dagarslogik som backend) för en blockserie. */
export function rollingAverage(
  points: BlockPoint[],
  windowDays = 30,
): { date: string; value: number; marginOfError: number | null }[] {
  const ms = windowDays * 864e5;
  const line: { date: string; value: number; marginOfError: number | null }[] = [];
  for (const pt of points) {
    const hi = time(pt.date);
    const lo = hi - ms;
    const win = points.filter((x) => {
      const t = time(x.date);
      return t > lo && t <= hi;
    });
    const value = win.reduce((s, x) => s + x.value, 0) / win.length;
    const moes = win.map((x) => x.marginOfError).filter((m): m is number => m != null);
    const moe = moes.length ? moes.reduce((s, m) => s + m, 0) / moes.length : null;
    line.push({
      date: pt.date,
      value: Math.round(value * 100) / 100,
      marginOfError: moe != null ? Math.round(moe * 100) / 100 : null,
    });
  }
  const byDate = new Map(line.map((p) => [p.date, p]));
  return [...byDate.values()].sort((a, b) => a.date.localeCompare(b.date));
}

/** Summerar ett blocks medlemmar i en uppsättning resultat (mätning eller valresultat). */
export function blockSum(
  results: { partyCode: string; value: number | null }[],
  members: string[],
): number {
  const byCode = new Map(results.map((r) => [r.partyCode, r.value]));
  let sum = 0;
  for (const m of members) {
    const v = byCode.get(m);
    if (v != null) sum += v;
  }
  return round1(sum);
}

/** Senaste riksdagsval på eller före ett givet datum (för förändring i procentenheter). */
export function latestElectionBefore(
  elections: BarometerElection[],
  beforeDate: string | null,
): BarometerElection | null {
  const past = elections
    .filter((e) => !beforeDate || e.date <= beforeDate)
    .sort((a, b) => a.date.localeCompare(b.date));
  return past.length ? past[past.length - 1] : null;
}

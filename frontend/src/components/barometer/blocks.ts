// Blockläge: summerar partiers stöd till två valbara grupperingar. Aggregeringen sker på
// klienten (en presentationsfråga) så att summorna räknas om direkt när man klickar ur ett
// parti. Grupperingen är INTE påtvingad – default nedan kan ändras genom partitoggels.

import type { BarometerElection, BarometerPoll } from "@/lib/barometer-api";

export interface BlockDef {
  id: string;
  /** Default-medlemmar (partikoder). Användaren kan klicka ur dem. */
  members: string[];
  color: string;
}

// Default: S·V·MP·C mot M·KD·L·SD (de två grupperingar maintainern efterfrågade).
export const DEFAULT_BLOCKS: BlockDef[] = [
  { id: "block-a", members: ["S", "V", "MP", "C"], color: "#d64550" },
  { id: "block-b", members: ["M", "KD", "L", "SD"], color: "#3b7dd8" },
];

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

/** Aktiva medlemmar = blockets default-partier som fortfarande är ikryssade. */
export function activeMembers(block: BlockDef, visible: Set<string>): string[] {
  return block.members.filter((m) => visible.has(m));
}

/** En blockprick per mätning = summan av de ikryssade medlemspartiernas stöd. */
export function buildBlockDots(
  opinionPolls: BarometerPoll[],
  members: string[],
  visible: Set<string>,
): BlockPoint[] {
  const active = members.filter((m) => visible.has(m));
  const points: BlockPoint[] = [];
  for (const poll of opinionPolls) {
    const byCode = new Map(poll.results.map((r) => [r.partyCode, r.value]));
    let sum = 0;
    let any = false;
    for (const m of active) {
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

/** Summerar ett blocks ikryssade medlemmar i en uppsättning resultat (mätning eller valresultat). */
export function blockSum(
  results: { partyCode: string; value: number | null }[],
  members: string[],
  visible: Set<string>,
): number {
  const active = members.filter((m) => visible.has(m));
  const byCode = new Map(results.map((r) => [r.partyCode, r.value]));
  let sum = 0;
  for (const m of active) {
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

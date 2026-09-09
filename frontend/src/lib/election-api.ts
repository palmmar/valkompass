// Datakontrakt för valvakan. Speglar GET /api/election/live (#85).
//
// Två saker är avsiktligt åtskilda hela vägen och får aldrig slås ihop i UI:t:
// `results` är faktiskt räknat resultat från Valmyndigheten, `forecast` är vår egen prognos.

/** Valvakans tillstånd. Backend äger regeln – frontend gör inga egna datumkontroller. */
export type ElectionPhase =
  | "preElection"
  | "electionDay"
  | "waitingForResults"
  | "live"
  | "preliminaryPaused"
  | "finalCounting"
  | "finished";

export interface ElectionSource {
  /** Valmyndighetens egen tidsstämpel för när siffrorna senast uppdaterades. */
  updatedAt: string;
  ingestedAt: string;
  /** Sant när källan inte uppdaterats på ett tag – visa siffrorna som fördröjda. */
  stale: boolean;
  /** Sant för genrepsdata. Får aldrig presenteras som verkligt valresultat. */
  isTest: boolean;
  stage: "preliminary" | "final";
}

export interface ElectionReporting {
  districtsReported: number;
  districtsTotal: number;
  eligibleVotersCovered: number;
  eligibleVotersTotal: number;
  /** Andel röstberättigade i räknade valdistrikt – inte andel av rösterna räknade. */
  coveragePercent: number | null;
  totalVotes: number;
  turnoutPercent: number | null;
  turnoutPercentPrevious: number | null;
}

export interface ElectionPartyResult {
  partyCode: string;
  name: string;
  displayOrder: number;
  votes: number;
  sharePercent: number;
  sharePreviousPercent: number | null;
  /** Förändring i procentenheter mot valet 2022. */
  shareChangePoints: number | null;
}

/** Valmyndighetens officiella preliminära mandat – inte vår egen mandaträkning. */
export interface ElectionMandate {
  partyCode: string;
  mandates: number;
  fixedMandates: number;
  levellingMandates: number;
  mandatesPrevious: number | null;
  change: number | null;
}

export interface ElectionThresholds {
  nationalPercent: number;
  constituencyPercent: number;
}

/** Ett partis prognos. Innehåller aldrig det räknade resultatet – det ligger i `results`. */
export interface ElectionForecastParty {
  partyCode: string;
  forecastShare: number;
  lower90: number;
  upper90: number;
  /** Sannolikhet att nå riksdagsspärren, 0–1. */
  probabilityAboveThreshold: number;
}

export interface ElectionForecast {
  parties: ElectionForecastParty[];
  /** Härlett ur intervallbredden, aldrig ur klockslag. */
  confidence: "low" | "medium" | "high" | "veryHigh";
  /** Halva bredden på ett typiskt 90 %-intervall, i procentenheter. */
  typicalUncertaintyPoints: number;
  coveragePercent: number;
  /** Hur geografiskt skevt underlaget är. 0 = de rapporterade distrikten speglar landet. */
  regionalSkewPercent: number;
  comparableDistrictsUsed: number;
  modelVersion: string;
  draws: number;
}

export interface ElectionLive {
  phase: ElectionPhase;
  source: ElectionSource | null;
  reporting: ElectionReporting | null;
  results: ElectionPartyResult[];
  officialMandates: ElectionMandate[];
  thresholds: ElectionThresholds;
  /** Prognos, eller null när ingen finns att visa. Alltid skild från `results`. */
  forecast: ElectionForecast | null;
}

/** Prognosläge i klartext. Beskriver modellens osäkerhet, inte hur långt kvällen gått. */
export const CONFIDENCE_LABEL: Record<ElectionForecast["confidence"], string> = {
  low: "Låg",
  medium: "Medel",
  high: "Hög",
  veryHigh: "Mycket hög",
};

export async function fetchElectionLive(): Promise<ElectionLive> {
  const res = await fetch("/api/election/live", { headers: { Accept: "application/json" } });
  if (!res.ok) throw new Error(`Valvaka-API svarade ${res.status}`);
  return (await res.json()) as ElectionLive;
}

/** Faser där rösträkningen pågår och det är värt att polla tätt. */
export function isCounting(phase: ElectionPhase): boolean {
  return phase === "waitingForResults" || phase === "live" || phase === "finalCounting";
}

/** Faser där det finns räknade siffror att visa. */
export function hasResults(phase: ElectionPhase): boolean {
  return phase === "live" || phase === "preliminaryPaused" || phase === "finalCounting" || phase === "finished";
}

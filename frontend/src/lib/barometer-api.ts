// Datakontrakt och hämtning för valbarometern. Helt fristående från quiz/resultat:
// ingen delad typ, ingen Zustand-store, ingen delningstoken. Publik, anonym read-only-data
// som konsumeras via TanStack Query i klientkomponenter.

export interface BarometerParty {
  code: string;
  name: string;
  color: string | null;
  displayOrder: number;
}

export interface BarometerPollResult {
  partyCode: string;
  /** Stöd i procent, eller null = under redovisningsgräns ("redovisas ej"), aldrig 0. */
  value: number | null;
  marginOfError: number | null;
}

export interface BarometerPoll {
  externalKey: string;
  pollsterCode: string;
  pollsterName: string;
  method: string | null;
  fieldStart: string | null;
  fieldEnd: string | null;
  publishedAt: string;
  sampleSize: number | null;
  sourceUrl: string | null;
  sourceCitation: string | null;
  results: BarometerPollResult[];
}

export interface BarometerPoint {
  date: string;
  value: number;
  marginOfError: number | null;
  pollsterCode: string;
  sampleSize: number | null;
  sourceUrl: string | null;
}

export interface BarometerSeries {
  partyCode: string;
  points: BarometerPoint[];
}

export interface BarometerAvgPoint {
  date: string;
  value: number;
  marginOfError: number | null;
}

export interface BarometerAvgSeries {
  partyCode: string;
  points: BarometerAvgPoint[];
}

export interface BarometerPartyValue {
  partyCode: string;
  value: number | null;
}

export interface BarometerElection {
  date: string;
  label: string;
  results: BarometerPartyValue[];
}

export interface BarometerTimeseries {
  parties: BarometerParty[];
  series: BarometerSeries[];
  monthlyAverage: BarometerAvgSeries[];
  rollingAverage: BarometerAvgSeries[];
  rollingWindowDays: number;
  elections: BarometerElection[];
  from: string | null;
  to: string | null;
  disclaimer: string;
}

export interface BarometerLatestResult {
  partyCode: string;
  value: number | null;
  marginOfError: number | null;
  /** Förändring i procentenheter mot senaste riksdagsval; null om underlag saknas. */
  electionDelta: number | null;
}

export interface BarometerLatest {
  parties: BarometerParty[];
  poll: BarometerPoll;
  electionYear: number | null;
  results: BarometerLatestResult[];
  disclaimer: string;
}

// Klientanrop går till relativ /api (proxas till backend via next.config-rewrites).
async function getJson<T>(path: string): Promise<T> {
  const res = await fetch(`/api/barometer${path}`, { headers: { Accept: "application/json" } });
  if (!res.ok) throw new Error(`Barometer-API svarade ${res.status}`);
  return (await res.json()) as T;
}

export function fetchBarometerTimeseries(): Promise<BarometerTimeseries> {
  return getJson<BarometerTimeseries>("/timeseries");
}

export function fetchBarometerLatest(): Promise<BarometerLatest> {
  return getJson<BarometerLatest>("/latest");
}

export function fetchBarometerPolls(params?: {
  pollster?: string;
  from?: string;
  to?: string;
}): Promise<BarometerPoll[]> {
  const qs = new URLSearchParams();
  if (params?.pollster) qs.set("pollster", params.pollster);
  if (params?.from) qs.set("from", params.from);
  if (params?.to) qs.set("to", params.to);
  const suffix = qs.toString() ? `?${qs}` : "";
  return getJson<BarometerPoll[]>(`/polls${suffix}`);
}

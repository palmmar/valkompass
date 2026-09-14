// Mätvärdena som Next-servern själv rapporterar. Backend har sin egen /metrics (se
// backend/src/Valkompass.Api/Program.cs) — den här filen handlar bara om sidtrafiken, som
// backend aldrig ser: startsidan och /om är statiska och gör inga API-anrop alls.
import { collectDefaultMetrics, Counter, Histogram, Registry } from "prom-client";

export const registry = new Registry();

// Nodeprocessens egna mätvärden (minne, CPU, event loop lag, GC). Gratis, och det som svarar
// på "är frontend pressad?" när valvakan har många samtidiga läsare.
collectDefaultMetrics({ register: registry, prefix: "valkompass_frontend_" });

export const pageViews = new Counter({
  name: "valkompass_page_views_total",
  help: "Sidvisningar, exklusive prefetch, statiska filer och hälsokontroller.",
  labelNames: ["route", "kind"],
  registers: [registry],
});

export const requestDuration = new Histogram({
  name: "valkompass_frontend_request_duration_seconds",
  help: "Svarstid för sidförfrågningar mot Next-servern.",
  labelNames: ["route", "status"],
  // Sidrendering, inte API-anrop: intressant intervall är tiotals millisekunder till någon
  // sekund. Allt över ett par sekunder är ändå "för långsamt" och behöver ingen upplösning.
  buckets: [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2, 5],
  registers: [registry],
});

/**
 * Sökvägar som aldrig ska räknas: de säger inget om besök och skulle dränka graferna.
 */
const IGNORED = [
  /^\/_next\//,
  /^\/__nextjs/,
  /^\/health$/,
  /^\/metrics$/,
  /^\/api\//, // bara i dev: i drift går /api direkt till backend via ingressen
  /\.[a-z0-9]+$/i, // ikoner, bilder, robots.txt, manifest …
];

/**
 * Sökväg → rutt-label. Listan finns för att hålla kardinaliteten nere: utan den blir varje
 * delad resultatlänk en egen tidsserie i Prometheus. Nya sidor läggs till här; tills dess
 * hamnar de under "other", vilket syns i grafen som en stigande okänd-post.
 */
const ROUTES: [RegExp, string][] = [
  [/^\/$/, "/"],
  [/^\/quiz$/, "/quiz"],
  [/^\/resultat\/[^/]+$/, "/resultat/[token]"],
  [/^\/valvaka$/, "/valvaka"],
  [/^\/barometer$/, "/barometer"],
  [/^\/om$/, "/om"],
  // Admin är ett fåtal interna sidor; de slås ihop till en label i stället för sex.
  [/^\/admin(\/|$)/, "/admin"],
];

/** Rutt-label för en sökväg, eller null när förfrågan inte ska räknas. */
export function routeLabel(pathname: string): string | null {
  if (IGNORED.some((pattern) => pattern.test(pathname))) return null;

  // Utan avslutande snedstreck, så att /om och /om/ blir samma serie.
  const path = pathname.length > 1 ? pathname.replace(/\/+$/, "") || "/" : pathname;
  for (const [pattern, label] of ROUTES) {
    if (pattern.test(path)) return label;
  }
  return "other";
}

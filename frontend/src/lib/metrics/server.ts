// Räknar sidtrafiken och serverar den på en egen port.
//
// Varför en egen port och inte en /metrics-rutt i appen: ingressen skickar allt som inte är
// /api till Next, så en rutt hade varit publik på valkompass.se. Porten här routas inte av
// ingressen och är därmed bara nåbar inifrån klustret, där Prometheus skrapar poddens IP.
//
// Varför diagnostics_channel och inte proxy.ts: Next dokumenterar uttryckligen att proxy körs
// frikopplat från renderingskoden och att man inte ska förlita sig på delade moduler eller
// globaler mellan dem. Nodes egna kanaler ligger under HTTP-servern i stället och ser varje
// förfrågan utan att appen behöver veta om det.
import dc from "node:diagnostics_channel";
import http from "node:http";
import type { IncomingMessage, Server, ServerResponse } from "node:http";

import { pageViews, registry, requestDuration, routeLabel } from "./registry";

/** Starttiden hängs på request-objektet; Symbol så att den inte krockar med något i Next. */
const START = Symbol("valkompass.metrics.start");

type TimedRequest = IncomingMessage & { [START]?: number };

interface HttpChannelMessage {
  request: TimedRequest;
  response: ServerResponse;
  server: Server;
}

let metricsServer: Server | undefined;

/** Så att ett systematiskt fel i mätningen inte spammar loggen med en rad per förfrågan. */
let recordFailureLogged = false;

/**
 * Startar mätvärdesservern och börjar räkna. Anropas en gång från instrumentation.ts.
 * Sätt METRICS_PORT=0 för att stänga av alltihop.
 */
export function startMetrics(): void {
  if (metricsServer) return; // register() kan köras om vid hot reload i dev

  const port = Number(process.env.METRICS_PORT ?? 9464);
  if (!Number.isInteger(port) || port <= 0) return;

  metricsServer = http.createServer(serveMetrics);

  // En upptagen port får aldrig fälla sidan. I dev händer det när två Next-servrar körs
  // samtidigt; i drift är det ett konfigurationsfel som ska synas i loggen, inte i en krasch.
  metricsServer.on("error", (error) => {
    console.warn(`[metrics] kunde inte lyssna på port ${port}:`, error);
  });
  metricsServer.listen(port, "0.0.0.0");

  // Håll inte processen vid liv för mätvärdenas skull – Next-servern äger livslängden.
  metricsServer.unref();

  dc.subscribe("http.server.request.start", (message) => {
    const { request } = message as HttpChannelMessage;
    request[START] = performance.now();
  });

  dc.subscribe("http.server.response.finish", (message) => {
    // Mätvärden får aldrig fälla sidan: en kastad prenumerant bubblar upp i Nodes
    // HTTP-server, inte i vår kod, och skulle ta ned hela processen.
    try {
      record(message as HttpChannelMessage);
    } catch (error) {
      if (!recordFailureLogged) {
        recordFailureLogged = true;
        console.warn("[metrics] kunde inte räkna en förfrågan:", error);
      }
    }
  });
}

async function serveMetrics(req: IncomingMessage, res: ServerResponse): Promise<void> {
  if (req.url?.split("?")[0] !== "/metrics") {
    res.writeHead(404).end();
    return;
  }

  try {
    const body = await registry.metrics();
    res.writeHead(200, { "Content-Type": registry.contentType }).end(body);
  } catch (error) {
    console.warn("[metrics] kunde inte samla in mätvärden:", error);
    res.writeHead(500).end();
  }
}

function record({ request, response, server }: HttpChannelMessage): void {
  // Skrapningar av /metrics ska inte mätas som trafik på sajten.
  if (server === metricsServer) return;

  const route = routeLabel(new URL(request.url ?? "/", "http://valkompass").pathname);
  if (route === null) return;

  const started = request[START];
  if (started !== undefined) {
    requestDuration.observe(
      { route, status: String(response.statusCode) },
      (performance.now() - started) / 1000,
    );
  }

  // Prefetch är Next som hämtar sidor i förväg när en länk syns eller hovras – inget besök.
  // Räknas de med blir siffran flera gånger för hög.
  if (request.headers["next-router-prefetch"] !== undefined) return;
  if (request.method !== "GET" || response.statusCode >= 400) return;

  // rsc-headern skiljer klientnavigering (bara sidans data hämtas) från en full sidladdning.
  // Båda är riktiga sidvisningar; separationen gör att man kan se hur folk rör sig i appen.
  const kind = request.headers.rsc !== undefined ? "navigation" : "document";
  pageViews.inc({ route, kind });
}

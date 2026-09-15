# Valkompass 2026

[![CI](https://github.com/palmmar/valkompass/actions/workflows/ci.yml/badge.svg)](https://github.com/palmmar/valkompass/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

En valkompass för det svenska riksdagsvalet 2026. Användaren svarar anonymt på ~50
påståenden och får se hur väl ställningstagandena stämmer överens med de åtta
riksdagspartierna (S, M, SD, C, V, KD, L, MP) — totalt, per politikområde och fråga för fråga.

> ⚠️ **Obundet verktyg.** Valkompassen är inte knuten till något parti. Partiernas
> positioner bygger på primärkällor (partiprogram, valmanifest, voteringar, officiella
> uttalanden) och källhänvisas. Hittar du ett fel? Hör av dig.

## Teknik

| Lager      | Stack |
|------------|-------|
| Backend    | ASP.NET Core Web API (.NET 10), Minimal APIs, EF Core 10 + Npgsql |
| Databas    | PostgreSQL 17 |
| Frontend   | Next.js 16 (App Router) + React 19 + TypeScript + shadcn/ui (Tailwind v4) |
| Auth (admin) | ASP.NET Core Identity (cookie) med rollerna Admin/Editor |

## Struktur

```
backend/    .NET-solution (Domain, Application, Infrastructure, Api + tester)
frontend/   Next.js-app
docker-compose.yml   Postgres + Adminer för lokal utveckling
```

Matchningslogiken ligger i `backend/src/Valkompass.Application/Matching` som ett rent,
databasfritt och enhetstestbart bibliotek.

## Komma igång (lokalt)

Förutsättningar: .NET 10 SDK, Node 20+, Docker.

```bash
# 1. Kopiera miljövariabler
cp .env.example .env

# 2. Starta databasen
docker compose up -d

# 3. Backend (kör migrations + starta API:t på http://localhost:5208)
cd backend
dotnet ef database update --project src/Valkompass.Infrastructure --startup-project src/Valkompass.Api
dotnet run --project src/Valkompass.Api
# OpenAPI/Scalar: http://localhost:5208/scalar/v1

# 4. Frontend (i ett nytt terminalfönster) → http://localhost:3000
cd frontend
npm run dev
```

## Test

```bash
cd backend
dotnet test                       # enhets- + integrationstester
```

## Drift

Appen körs på k3s och synkas av Argo CD. Manifesten (API, frontend, PostgreSQL, Ingress) bor i
[palmmar/valkompass-gitops](https://github.com/palmmar/valkompass-gitops); `Application`-resursen
som pekar dit ligger i [palmmar/k3s-gitops](https://github.com/palmmar/k3s-gitops).

```text
main  ──CI──>  ghcr.io/palmmar/valkompass-{api,frontend}:<sha>
      └──promote──>  valkompass-gitops (k8s/overlays/prod)  ──Argo CD──>  k3s
```

Varje push till `main` bygger båda imagerna, taggar dem med commit-SHA:t och skriver in taggen i
prod-overlayn. Frontend-imagen byggs utan inbakad API-domän — klienten anropar relativa
`/api`-URL:er och ingressen delar trafiken mellan `valkompass-frontend` och `valkompass-api` —
så samma tagg fungerar i alla miljöer. Secrets, hälsokontroller och driftsdetaljer beskrivs i
[gitops-repots k8s/README.md](https://github.com/palmmar/valkompass-gitops/blob/main/k8s/README.md).

## Mätvärden (Prometheus/Grafana)

Båda tjänsterna exponerar mätvärden i Prometheus-format. Instrumenteringen är
`System.Diagnostics.Metrics` i backend (samma API som ASP.NET Core mäter sig själv med) och
`prom-client` i Next-servern; OpenTelemetry är bara exportör och går att byta ut utan att
mätkoden ändras.

| Tjänst   | Skrapas på | Publik? |
|----------|-----------|---------|
| API      | `:8080/metrics` | Nej – ingressen routar bara `/api/*` hit |
| Frontend | `:9464/metrics` | Nej – egen port som ingressen inte routar (`METRICS_PORT`, 0 = av) |

### Vad som mäts

| Mätvärde | Typ | Svarar på |
|----------|-----|-----------|
| `valkompass_page_views_total{route,kind}` | counter | **Antal besök** per sida. `kind`: `document` = sidladdning, `navigation` = klientnavigering. Prefetch, statiska filer och `/health` räknas inte. |
| `valkompass_frontend_request_duration_seconds{route,status}` | histogram | Svarstider för sidrendering |
| `valkompass_frontend_nodejs_*`, `..._process_*` | diverse | Minne, CPU och event loop-lag i Next-servern |
| `valkompass_quiz_started_total{mode,variant}` | counter | Påbörjade kompasser |
| `valkompass_quiz_completed_total{mode,variant}` | counter | **Utförda kompasser** |
| `valkompass_quiz_sessions_stored` | gauge | Totalt antal sparade resultat – läses ur databasen och överlever en omstart |
| `valkompass_election_snapshot_ingested_at_seconds` | gauge | **När valvakan senast fick ny data** (unixtid) |
| `valkompass_election_snapshot_source_updated_at_seconds` | gauge | Valmyndighetens egen tidsstämpel på siffrorna |
| `valkompass_election_districts_reported` / `_expected` | gauge | Rösträkningens framsteg |
| `valkompass_election_import_runs_total{outcome}` | counter | Importvarv per utfall: `imported`, `unchanged`, `no_results_published`, `rejected_test_data`, `error` |
| `valkompass_election_import_duration_seconds` | histogram | Tid per lyckat importvarv |
| `http_server_request_duration_seconds{http_route,http_response_status_code,…}` | histogram | API-trafik, latens och fel per endpoint |

Räknarna nollställs vid omstart – normalt för Prometheus, som hanterar det i `rate()` och
`increase()`. Värden som måste överleva omstart läses ur databasen var 30:e sekund av
`MetricsRefreshBackgroundService` (`Metrics__RefreshInterval` ändrar takten). Saknas data
rapporteras ingen tidsserie alls i stället för en nolla: en valvaka som inte börjat ska ge
en tom graf, inte "uppdaterad 1970".

### Exempelfrågor

```promql
# Besök senaste dygnet, per sida
sum by (route) (increase(valkompass_page_views_total[24h]))

# Utförda kompasser senaste dygnet, och totalt sedan start
sum(increase(valkompass_quiz_completed_total[24h]))
valkompass_quiz_sessions_stored

# Fullföljandegrad (påbörjade → klara)
sum(increase(valkompass_quiz_completed_total[24h]))
  / sum(increase(valkompass_quiz_started_total[24h]))

# Minuter sedan valvakan uppdaterades – larma när den står still under valkvällen
(time() - valkompass_election_snapshot_ingested_at_seconds) / 60

# Andel räknade valdistrikt
valkompass_election_districts_reported / valkompass_election_districts_expected

# API-fel, utan hälsokontrollerna
sum(rate(http_server_request_duration_seconds_count{
  http_response_status_code=~"5..", http_route!="/health"}[5m]))
```

### Skrapning i klustret

Manifesten bor i [palmmar/valkompass-gitops](https://github.com/palmmar/valkompass-gitops) och
behöver två tillägg: en namngiven port `metrics` (9464) på frontendens `Service`, och mål för
Prometheus. Med kube-prometheus-stack räcker en `ServiceMonitor` per tjänst:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: valkompass
spec:
  selector:
    matchLabels: { app.kubernetes.io/part-of: valkompass }
  endpoints:
    - port: http     # API:t – /metrics ligger på samma port som API:t
      path: /metrics
    - port: metrics  # frontendens egen port 9464
      path: /metrics
```

Kör klustret i stället Prometheus med annotationsbaserad upptäckt sätts
`prometheus.io/scrape: "true"`, `prometheus.io/port` och `prometheus.io/path: /metrics` på
poddarna. Ingressen ska inte routa `/metrics` – görs regeln för `/api` någon gång bredare måste
`/metrics` blockeras där.

## Matchningsmodell (kort)

4-gradig skala utan neutralt mitten (1–4), med "hoppa över" och "extra viktig"
(dubbel vikt). Per fråga och parti: `agreement = 1 − avstånd/3`. Total och per kategori
är ett viktat medelvärde över jämförbara frågor. Detaljer i implementeringsplanen.

## Innehåll och metodik

De ~50 frågorna är fördelade efter de samhällsfrågor väljarna anser är viktigast, baserat
på opinionsmätningar (t.ex. [Novus – viktigaste politiska frågan 2025](https://novus.se/valjarforstaelse-arkiv/2025-08-viktigaste-politiska-fragan/)
och [SOM-institutet/Lunds universitet](https://www.lu.se/artikel/energi-samt-lag-och-ordning-viktiga-valfragor)):
sjukvård, lag och ordning, migration, klimat/energi, skola och ekonomi väger tyngst, följt
av arbetsmarknad, försvar, äldreomsorg, bostäder och EU.

> ℹ️ **Partipositionerna är källsatta.** De 400 positionerna (50 påståenden × 8 partier) är
> belagda mot verkliga källor – partiprogram/"Vår politik A–Ö", riksdagsvoteringar, etablerade
> valkompasser och trovärdig nyhetsrapportering – med full proveniens (ordagrant belägg, URL och
> konfidens) i `backend/tools/sourcing/sources.json`. Konfidensen fördelar sig **200 high / 147
> medium / 53 low**; de 53 low-confidence-cellerna listas i `backend/tools/sourcing/NEEDS_REVIEW.md`
> och bör dubbelkollas i admingränssnittet före publicering.

Innehållet seedas idempotent från `backend/src/Valkompass.Infrastructure/Seed/Content/*.json`.
Generatorn `backend/tools/gen_content.py` skapar `questions.json` och bygger `positions.json` från
den källsatta `backend/tools/sourcing/sources.json`.

## Licens

Öppen källkod under [MIT-licensen](LICENSE) – fri att använda, kopiera, ändra, sprida och
använda kommersiellt. Bidrag är välkomna.

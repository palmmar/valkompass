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

> ⚠️ **Partipositionerna är ett UTKAST.** De är ett första utkast baserat på partiernas
> etablerade hållningar och är markerade `Utkast – verifieras i admin`. Innan publicering
> ska varje position granskas, justeras och källsättas mot primärkällor (partiprogram,
> valmanifest, voteringar) via admingränssnittet.

Innehållet seedas idempotent från `backend/src/Valkompass.Infrastructure/Seed/Content/*.json`.
Generatorn `backend/tools/gen_content.py` skapar `questions.json` och `positions.json`.

## Licens

Öppen källkod under [MIT-licensen](LICENSE) – fri att använda, kopiera, ändra, sprida och
använda kommersiellt. Bidrag är välkomna.

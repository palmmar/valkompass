# Bidra till Valkompass 2026

Tack för att du vill bidra! Projektet är öppen källkod under [MIT-licensen](LICENSE) och
bidrag av alla slag är välkomna – kod, innehåll, buggrapporter och förbättringsförslag.

## Komma igång

Se [README](README.md) för hur du kör projektet lokalt (Docker + .NET 10 + Node).

## Innan du skickar en pull request

Kör testerna och bygget lokalt – samma som CI:n gör:

```bash
# Backend
cd backend && dotnet test

# Frontend
cd frontend && npm run build
```

- Följ den befintliga kodstilen (se `.editorconfig`). Skriv kod som liknar koden runtomkring.
- Håll PR:er fokuserade – en sak per PR.
- Beskriv *vad* och *varför* i PR-beskrivningen.

## Bidra med innehåll (frågor och partipositioner)

Innehållet lever i `backend/src/Valkompass.Infrastructure/Seed/Content/*.json` och kan även
redigeras via admingränssnittet. **Partipositionerna är källsatta** mot verkliga källor (full
proveniens i `backend/tools/sourcing/sources.json`); de 53 low-confidence-cellerna i
`backend/tools/sourcing/NEEDS_REVIEW.md` bör dubbelkollas i admin före publicering.

Om du föreslår eller rättar en partiposition:

- Ange en **primärkälla** (partiprogram, valmanifest, riksdagsvotering eller officiellt
  uttalande) – gärna med länk.
- Sträva efter **neutralitet** och samma källkvalitet för alla partier.
- Formulera påståenden neutralt, en sakfråga i taget.

Är du osäker? Öppna en issue med mallen **Felaktig partiposition** så diskuterar vi först.

## Uppförandekod

Var respektfull och saklig. Det här är ett partipolitiskt obundet verktyg – håll diskussioner
om innehåll faktabaserade och källbelagda.

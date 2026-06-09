# GAL-TAN + ekonomisk vänster–höger (tvådimensionell positionering)

En first pass för att placera de 8 riksdagspartierna **och användaren** på två axlar:

- **Ekonomisk vänster–höger** (skatt, vinst i välfärden, LAS, bidrag, hyror …)
- **GAL–TAN** – grön/alternativ/frihetlig ↔ traditionell/auktoritär/nationalistisk
  (straff, övervakning, migration, klimat/grön styrning, bistånd)

Axlarna är ungefär ortogonala, så avsikten är en **2D-karta** (en punkt per parti + en
för användaren), inte en ensam GAL-TAN-linje.

## Metod – "samma linjal"

1. **`axes.json`** taggar varje av de 50 påståendena med en riktningsvikt per axel
   (`-1` / `0` / `+1`). Tecknet pekar mot axelns höga ände (höger resp. TAN);
   `0` = laddar inte axeln (avsiktligt utesluten). Detta är ett dokumenterat modellval.
2. **Partikoordinat** = medelvärdet av `vikt · centrerat svar` över de taggade frågor
   där partiet har en känd position. Svar `1..4` centreras till `-1.5..+1.5` (samma
   mappning som `MatchCalculator`). Råvärdet `[-1.5, +1.5]` skalas om till `0..10`.
3. **Användarkoordinat** beräknas med exakt samma formel → ligger på samma karta.

Partikoordinaterna kommer alltså helt ur era egna källsatta positioner
(`positions.json`) – **CHES används aldrig som indata.**

## Oberoende validering – Chapel Hill Expert Survey

`ches_reference.json` innehåller CHES partiskattningar för Sverige (2024 primär, 2019
alternativ) – en oberoende akademisk mätning (`lrecon`, `galtan`, skala 0–10). Skriptet
jämför den data-härledda axeln mot CHES i efterhand:

| Axel | Spearman (rang) | Pearson | mot CHES |
|------|----------------|---------|----------|
| Ekonomi | **+0.96** | +0.99 | 2024 |
| GAL-TAN | **+0.95** | +0.94 | 2024 |

Hög rangkorrelation = vår axel mäter samma konstrukt som forskningen, inte en magkänsla.
En enkel OLS-kalibrering (lutning + intercept från de 8 partierna) gör att även
användarens värde kan uttryckas på CHES-skalan (`chesCalibrated`).

## Körning

```bash
cd backend/tools/galtan
python3 compute_axes.py                 # partitabell + validering, skriver axes_output.json
python3 compute_axes.py --wave 2019     # validera mot CHES 2019
python3 compute_axes.py svar.json       # placera även en användare; svar.json = {"questionKey": 1..4}
```

Endast Python-stdlib (Spearman/Pearson/OLS implementerade för hand). `axes_output.json`
är artefakten en framtida app-integration kan läsa.

## Förbehåll (läs innan publik användning)

- **Axeltaggningen är ett modellval.** Granska `axes.json`; särskilt de markerade
  korsladdande frågorna (drivmedelsskatt, kärnkraft, bistånd, religiösa friskolor, EU).
- **GAL-TAN-axeln är lag-och-ordning/migrations-tung.** Hela högerblocket (M/KD/L)
  hamnar därför något mer TAN än i CHES. **L är den tydligaste avvikaren**
  (data ≈ TAN, CHES ≈ mitten) – dels för att axeln väger straff/migration tungt, dels
  för att era 2026-källor fångar L:s Tidö-förflyttning som CHES 2024 (fältarbete okt–nov
  2024) inte gör. Vill man dämpa det kan man vikta ned några lag/migrationsfrågor eller
  lägga till en social-/livsstilsdimension.
- **Ingen salience-viktning.** Ett parti kan ha en åsikt utan att frågan är central för
  det; CHES väger in detta, vi inte.
- **`forsvar-*` och `eu-*` är uteslutna** – de är egna dimensioner och korsladdar
  (vänster- och högereuroskepsis) snarare än att mäta ekonomi eller GAL-TAN.

## Källor

- CHES-Europe (2024): https://www.chesdata.eu/ches-europe
- CHES 2019: https://www.chesdata.eu/2019-chapel-hill-expert-survey
- Valforskningsprogrammet, GU: https://www.gu.se/en/swedish-national-election-studies

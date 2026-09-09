# Referensdata om valdistrikt (Valmyndigheten)

Underlag för valvakan (#81, #83). **Hämtat 2026-09-09** från
<https://www.val.se/valresultat-och-statistik/statistik-och-data/radata-val-2026>.

Filerna ligger i repot av samma skäl som `../sourcing/`: valnatten ska inte vara beroende av att
externa Excel-filer fortfarande går att hämta. De används vid import/build – aldrig i drift.

| Fil | Innehåll |
|---|---|
| `antal-rostberattigade-per-valdistrikt-och-valtyp-14-augusti-2026.xlsx` | Röstberättigade per valdistrikt per 14 augusti 2026 (kvalifikationsdagen) |
| `valdistrikt-jamforelser-mellan-2022-och-2026.xlsx` | Mappning valdistrikt 2022 ↔ 2026 med jämförbarhetsstatus |

## Behövs de här?

Antagligen inte – och det bör kollas innan någon bygger en importer för dem.

Valmyndighetens **live-resultatfil innehåller redan** både 2022-jämförelsen per valdistrikt och
per kommun, jämförbarhetsstatus, mappningen till 2022 års distriktskoder och antal
röstberättigade. Se `../../tests/fixtures/valmyndigheten/README.md`.

Filerna här behövs bara om en *delvis räknad* resultatfil visar sig utelämna valdistrikt som ännu
inte rapporterat – då saknas nämnaren (röstberättigade i orapporterade distrikt) och den måste
komma härifrån. De är alltså **försäkring**, inte en planerad pipeline.

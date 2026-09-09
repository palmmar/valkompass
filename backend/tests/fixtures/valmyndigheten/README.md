# Arkiverad valdata från Valmyndigheten

Fixtures för valvakan (#81). Materialet är **hämtat och verifierat 2026-09-09** och ligger i
repot för att det inte går att hämta igen: Valmyndighetens publika genrep pågick
**17 augusti – 2 september 2026** och är avslutat. Filerna låg kvar på `resultat.val.se` när de
arkiverades, men det finns ingen garanti för att de gör det när valvakan ska byggas eller testas.

Alla filer är **testdata** (`"test": true`). De får aldrig visas som skarpt valresultat.

## `genrep2026/`

| Fil | Källa |
|---|---|
| `index.md5` | `https://resultat.val.se/resultatfiler/genrep2026/index.md5` |
| `Genrep_2026_preliminar_00_RD.zip` | `https://resultat.val.se/resultatfiler/genrep2026/p/rd/Genrep_2026_preliminar_00_RD.zip` |

Nedladdad checksumma stämmer mot `index.md5` (`3e6407d8abce4a7856a396cd61e02724`).

Motsvarande produktionsindex är `https://resultat.val.se/resultatfiler/val2026/index.md5`. Det
svarar redan 200 men är **tomt** fram till valnatten (`d41d8cd98f00b204e9800998ecf8427e` = md5 av
tom indata) – importern måste tolka det som "inga resultat ännu", inte som fel.

### Innehåll i ZIP:en

```
Genrep_2026_preliminar_rostfordelning_00_RD.json    40 MB   per valdistrikt
Genrep_2026_preliminar_summering_RD.json             2 MB   per kommun
Genrep_2026_preliminar_mandatfordelning_00_RD.json 305 kB   officiell mandatfördelning
+ en *_sign.sha256 per JSON-fil
```

Snapshoten är färdigräknad: 6626 av 6626 valdistrikt, `antalUppdateringar: 353`.

### Det viktigaste om formatet

Resultatfilen innehåller **redan jämförelsen mot 2022**, per valdistrikt och per parti – vi
behöver alltså inte bygga den baselinen själva:

```jsonc
{
  "valdistriktskod": "01800101",
  "kommunkod": "0180", "lankod": "01", "kretskod": "01",
  "antalRostberattigade": 1264,
  "rapporteringsTid": "2026-08-31T14:17:07",
  "statusJamforelse": "Kan jämföras",
  "valdistriktskodForegaendeVal": ["01800101"],
  "valdeltagandeForegaendeVal": 79.4,
  "rostfordelning": { "rosterPaverkaMandat": { "partiRoster": [
    { "partiforkortning": "M", "andelRoster": 19.5,
      "andelRosterForegaendeVal": 19, "forandringAndelRoster": 0.5 }
  ]}}
}
```

`statusJamforelse` i genrepet: **5024 "Kan jämföras"**, **1567 "Ej jämförbart"**,
**35 "Jämförs mot summerat"**.

Att räkna med:

- **314 uppsamlingsdistrikt** (av 6626) har `antalRostberattigade: null` och är alltid
  "Ej jämförbart". Det är förtids- och brevröster med systematiskt annan partisammansättning –
  de kan inte behandlas som vanliga valdistrikt i en prognos.
- `summering`-filen har samma 2022-jämförelse per kommun, plus `antalRostberattigade` och
  `antalValdistriktSomSkaRaknas` – dvs. underlag även för distrikt som ännu inte rapporterat.
- `mandatfordelning` ger både officiell preliminär mandatfördelning och
  `antalRostberattigade` vs `antalRostberattigadeIRaknadeValdistrikt` för riket, så
  täckningsgraden räknat på röstberättigade behöver inte beräknas av oss.
- Rå `rostfordelning` är 40 MB. Den bör strömmas/parsas till en normaliserad snapshot, inte
  hållas i minnet som JSON-dokument.

**Obekräftat:** om en *delvis* räknad fil listar valdistrikt som ännu inte rapporterat. Den här
snapshoten är färdigräknad, så det går inte att avgöra härifrån. Svaret avgör om vi behöver
`backend/tools/valdata/` som fallback för nämnaren. Se #83.

### Inte arkiverat

`Genrep_2026_slutlig_00_RD.zip` (slutlig räkning) är **23,8 MB zippad / 260 MB uppackad** och
utelämnad för att inte tynga repot. Hämta vid behov från
`https://resultat.val.se/resultatfiler/genrep2026/s/rd/Genrep_2026_slutlig_00_RD.zip`
(md5 `f4d220f18d61c1aded3b7ac4aac50949` per 2026-09-09).

## Källor

- Teknisk beskrivning: <https://www.val.se/valresultat-och-statistik/statistik-och-data/teknisk-beskrivning-av-resultatfiler>
- Rådata 2026: <https://www.val.se/valresultat-och-statistik/statistik-och-data/radata-val-2026>

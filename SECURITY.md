# Säkerhetspolicy

## Rapportera en sårbarhet

Rapportera säkerhetsproblem **privat** – inte som en publik issue.

- Helst via GitHubs privata rapportering: fliken **Security → Report a vulnerability**
  (Privacy vulnerability reporting).
- Eller via e-post till **markus@palmenas.se**.

Beskriv gärna hur sårbarheten kan återskapas och vilken påverkan den har. Vi bekräftar
mottagandet så snart vi kan, håller dig informerad om åtgärder och krediterar dig gärna när
problemet är åtgärdat (om du vill). Tack för att du rapporterar ansvarsfullt.

## Versioner som stöds

Projektet är under aktiv utveckling. Säkerhetsfixar görs mot den senaste koden på `main`.

## Omfattning

Observera att detta är ett verktyg under utveckling där driftshärdning (HTTPS-cookies,
CSRF-skydd, hemlighetshantering) ännu inte är på plats för produktion – se README. Standard-
inloggningsuppgifterna i `appsettings.Development.json` är endast avsedda för lokal utveckling
och måste bytas före driftsättning.

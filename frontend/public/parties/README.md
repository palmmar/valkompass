# Partilogotyper

Logotyper hanteras numera i **admin-panelen** (Partier → Redigera → Logotyp), inte som
statiska filer här. De lagras som binärdata i databasen och serveras av backend på
`GET /api/parties/<code>/logo`. `<PartyLogo>` (`frontend/src/components/party-logo.tsx`)
hämtar därifrån och faller tillbaka på en färgad cirkel med partikoden om ingen logotyp
finns.

## Format

- **PNG eller WebP**, max 512 kB. Helst transparent bakgrund; logotypen centreras med
  `object-contain` och visas i storlekar mellan 20 och 56 px.
- SVG stöds avsiktligt inte (kan bära skript/XSS när det serveras från samma origin).
- Använd officiella filer (partiernas pressrum / Wikimedia Commons) och se till att
  licens-/varumärkesvillkor tillåter användningen.

Den här mappen används inte längre för att servera logotyper.

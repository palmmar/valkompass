# Partilogotyper

Lägg partiernas logotyper här. Filerna serveras statiskt och visas av
`<PartyLogo>` (`frontend/src/components/party-logo.tsx`) på resultatsidan.

## Namngivning

En fil per parti, namngiven med partikoden i **gemener** + `.svg`:

| Parti | Kod | Filnamn |
|-------|-----|---------|
| Vänsterpartiet | V | `v.svg` |
| Socialdemokraterna | S | `s.svg` |
| Miljöpartiet | MP | `mp.svg` |
| Centerpartiet | C | `c.svg` |
| Liberalerna | L | `l.svg` |
| Kristdemokraterna | KD | `kd.svg` |
| Moderaterna | M | `m.svg` |
| Sverigedemokraterna | SD | `sd.svg` |

## Tips

- **SVG föredras** (skalar skarpt i alla storlekar). Vill du använda PNG i
  stället är det en enrads-ändring i `party-logo.tsx` (filändelsen).
- Helst med **transparent bakgrund**; logotypen centreras med `object-contain`
  och visas i storlekar mellan 20 och 56 px.
- Saknas en fil visas automatiskt en färgad cirkel med partikoden som fallback –
  inget kraschar, så du kan lägga till logotyper en i taget.
- Använd officiella filer (partiernas pressrum / Wikimedia Commons) och se till
  att licens/varumärkesvillkor tillåter användningen.

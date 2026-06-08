#!/usr/bin/env python3
"""
Genererar questions.json och positions.json för valkompassen.

Frågorna är fördelade efter väljarnas viktigaste frågor (Novus 2025: sjukvård, skola,
lag och ordning, migration, klimat/energi, ekonomi, försvar, arbetsmarknad, äldreomsorg).

Partiernas positioner källsätts från sourcing/sources.json (verkliga, hämtade
webbkällor: partiprogram/"Vår politik A–Ö", riksdagsvoteringar, etablerade valkompasser,
trovärdig nyhetsrapportering). Värdelistan nedan är det ursprungliga UTKASTET och används
numera bara som fallback för celler som ännu inte finns i sources.json.

Frågornas nulägesbeskrivningar (explanation + explanationSourceUrl) källsätts på samma sätt
från sourcing/explanations.json för de frågor som är formulerade som en förändring mot i dag
("A-kassan ska höjas" osv.). Den korta explanation-texten i Q nedan används som fallback för
frågor som saknas i explanations.json.

Skala: 4 = håller helt med, 3 = delvis med, 2 = delvis emot, 1 = håller inte med, None = oklar.
Partiordning i value-listan: [V, S, MP, C, L, KD, M, SD]
"""
import json
import os

PARTIES = ["V", "S", "MP", "C", "L", "KD", "M", "SD"]

# (externalKey, categorySlug, text, explanation, [V,S,MP,C,L,KD,M,SD])
Q = [
    # --- Sjukvård ---
    ("sjukvard-statlig-styrning", "sjukvard",
     "Staten ska ta över ansvaret för sjukvården från regionerna",
     "Om sjukvårdens huvudmannaskap ska flyttas från regionerna till staten.",
     [2, 2, 2, 1, 4, 4, 3, 4]),
    ("sjukvard-vinstforbud", "sjukvard",
     "Vinster i välfärden ska förbjudas",
     "Om privata utförare inom vård, skola och omsorg ska få ta ut vinst.",
     [4, 3, 3, 1, 1, 1, 1, 2]),
    ("sjukvard-privat-vard", "sjukvard",
     "Privata vårdgivare är bra för sjukvården",
     "Om privata aktörer förbättrar vårdens kvalitet och tillgänglighet.",
     [1, 2, 2, 4, 4, 4, 4, 3]),
    ("sjukvard-mer-resurser", "sjukvard",
     "Mer skattepengar ska gå till sjukvården även om det kräver höjda skatter",
     "Om vården bör tillföras mer resurser även till priset av högre skatt.",
     [4, 3, 3, 2, 2, 3, 2, 3]),
    ("sjukvard-tandvard", "sjukvard",
     "Tandvården ska ingå i högkostnadsskyddet på samma sätt som annan sjukvård",
     "Om tandvård ska subventioneras som övrig hälso- och sjukvård.",
     [4, 4, 4, 3, 2, 3, 2, 3]),
    ("sjukvard-privata-forsakringar", "sjukvard",
     "Privata sjukvårdsförsäkringar ska begränsas",
     "Om försäkringar som ger snabbare vård ska begränsas.",
     [4, 3, 3, 1, 1, 2, 1, 2]),

    # --- Lag och ordning ---
    ("lag-skarpta-straff", "lag-ordning",
     "Straffen för våldsbrott ska skärpas kraftigt",
     "Om längre fängelsestraff för våldsbrott.",
     [2, 3, 2, 3, 4, 4, 4, 4]),
    ("lag-fler-poliser", "lag-ordning",
     "Sverige ska anställa betydligt fler poliser",
     "Om en stor utökning av poliskåren.",
     [3, 4, 3, 4, 4, 4, 4, 4]),
    ("lag-visitationszoner", "lag-ordning",
     "Polisen ska få inrätta visitationszoner där de kan kroppsvisitera utan konkret brottsmisstanke",
     "Om polisen ska få utökade befogenheter i särskilt utsatta områden.",
     [1, 2, 1, 1, 3, 4, 4, 4]),
    ("lag-anonyma-vittnen", "lag-ordning",
     "Anonyma vittnen ska tillåtas i fler brottmål",
     "Om vittnen ska kunna vara anonyma för att fler ska våga vittna.",
     [2, 3, 1, 2, 3, 4, 4, 4]),
    ("lag-sankt-straffmyndighet", "lag-ordning",
     "Barn under 15 år ska kunna straffas för brott",
     "Om straffmyndighetsåldern ska sänkas.",
     [1, 1, 1, 1, 2, 3, 2, 4]),
    ("lag-kameraovervakning", "lag-ordning",
     "Kameraövervakningen på allmänna platser ska byggas ut kraftigt",
     "Om mer övervakning för att förebygga och klara upp brott.",
     [2, 3, 2, 3, 3, 4, 4, 4]),

    # --- Migration och integration ---
    ("migration-faerre-asyl", "migration",
     "Sverige ska ta emot färre asylsökande",
     "Om asylmottagandet bör minska jämfört med i dag.",
     [1, 3, 1, 2, 3, 4, 4, 4]),
    ("migration-medborgarskap", "migration",
     "Det ska bli svårare att få svenskt medborgarskap",
     "Om hårdare krav på tid, språk och kunskaper för medborgarskap.",
     [1, 3, 1, 2, 3, 4, 4, 4]),
    ("migration-arbetskraft", "migration",
     "Arbetskraftsinvandringen ska begränsas till högkvalificerade yrken",
     "Om arbetskraftsinvandring för lägre kvalificerade jobb ska begränsas.",
     [2, 3, 2, 1, 2, 3, 3, 4]),
    ("migration-atervandring", "migration",
     "Staten ska aktivt uppmuntra invandrare att återvandra till sina hemländer",
     "Om staten ska driva program för frivillig återvandring.",
     [1, 1, 1, 1, 1, 3, 2, 4]),
    ("migration-sprakkrav-bidrag", "migration",
     "Det ska krävas kunskaper i svenska för att få vissa bidrag",
     "Om språkkrav ska kopplas till rätten till bidrag.",
     [1, 3, 2, 3, 4, 4, 4, 4]),
    ("migration-utvisning-vandel", "migration",
     "Det ska bli lättare att utvisa utländska medborgare på grund av bristande livsföring",
     "Om utvisning ska kunna ske även utan att personen dömts för brott.",
     [1, 2, 1, 1, 3, 4, 4, 4]),

    # --- Ekonomi och skatter ---
    ("ekonomi-hoginkomstskatt", "ekonomi-skatter",
     "Skatten på höga inkomster ska höjas",
     "Om personer med höga inkomster ska betala mer i skatt.",
     [4, 3, 3, 1, 1, 1, 1, 2]),
    ("ekonomi-formogenhetsskatt", "ekonomi-skatter",
     "Sverige ska återinföra en skatt på stora förmögenheter",
     "Om en förmögenhetsskatt ska införas igen.",
     [4, 2, 3, 1, 1, 1, 1, 1]),
    ("ekonomi-sankt-skatt-arbete", "ekonomi-skatter",
     "Skatten på arbete ska sänkas",
     "Om inkomstskatten generellt ska sänkas.",
     [1, 2, 2, 4, 4, 4, 4, 3]),
    ("ekonomi-drivmedelsskatt", "ekonomi-skatter",
     "Skatten på bensin och diesel ska sänkas",
     "Om drivmedelsskatterna ska sänkas för att hålla nere priserna.",
     [1, 2, 1, 2, 2, 4, 3, 4]),
    ("ekonomi-bistand", "ekonomi-skatter",
     "Sverige ska minska biståndet till andra länder",
     "Om biståndsbudgeten ska minska.",
     [1, 2, 1, 1, 2, 3, 3, 4]),

    # --- Klimat och energi ---
    ("klimat-karnkraft-utbyggnad", "klimat-energi",
     "Sverige ska bygga ut kärnkraften",
     "Om ny kärnkraft bör byggas för elförsörjningen.",
     [1, 3, 1, 2, 4, 4, 4, 4]),
    ("klimat-skarpta-mal", "klimat-energi",
     "Sveriges klimatmål ska skärpas",
     "Om Sverige ska sätta tuffare mål för utsläppsminskningar.",
     [4, 3, 4, 3, 2, 2, 2, 1]),
    ("klimat-vindkraft", "klimat-energi",
     "Utbyggnaden av vindkraft ska påskyndas",
     "Om mer vindkraft ska byggas snabbare.",
     [3, 3, 4, 4, 3, 2, 2, 1]),
    ("klimat-flygskatt", "klimat-energi",
     "Flygskatten ska höjas för att minska klimatutsläppen",
     "Om skatten på flygresor ska höjas.",
     [4, 3, 4, 2, 1, 1, 1, 1]),
    ("klimat-reduktionsplikt", "klimat-energi",
     "Reduktionsplikten ska sänkas för att hålla nere drivmedelspriserna",
     "Om kravet på inblandning av biodrivmedel ska minska.",
     [1, 2, 1, 2, 3, 4, 3, 4]),
    ("klimat-prioritet", "klimat-energi",
     "Klimatpolitiken ska prioriteras även om det kostar jobb och tillväxt på kort sikt",
     "Om klimatåtgärder bör gå före kortsiktig ekonomisk tillväxt.",
     [4, 3, 4, 2, 2, 1, 1, 1]),

    # --- Skola ---
    ("skola-friskolor-vinst", "skola",
     "Vinstdrivande friskolor ska förbjudas",
     "Om aktiebolag ska få driva skattefinansierade skolor med vinstsyfte.",
     [4, 3, 3, 1, 1, 1, 1, 2]),
    ("skola-tidigare-betyg", "skola",
     "Betyg ska ges från lägre åldrar än i dag",
     "Om betyg ska införas tidigare i grundskolan.",
     [1, 2, 1, 2, 4, 4, 3, 3]),
    ("skola-statlig-skola", "skola",
     "Staten ska ta över ansvaret för skolan från kommunerna",
     "Om skolans huvudmannaskap ska förstatligas.",
     [2, 3, 2, 1, 4, 3, 3, 3]),
    ("skola-mobilforbud", "skola",
     "Mobiltelefoner ska förbjudas under hela skoldagen",
     "Om ett generellt mobilförbud i skolan.",
     [3, 3, 3, 3, 4, 4, 4, 4]),
    ("skola-religiosa-friskolor", "skola",
     "Religiösa friskolor ska förbjudas",
     "Om konfessionella friskolor ska få finnas kvar.",
     [4, 3, 3, 1, 3, 1, 2, 3]),

    # --- Arbetsmarknad ---
    ("arbete-las", "arbetsmarknad",
     "Anställningsskyddet ska försvagas för att göra det lättare att säga upp personal",
     "Om arbetsrätten (LAS) ska göras mer flexibel för arbetsgivare.",
     [1, 2, 1, 4, 3, 3, 4, 3]),
    ("arbete-akassa", "arbetsmarknad",
     "A-kassan ska höjas",
     "Om ersättningen vid arbetslöshet ska bli högre.",
     [4, 3, 3, 1, 1, 2, 1, 2]),
    ("arbete-bidragstak", "arbetsmarknad",
     "Det ska finnas ett tak för hur mycket bidrag ett hushåll kan få",
     "Om ett bidragstak ska göra det mer lönsamt att arbeta.",
     [1, 2, 1, 3, 4, 4, 4, 4]),
    ("arbete-arbetstid", "arbetsmarknad",
     "Arbetstiden ska förkortas med bibehållen lön",
     "Om normalarbetstiden ska sänkas, t.ex. till sex timmar.",
     [4, 2, 3, 1, 1, 1, 1, 2]),

    # --- Försvar och säkerhet ---
    ("forsvar-okade-utgifter", "forsvar",
     "Sverige ska öka försvarsutgifterna ytterligare",
     "Om försvarsanslagen ska höjas mer än i dag.",
     [2, 3, 2, 4, 4, 4, 4, 4]),
    ("forsvar-varnplikt", "forsvar",
     "Värnplikten ska byggas ut och omfatta fler",
     "Om fler ska göra militär grundutbildning.",
     [3, 4, 3, 4, 4, 4, 4, 4]),
    ("forsvar-karnvapen", "forsvar",
     "Kärnvapen ska få placeras på svensk mark om säkerhetsläget kräver det",
     "Om Sverige ska tillåta kärnvapen på sitt territorium.",
     [1, 2, 1, 2, 3, 3, 3, 3]),
    ("forsvar-ukraina", "forsvar",
     "Sverige ska fortsätta ge omfattande militärt stöd till Ukraina",
     "Om det militära stödet till Ukraina ska ligga kvar på hög nivå.",
     [3, 4, 4, 4, 4, 4, 4, 3]),

    # --- Äldreomsorg ---
    ("aldre-resurser", "aldreomsorg",
     "Mer resurser ska satsas på äldreomsorgen även om det kräver höjd skatt",
     "Om äldreomsorgen bör tillföras mer pengar även till priset av högre skatt.",
     [4, 3, 3, 2, 2, 3, 2, 3]),
    ("aldre-pension", "aldreomsorg",
     "De lägsta pensionerna ska höjas",
     "Om garantipension och låga pensioner ska bli högre.",
     [4, 3, 3, 2, 2, 3, 2, 4]),
    ("aldre-privat-omsorg", "aldreomsorg",
     "Privata utförare i äldreomsorgen ska begränsas",
     "Om privata företag i äldreomsorgen ska få en mindre roll.",
     [4, 3, 3, 1, 1, 1, 1, 2]),

    # --- Bostäder ---
    ("bostad-hyressattning", "bostader",
     "Hyrorna ska sättas mer marknadsmässigt",
     "Om hyrorna ska bestämmas friare i stället för genom dagens förhandlingssystem.",
     [1, 2, 2, 4, 4, 3, 4, 2]),
    ("bostad-subventioner", "bostader",
     "Staten ska subventionera byggandet av fler hyresrätter",
     "Om statliga stöd ska öka byggandet av hyresrätter.",
     [4, 4, 4, 1, 1, 2, 1, 2]),

    # --- EU och omvärlden ---
    ("eu-mer-makt", "eu",
     "EU ska få mer makt på bekostnad av medlemsländerna",
     "Om mer beslutsmakt ska flyttas från Sverige till EU.",
     [1, 2, 3, 3, 4, 2, 2, 1]),
    ("eu-euro", "eu",
     "Sverige ska införa euron som valuta",
     "Om Sverige ska byta krona mot euro.",
     [1, 1, 1, 2, 4, 2, 2, 1]),
    ("eu-forsvarssamarbete", "eu",
     "Sverige bör vara drivande för ett tätare försvarssamarbete inom EU",
     "Om Sverige ska driva på för mer gemensamt försvar i EU.",
     [1, 3, 3, 3, 4, 3, 3, 1]),
]

DRAFT_SOURCE = "Utkast – verifieras i admin"


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    content_dir = os.path.normpath(os.path.join(
        here, "..", "src", "Valkompass.Infrastructure", "Seed", "Content"))

    # Källsatta positioner (verkliga webbkällor). Faller tillbaka på utkastet i Q
    # för ev. celler som saknas i sources.json.
    sources_path = os.path.join(here, "sourcing", "sources.json")
    with open(sources_path, encoding="utf-8") as f:
        src_by = {(s["questionKey"], s["partyCode"]): s for s in json.load(f)}

    # Källsatta nulägesbeskrivningar (verkliga webbkällor). Faller tillbaka på den korta
    # explanation-texten i Q för frågor som saknas i explanations.json.
    expl_path = os.path.join(here, "sourcing", "explanations.json")
    with open(expl_path, encoding="utf-8") as f:
        expl_by = {e["questionKey"]: e for e in json.load(f)}

    questions = []
    positions = []
    order_by_cat = {}

    for key, cat, text, expl, values in Q:
        order_by_cat[cat] = order_by_cat.get(cat, 0) + 1
        e = expl_by.get(key)
        questions.append({
            "externalKey": key,
            "categorySlug": cat,
            "displayOrder": order_by_cat[cat],
            "text": text,
            "explanation": e["explanation"] if e else expl,
            "explanationSourceUrl": e["sourceUrl"] if e else None,
        })
        for party, value in zip(PARTIES, values):
            src = src_by.get((key, party))
            if src is not None and src.get("value") is not None:
                positions.append({
                    "partyCode": party,
                    "questionKey": key,
                    "value": src["value"],
                    "motivation": src["motivation"],
                    "sourceCitation": src["sourceCitation"],
                    "sourceUrl": src["sourceUrl"],
                })
            elif value is not None:
                positions.append({
                    "partyCode": party,
                    "questionKey": key,
                    "value": value,
                    "motivation": None,
                    "sourceCitation": DRAFT_SOURCE,
                    "sourceUrl": None,
                })

    with open(os.path.join(content_dir, "questions.json"), "w", encoding="utf-8") as f:
        json.dump(questions, f, ensure_ascii=False, indent=2)
        f.write("\n")
    with open(os.path.join(content_dir, "positions.json"), "w", encoding="utf-8") as f:
        json.dump(positions, f, ensure_ascii=False, indent=2)
        f.write("\n")

    sourced_expl = sum(1 for q in questions if q["explanationSourceUrl"])
    print(f"Wrote {len(questions)} questions ({sourced_expl} med källsatt nuläge) "
          f"and {len(positions)} positions to {content_dir}")


if __name__ == "__main__":
    main()

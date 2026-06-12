#!/usr/bin/env python3
"""
Genererar questions.json och positions.json för valkompassen.

Frågorna är fördelade efter väljarnas viktigaste frågor (Novus 2025/2026: sjukvård, skola,
lag och ordning, migration, klimat/energi, ekonomi, försvar, arbetsmarknad, äldreomsorg).
Varje fråga har en djupnivå (TIER 1–3) som styr quizlägena Snabb 25 / Standard 50 /
Fördjupning 75 – lägena är nästlade (25 ⊂ 50 ⊂ 75).

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
    ("sjukvard-dodshjalp", "sjukvard",
     "Aktiv dödshjälp ska tillåtas för svårt sjuka i livets slutskede",
     "Om dödshjälp ska legaliseras under strikta villkor.",
     [3, 2, 3, 2, 3, 1, 2, 3]),
    ("sjukvard-surrogat", "sjukvard",
     "Altruistiskt surrogatmödraskap ska tillåtas i Sverige",
     "Om surrogatmödraskap utan ersättning ska bli lagligt.",
     [1, 1, None, 4, 4, 1, 4, 2]),

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
    ("lag-preventiv-avlyssning", "lag-ordning",
     "Polisen ska få använda hemlig avlyssning mot personer som inte är misstänkta för ett konkret brott",
     "Om preventiva tvångsmedel ska få användas utan konkret brottsmisstanke.",
     [1, 3, 1, 2, 4, 4, 4, 4]),
    ("lag-narkotika-avkriminalisera", "lag-ordning",
     "Eget bruk av narkotika ska avkriminaliseras",
     "Om bruk av narkotika ska mötas med vård i stället för straff.",
     [4, 1, 3, 4, 2, 1, 1, 1]),

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
    ("migration-anhorig-begransa", "migration",
     "Försörjningskraven för anhöriginvandring ska skärpas kraftigt",
     "Om kraven för att ta hit anhöriga ska höjas ytterligare.",
     [1, 3, 1, 2, 4, 4, 4, 4]),
    ("migration-tiggeriforbud", "migration",
     "Tiggeri ska förbjudas i hela Sverige",
     "Om ett nationellt förbud mot tiggeri ska införas.",
     [1, 2, 1, 1, 2, 3, 4, 4]),

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
    ("ekonomi-arvsskatt", "ekonomi-skatter",
     "Sverige ska återinföra en arvsskatt på stora arv",
     "Om en skatt på stora arv ska införas igen.",
     [4, 2, 3, 1, 1, 1, 1, 1]),
    ("ekonomi-rut-avskaffa", "ekonomi-skatter",
     "RUT-avdraget för hushållsnära tjänster ska avskaffas",
     "Om skattereduktionen för hushållsnära tjänster ska tas bort.",
     [4, 2, 2, 1, 1, 1, 1, 1]),
    ("ekonomi-matmoms", "ekonomi-skatter",
     "Den sänkta momsen på livsmedel ska göras permanent",
     "Om den tillfälligt sänkta matmomsen ska bli permanent.",
     [4, 3, 4, 2, 2, 2, 2, 3]),

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
    ("klimat-bilforbud-2035", "klimat-energi",
     "EU:s förbud mot försäljning av nya bensin- och dieselbilar från 2035 ska rivas upp",
     "Om utfasningen av nya fossilbilar i EU ska stoppas.",
     [1, 2, 1, 1, 2, 4, 3, 4]),
    ("klimat-hoghastighetstag", "klimat-energi",
     "Sverige ska bygga nya stambanor för höghastighetståg",
     "Om nya stambanor för snabbtåg ska byggas mellan storstäderna.",
     [4, 3, 4, 3, 1, 1, 1, 1]),

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
    ("skola-skolval-lottning", "skola",
     "Det fria skolvalet ska begränsas och elever fördelas genom ett gemensamt, offentligt antagningssystem",
     "Om antagningen till skolor ska samordnas offentligt i stället för köer.",
     [4, 3, 3, 1, 1, 1, 1, 1]),
    ("skola-sprakforskola", "skola",
     "Förskola ska vara obligatorisk för barn som inte talar tillräckligt bra svenska",
     "Om obligatorisk språkförskola ska införas för barn med bristande svenska.",
     [2, 3, 2, 3, 4, 4, 4, 3]),

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
    ("arbete-ingangsloner", "arbetsmarknad",
     "Det ska bli lättare att anställa till lägre ingångslöner",
     "Om lägre ingångslöner ska sänka trösklarna in på arbetsmarknaden.",
     [1, 1, 2, 4, 4, 4, 4, 2]),
    ("arbete-strejkratt", "arbetsmarknad",
     "Rätten att ta till strejk och sympatiåtgärder ska begränsas",
     "Om en proportionalitetsprincip ska begränsa konflikträtten.",
     [1, 1, 1, 3, 4, 4, 4, 3]),

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
    ("forsvar-utlandska-baser", "forsvar",
     "Permanenta utländska militärbaser ska inte tillåtas på svensk mark i fredstid",
     "Om utländsk stadigvarande militär närvaro i Sverige ska förbjudas.",
     [4, 2, 4, 1, 1, 1, 1, 2]),
    ("forsvar-vapenexport", "forsvar",
     "Sverige ska förbjuda vapenexport till länder som inte är demokratier",
     "Om vapenexporten till icke-demokratier ska stoppas.",
     [4, 2, 4, 2, 2, 1, 1, 2]),

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
    ("aldre-pensionsalder", "aldreomsorg",
     "Pensionsåldern ska höjas i takt med att vi lever längre",
     "Om åldersgränserna i pensionssystemet ska följa medellivslängden.",
     [1, 4, 3, 4, 4, 4, 4, 1]),
    ("aldre-boendegaranti", "aldreomsorg",
     "Alla över 85 år ska ha rätt till plats på äldreboende utan biståndsprövning",
     "Om en äldreboendegaranti ska införas för de äldsta.",
     [3, 2, 2, 4, 3, 4, 2, 4]),

    # --- Bostäder ---
    ("bostad-hyressattning", "bostader",
     "Hyrorna ska sättas mer marknadsmässigt",
     "Om hyrorna ska bestämmas friare i stället för genom dagens förhandlingssystem.",
     [1, 2, 2, 4, 4, 3, 4, 2]),
    ("bostad-subventioner", "bostader",
     "Staten ska subventionera byggandet av fler hyresrätter",
     "Om statliga stöd ska öka byggandet av hyresrätter.",
     [4, 4, 4, 1, 1, 2, 1, 2]),
    ("bostad-flyttskatt", "bostader",
     "Skatten på vinst vid bostadsförsäljning ska sänkas eller avskaffas",
     "Om reavinstskatten ska sänkas för att öka rörligheten på bostadsmarknaden.",
     [1, 1, 1, 3, 4, 4, 3, 3]),
    ("bostad-ranteavdrag", "bostader",
     "Ränteavdragen på bolån ska trappas ned",
     "Om skattesubventionen av bolåneräntor ska minskas stegvis.",
     [4, 2, 4, 3, 1, 1, 1, 1]),
    ("bostad-strandskydd", "bostader",
     "Strandskyddet ska luckras upp för att göra det lättare att bygga nära vatten",
     "Om reglerna för byggande i strandnära lägen ska bli mindre strikta.",
     [1, 2, 1, 4, 3, 4, 4, 3]),

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
    ("eu-gemensam-skuld", "eu",
     "EU ska kunna ta gemensamma lån för att finansiera försvar och investeringar",
     "Om EU ska få låna gemensamt i stället för enbart via medlemsavgifter.",
     [2, 4, 3, 4, 2, 2, 1, 1]),
    ("eu-ukraina-medlemskap", "eu",
     "Sverige ska driva på för att Ukraina blir medlem i EU",
     "Om Sverige aktivt ska stödja ett ukrainskt EU-medlemskap.",
     [2, 4, 4, 4, 4, 3, 4, 2]),
    ("eu-asylpakt", "eu",
     "Asyl- och flyktingmottagandet ska fördelas mellan EU-länderna genom en gemensam mekanism",
     "Om EU:s migrationspakt med solidarisk fördelning mellan länderna.",
     [2, 3, 3, 4, 4, 3, 3, 1]),
]

# Djupnivå per fråga: 1 = Snabb (25), 2 = Standard (50), 3 = Fördjupning (75).
# Rangordning efter väljarnas viktigaste frågor (Novus 2026) med kategoribalans;
# nivå 1 innehåller minst fyra ekonomi- och fyra GAL-TAN-laddade frågor så att
# den politiska kartan kan ritas även i snabbläget.
TIER = {
    # Sjukvård: 3/3/2
    "sjukvard-vinstforbud": 1, "sjukvard-privat-vard": 1, "sjukvard-mer-resurser": 1,
    "sjukvard-statlig-styrning": 2, "sjukvard-tandvard": 2, "sjukvard-privata-forsakringar": 2,
    "sjukvard-dodshjalp": 3, "sjukvard-surrogat": 3,
    # Lag och ordning: 3/3/2
    "lag-skarpta-straff": 1, "lag-visitationszoner": 1, "lag-sankt-straffmyndighet": 1,
    "lag-fler-poliser": 2, "lag-anonyma-vittnen": 2, "lag-kameraovervakning": 2,
    "lag-preventiv-avlyssning": 3, "lag-narkotika-avkriminalisera": 3,
    # Migration: 2/3/3
    "migration-faerre-asyl": 1, "migration-atervandring": 1,
    "migration-medborgarskap": 2, "migration-sprakkrav-bidrag": 2, "migration-utvisning-vandel": 2,
    "migration-arbetskraft": 3, "migration-anhorig-begransa": 3, "migration-tiggeriforbud": 3,
    # Ekonomi och skatter: 3/2/3
    "ekonomi-hoginkomstskatt": 1, "ekonomi-sankt-skatt-arbete": 1, "ekonomi-bistand": 1,
    "ekonomi-formogenhetsskatt": 2, "ekonomi-drivmedelsskatt": 2,
    "ekonomi-arvsskatt": 3, "ekonomi-rut-avskaffa": 3, "ekonomi-matmoms": 3,
    # Klimat och energi: 2/3/3
    "klimat-karnkraft-utbyggnad": 1, "klimat-prioritet": 1,
    "klimat-skarpta-mal": 2, "klimat-vindkraft": 2, "klimat-reduktionsplikt": 2,
    "klimat-flygskatt": 3, "klimat-bilforbud-2035": 3, "klimat-hoghastighetstag": 3,
    # Skola: 3/2/2
    "skola-friskolor-vinst": 1, "skola-statlig-skola": 1, "skola-skolval-lottning": 1,
    "skola-tidigare-betyg": 2, "skola-religiosa-friskolor": 2,
    "skola-mobilforbud": 3, "skola-sprakforskola": 3,
    # Arbetsmarknad: 2/2/2
    "arbete-akassa": 1, "arbete-bidragstak": 1,
    "arbete-las": 2, "arbete-arbetstid": 2,
    "arbete-ingangsloner": 3, "arbete-strejkratt": 3,
    # Försvar och säkerhet: 2/2/2
    "forsvar-okade-utgifter": 1, "forsvar-karnvapen": 1,
    "forsvar-varnplikt": 2, "forsvar-ukraina": 2,
    "forsvar-utlandska-baser": 3, "forsvar-vapenexport": 3,
    # Äldreomsorg: 2/2/1
    "aldre-resurser": 1, "aldre-pension": 1,
    "aldre-privat-omsorg": 2, "aldre-pensionsalder": 2,
    "aldre-boendegaranti": 3,
    # Bostäder: 1/2/2
    "bostad-hyressattning": 1,
    "bostad-subventioner": 2, "bostad-strandskydd": 2,
    "bostad-flyttskatt": 3, "bostad-ranteavdrag": 3,
    # EU och omvärlden: 2/1/3
    "eu-euro": 1, "eu-mer-makt": 1,
    "eu-forsvarssamarbete": 2,
    "eu-gemensam-skuld": 3, "eu-ukraina-medlemskap": 3, "eu-asylpakt": 3,
}

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

    missing_tier = [key for key, *_ in Q if key not in TIER]
    if missing_tier:
        raise SystemExit(f"Frågor utan TIER-nivå: {missing_tier}")
    tier_counts = {t: sum(1 for v in TIER.values() if v == t) for t in (1, 2, 3)}
    if tier_counts != {1: 25, 2: 25, 3: 25}:
        raise SystemExit(f"Fel nivåfördelning (förväntat 25/25/25): {tier_counts}")

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
            "tier": TIER[key],
            "text": text,
            "explanation": e["explanation"] if e else expl,
            "explanationSourceUrl": e["sourceUrl"] if e else None,
        })
        for party, value in zip(PARTIES, values):
            src = src_by.get((key, party))
            if src is not None:
                # Källsatt cell vinner alltid – även value=None (dokumenterat oklar
                # position; frågan exkluderas då i matchningen för det partiet).
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

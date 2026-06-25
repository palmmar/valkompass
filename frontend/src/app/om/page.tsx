import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Om & metod – Valkompass 2026",
};

export default function OmPage() {
  return (
    <div className="mx-auto max-w-2xl space-y-6 px-4 py-10">
      <h1 className="text-3xl font-bold tracking-tight">Om valkompassen</h1>

      <section className="space-y-2">
        <h2 className="text-xl font-semibold">Vad är det här?</h2>
        <p className="text-muted-foreground">
          Valkompassen hjälper dig att se hur väl dina åsikter stämmer överens med de åtta
          riksdagspartierna inför valet 2026. Du svarar på ett antal påståenden och får en
          matchning – totalt, per politikområde och fråga för fråga.
        </p>
      </section>

      <section className="space-y-2">
        <h2 className="text-xl font-semibold">Hur väljs frågorna?</h2>
        <p className="text-muted-foreground">
          Frågorna är fördelade efter de samhällsfrågor väljarna anser är viktigast, utifrån
          opinionsmätningar (t.ex. Novus och SOM-institutet). Områden som sjukvård, lag och
          ordning, migration, klimat och energi, skola och ekonomi väger därför tyngre.
        </p>
      </section>

      <section className="space-y-2">
        <h2 className="text-xl font-semibold">Hur räknas matchningen?</h2>
        <p className="text-muted-foreground">
          Varje påstående besvaras på en fyrgradig skala (håller med – håller inte med). Din
          överensstämmelse med ett parti beräknas som avståndet mellan dina och partiets svar.
          Frågor du markerar som “extra viktiga” väger dubbelt. Frågor du hoppar över, och
          frågor där ett parti saknar tydlig position, påverkar inte matchningen.
        </p>
      </section>

      <section className="space-y-2">
        <h2 className="text-xl font-semibold">Hur räknas den politiska kartan?</h2>
        <p className="text-muted-foreground">
          På resultatsidan placeras du och partierna på två axlar: en ekonomisk vänster–höger och
          en GAL–TAN (grön/alternativ/libertär ↔ traditionell/auktoritär/nationalistisk). Varje
          påstående knyts till den axel det laddar på. Ett partis position är medelvärdet av dess
          källsatta svar på axelns frågor, och din prick beräknas på exakt samma sätt utifrån dina
          svar – så att du och partierna mäts med samma måttstock.
        </p>
        <p className="text-muted-foreground">
          GAL–TAN kompletterar vänster–höger genom att fånga sociala och kulturella värderingar i
          stället för ekonomi och fördelning. <span className="font-medium text-foreground">GAL</span>{" "}
          står för Grön, Alternativ och Libertär – en frihetlig sida som betonar miljö och klimat,
          öppenhet, individens fri- och rättigheter, mångfald och tolerans.{" "}
          <span className="font-medium text-foreground">TAN</span> står för Traditionell, Auktoritär
          och Nationalistisk – en sida som betonar ordning och trygghet, fasta normer och
          traditioner, starkare statlig auktoritet och nationell gemenskap. De flesta partier och
          personer hamnar någonstans mellan ytterligheterna.
        </p>
        <p className="text-muted-foreground">
          För att axlarna ska vara oberoende och inte bara en bedömning kalibreras och kontrolleras
          de mot{" "}
          <a
            href="https://www.chesdata.eu/ches-europe"
            target="_blank"
            rel="noopener noreferrer"
            className="underline underline-offset-2"
          >
            Chapel Hill Expert Survey (2024)
          </a>
          , en oberoende akademisk skattning där statsvetare placerar partierna. Rangordningen ur
          våra data stämmer mycket väl med deras. Kartan är ändå en förenkling: axlarna väger tungt
          på lag och ordning, migration och klimat, frågor om försvar och EU lämnas utanför, och
          måttet säger inget om hur central en fråga är för ett parti.
        </p>
      </section>

      <section className="space-y-2">
        <h2 className="text-xl font-semibold">Sparas mina svar?</h2>
        <p className="text-muted-foreground">
          Medan du gör kompassen sparas dina svar lokalt i din webbläsare (localStorage) så att
          du kan ladda om sidan eller komma tillbaka senare och fortsätta där du slutade.
          Informationen stannar i din webbläsare – inget skickas till oss förrän du själv väljer
          att se ditt resultat, och då lagras svaren anonymt utan koppling till dig. Du kan när
          som helst börja om via knappen “Börja om” på kompassens startsida, eller rensa
          lagringen i webbläsarens inställningar.
        </p>
      </section>

      <section className="space-y-2">
        <h2 className="text-xl font-semibold">Neutralitet och källor</h2>
        <p className="text-muted-foreground">
          Valkompassen är obunden och inte knuten till något parti. Partiernas positioner
          bygger på primärkällor – partiprogram, valmanifest, voteringar och officiella
          uttalanden – och är en tolkning som kan förenkla komplexa frågor. Hittar du ett fel?
          Hör av dig så rättar vi.
        </p>
      </section>
    </div>
  );
}

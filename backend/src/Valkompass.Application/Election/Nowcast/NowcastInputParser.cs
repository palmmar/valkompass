using System.Text.Json;

namespace Valkompass.Application.Election.Nowcast;

/// <summary>
/// Läser distrikts- och kommunnivån ur Valmyndigheten resultatfiler till prognosmodellens
/// indata.
/// </summary>
/// <remarks>
/// Röstfördelningsfilen är ca 40 MB. Den läses med <see cref="Utf8JsonReader"/> och projiceras
/// direkt till kompakta poster, i stället för att materialiseras som ett JsonDocument – det
/// senare skulle kosta flera hundra megabyte för data vi ändå bara sammanfattar.
/// </remarks>
public static class NowcastInputParser
{
    /// <summary>Läser valdistrikten ur <c>rostfordelning</c>-filen.</summary>
    public static IReadOnlyList<DistrictSnapshot> ParseDistricts(ReadOnlySpan<byte> utf8Json)
    {
        var districts = new List<DistrictSnapshot>(7000);
        var reader = new Utf8JsonReader(utf8Json, isFinalBlock: true, state: default);

        if (!SeekArray(ref reader, "valdistrikt"))
        {
            throw new ElectionResultFormatException("Filen saknar listan valdistrikt.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            districts.Add(ReadDistrict(ref reader));
        }

        return districts;
    }

    /// <summary>Läser kommunerna ur <c>summering</c>-filen.</summary>
    public static IReadOnlyList<MunicipalitySnapshot> ParseMunicipalities(ReadOnlySpan<byte> utf8Json)
    {
        var municipalities = new List<MunicipalitySnapshot>(300);
        var reader = new Utf8JsonReader(utf8Json, isFinalBlock: true, state: default);

        if (!SeekArray(ref reader, "kommuner"))
        {
            throw new ElectionResultFormatException("Filen saknar listan kommuner.");
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            municipalities.Add(ReadMunicipality(ref reader));
        }

        return municipalities;
    }

    private static DistrictSnapshot ReadDistrict(ref Utf8JsonReader reader)
    {
        string? code = null, municipality = null, county = null, status = null;
        bool reported = false;
        int? eligible = null;
        var votes = new VoteBlock();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("valdistriktskod")) code = ReadString(ref reader);
            else if (reader.ValueTextEquals("kommunkod")) municipality = ReadString(ref reader);
            else if (reader.ValueTextEquals("lankod")) county = ReadString(ref reader);
            else if (reader.ValueTextEquals("statusJamforelse")) status = ReadString(ref reader);
            else if (reader.ValueTextEquals("antalRostberattigade")) eligible = ReadNullableInt(ref reader);
            // Ett distrikt som saknar rapporteringstid har ännu inte räknats. Valmyndigheten
            // skriver tom sträng för dem, inte null – en delvis räknad fil listar alltså alla
            // 6626 distrikten hela kvällen. Tolkas tom sträng som rapporterad ser modellen ett
            // färdigräknat val från första filen och avstår från att prognosticera alls.
            else if (reader.ValueTextEquals("rapporteringsTid"))
                reported = !string.IsNullOrWhiteSpace(ReadString(ref reader));
            else if (reader.ValueTextEquals("rostfordelning")) votes = ReadVoteDistribution(ref reader);
            else SkipValue(ref reader);
        }

        return new DistrictSnapshot(
            Code: code ?? throw new ElectionResultFormatException("Ett valdistrikt saknar valdistriktskod."),
            MunicipalityCode: municipality ?? string.Empty,
            CountyCode: county ?? string.Empty,
            IsReported: reported,
            Comparison: ParseComparison(status),
            EligibleVoters: eligible,
            Votes: votes.Votes,
            VotesPrevious: votes.VotesPrevious,
            Parties: votes.Parties);
    }

    private static MunicipalitySnapshot ReadMunicipality(ref Utf8JsonReader reader)
    {
        string? code = null, county = null;
        int eligible = 0, reported = 0, total = 0;
        var votes = new VoteBlock();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("kommunkod")) code = ReadString(ref reader);
            else if (reader.ValueTextEquals("lankod")) county = ReadString(ref reader);
            else if (reader.ValueTextEquals("antalRostberattigade")) eligible = ReadNullableInt(ref reader) ?? 0;
            else if (reader.ValueTextEquals("antalValdistriktRaknade")) reported = ReadNullableInt(ref reader) ?? 0;
            else if (reader.ValueTextEquals("antalValdistriktSomSkaRaknas")) total = ReadNullableInt(ref reader) ?? 0;
            else if (reader.ValueTextEquals("rostfordelning")) votes = ReadVoteDistribution(ref reader);
            else SkipValue(ref reader);
        }

        return new MunicipalitySnapshot(
            Code: code ?? throw new ElectionResultFormatException("En kommun saknar kommunkod."),
            CountyCode: county ?? string.Empty,
            EligibleVoters: eligible,
            DistrictsReported: reported,
            DistrictsTotal: total,
            Votes: votes.Votes,
            VotesPrevious: votes.VotesPrevious,
            Parties: votes.Parties);
    }

    /// <summary>
    /// Läser <c>rostfordelning.rosterPaverkaMandat</c>. Endast röster som påverkar
    /// mandatfördelningen räknas – det är samma nämnare som Valmyndighetens egna andelar.
    /// </summary>
    private static VoteBlock ReadVoteDistribution(ref Utf8JsonReader reader)
    {
        var block = new VoteBlock();
        if (!StepInto(ref reader, JsonTokenType.StartObject))
        {
            return block;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("rosterPaverkaMandat"))
            {
                block = ReadMandateVotes(ref reader);
            }
            else
            {
                SkipValue(ref reader);
            }
        }

        return block;
    }

    private static VoteBlock ReadMandateVotes(ref Utf8JsonReader reader)
    {
        var parties = new List<PartyVotes>(8);
        int votes = 0;
        int? previous = null;

        if (!StepInto(ref reader, JsonTokenType.StartObject))
        {
            return new VoteBlock();
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("antalRoster")) votes = ReadNullableInt(ref reader) ?? 0;
            else if (reader.ValueTextEquals("antalRosterForegaendeVal")) previous = ReadNullableInt(ref reader);
            else if (reader.ValueTextEquals("partiRoster")) ReadParties(ref reader, parties);
            else SkipValue(ref reader);
        }

        return new VoteBlock { Votes = votes, VotesPrevious = previous, Parties = parties };
    }

    private static void ReadParties(ref Utf8JsonReader reader, List<PartyVotes> parties)
    {
        if (!StepInto(ref reader, JsonTokenType.StartArray))
        {
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            string? code = null;
            int votes = 0;
            int? previous = null;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                if (reader.ValueTextEquals("partiforkortning")) code = ReadString(ref reader);
                else if (reader.ValueTextEquals("antalRoster")) votes = ReadNullableInt(ref reader) ?? 0;
                else if (reader.ValueTextEquals("antalRosterForegaendeVal")) previous = ReadNullableInt(ref reader);
                else SkipValue(ref reader);
            }

            if (code is not null)
            {
                parties.Add(new PartyVotes(code, votes, previous));
            }
        }
    }

    private static DistrictComparison ParseComparison(string? status) => status switch
    {
        "Kan jämföras" => DistrictComparison.Comparable,
        "Jämförs mot summerat" => DistrictComparison.Aggregated,
        _ => DistrictComparison.NotComparable,
    };

    /// <summary>Spolar fram till en namngiven lista på toppnivå och stannar på dess StartArray.</summary>
    private static bool SeekArray(ref Utf8JsonReader reader, string name)
    {
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals(name))
            {
                return reader.Read() && reader.TokenType == JsonTokenType.StartArray;
            }

            SkipValue(ref reader);
        }

        return false;
    }

    /// <summary>
    /// Kliver in i värdet efter ett fältnamn och svarar om det var behållaren vi väntade oss.
    /// </summary>
    /// <remarks>
    /// Valmyndigheten skriver <c>null</c> i stället för objekt och listor för valdistrikt och
    /// kommuner som inte rapporterat – tidigt på valnatten gäller det de allra flesta. Läses
    /// null som om det vore en behållare tappar läsaren nivån och drar in nästa distrikts fält
    /// i det föregående: 6626 distrikt blev 979 hopslagna poster, tyst och utan fel.
    /// Vid null står läsaren kvar på null-token, vilket är precis där anroparen väntar sig den.
    /// </remarks>
    private static bool StepInto(ref Utf8JsonReader reader, JsonTokenType expected)
    {
        reader.Read();
        return reader.TokenType == expected;
    }

    /// <summary>
    /// Konsumerar värdet efter ett fältnamn. Nästlade objekt och listor hoppas över i sin
    /// helhet, så att läsaren stannar kvar på rätt nivå.
    /// </summary>
    private static void SkipValue(ref Utf8JsonReader reader)
    {
        reader.Read();
        reader.Skip();
    }

    private static string? ReadString(ref Utf8JsonReader reader)
    {
        reader.Read();
        return reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
    }

    private static int? ReadNullableInt(ref Utf8JsonReader reader)
    {
        reader.Read();
        return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value) ? value : null;
    }

    private struct VoteBlock
    {
        public int Votes { get; set; }
        public int? VotesPrevious { get; set; }
        public IReadOnlyList<PartyVotes> Parties { get; set; }

        public VoteBlock()
        {
            Votes = 0;
            VotesPrevious = null;
            Parties = [];
        }
    }
}

using System.Globalization;
using System.Text.Json;

namespace Valkompass.Application.Election;

/// <summary>Källfilen gick inte att tolka som ett giltigt riksdagsresultat.</summary>
public sealed class ElectionResultFormatException(string message) : Exception(message);

/// <summary>
/// Tolkar Valmyndighetens resultatfiler till en <see cref="ElectionSnapshot"/>. Rent och
/// databasfritt så att det går att enhetstesta mot arkiverade fixtures utan nätverk – se
/// <c>backend/tests/fixtures/valmyndigheten/</c>.
/// </summary>
/// <remarks>
/// Vi läser <c>mandatfordelning</c>-filen (ca 300 kB), inte <c>rostfordelning</c> (ca 40 MB).
/// Mandatfilen innehåller rikets röstfördelning per parti, den officiella preliminära
/// mandatfördelningen, rapporterade valdistrikt och röstberättigade både totalt och i räknade
/// distrikt – allt som räknat resultat och rapporteringsgrad behöver. Distriktsdatan i den
/// stora filen behövs först för nowcasten (#84) och bör då strömmas, inte läsas som ett
/// dokument i minnet.
/// </remarks>
public static class ElectionResultParser
{
    /// <summary>Riksdagsvalet. Andra valtyper (RF/KF) ingår inte i valvakan.</summary>
    private const string ParliamentaryElection = "RD";

    /// <summary>Tidsstämplarna i filerna saknar offset och avser svensk tid.</summary>
    private static readonly TimeZoneInfo SwedishTime = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Stockholm");

    /// <summary>
    /// Läser en <c>mandatfordelning</c>-fil. <paramref name="checksum"/> är filens summa ur
    /// <c>index.md5</c> och följer med snapshoten så att den går att spåra i loggarna.
    /// </summary>
    public static ElectionSnapshot ParseMandateFile(Stream json, string checksum, DateTimeOffset ingestedAt)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);

        using var document = ParseDocument(json);
        var root = document.RootElement;

        var electionType = RequiredString(root, "valtyp");
        if (!string.Equals(electionType, ParliamentaryElection, StringComparison.OrdinalIgnoreCase))
        {
            throw new ElectionResultFormatException(
                $"Förväntade valtyp {ParliamentaryElection} men filen innehåller {electionType}.");
        }

        var area = RequiredObject(root, "valomrade");
        var votes = RequiredObject(RequiredObject(area, "rostfordelning"), "rosterPaverkaMandat");

        var source = new ElectionSnapshotSource(
            ElectionDate: RequiredDate(root, "valdatum"),
            PreviousElectionDate: RequiredDate(root, "tidigareValdatum"),
            Stage: ParseStage(RequiredString(root, "rakningstillfalle")),
            // Saknad test-flagga tolkas som testdata. Att av misstag märka skarp data som test
            // är ofarligt; det omvända är att publicera genrepssiffror som valresultat.
            IsTest: OptionalBool(root, "test") ?? true,
            UpdatedAt: RequiredTimestamp(root, "senasteUppdateringstid"),
            IngestedAt: ingestedAt,
            Checksum: checksum,
            UpdateCount: OptionalInt(root, "antalUppdateringar") ?? 0);

        var reporting = new ElectionReporting(
            DistrictsReported: RequiredInt(area, "antalValdistriktRaknade"),
            DistrictsTotal: RequiredInt(area, "antalValdistriktSomSkaRaknas"),
            EligibleVotersCovered: OptionalInt(area, "antalRostberattigadeIRaknadeValdistrikt") ?? 0,
            EligibleVotersTotal: RequiredInt(area, "antalRostberattigade"),
            TotalVotes: RequiredInt(area, "totaltAntalRoster"),
            TotalVotesPrevious: OptionalInt(area, "totaltAntalRosterForegaendeVal"),
            TurnoutPercent: OptionalDecimal(area, "valdeltagande"),
            TurnoutPercentPrevious: OptionalDecimal(area, "valdeltagandeForegaendeVal"));

        if (reporting.DistrictsTotal <= 0)
        {
            throw new ElectionResultFormatException("antalValdistriktSomSkaRaknas saknas eller är noll.");
        }

        if (reporting.DistrictsReported > reporting.DistrictsTotal)
        {
            throw new ElectionResultFormatException(
                $"Fler räknade valdistrikt ({reporting.DistrictsReported}) än totalt ({reporting.DistrictsTotal}).");
        }

        return new ElectionSnapshot(
            Source: source,
            Reporting: reporting,
            Results: ParseResults(votes),
            Mandates: ParseMandates(RequiredObject(area, "mandatfordelning")),
            OtherParties: ParseOtherParties(votes),
            ThresholdPercent: OptionalDecimal(area, "valomradessparrProcent") ?? 4m,
            ConstituencyThresholdPercent: OptionalDecimal(area, "valkretssparrProcent") ?? 12m);
    }

    private static IReadOnlyList<PartyResult> ParseResults(JsonElement votes)
    {
        var results = new List<PartyResult>();
        foreach (var party in RequiredArray(votes, "partiRoster").EnumerateArray())
        {
            results.Add(new PartyResult(
                // partiforkortning matchar Party.Code i vår seed (M, C, L, KD, S, V, MP, SD).
                Code: RequiredString(party, "partiforkortning"),
                Name: RequiredString(party, "partibeteckning"),
                DisplayOrder: OptionalInt(party, "ordningsnummer") ?? 0,
                ColorHex: OptionalString(party, "fargkod") ?? string.Empty,
                Votes: RequiredInt(party, "antalRoster"),
                SharePercent: OptionalDecimal(party, "andelRoster") ?? 0m,
                VotesPrevious: OptionalInt(party, "antalRosterForegaendeVal"),
                SharePreviousPercent: OptionalDecimal(party, "andelRosterForegaendeVal"),
                ShareChangePoints: OptionalDecimal(party, "forandringAndelRoster")));
        }

        if (results.Count == 0)
        {
            throw new ElectionResultFormatException("partiRoster är tom – filen innehåller inget resultat.");
        }

        return results;
    }

    private static IReadOnlyList<PartyMandate> ParseMandates(JsonElement mandates)
    {
        var result = new List<PartyMandate>();
        foreach (var party in RequiredArray(mandates, "partiLista").EnumerateArray())
        {
            result.Add(new PartyMandate(
                Code: RequiredString(party, "partiforkortning"),
                Total: RequiredInt(party, "antalMandat"),
                Fixed: OptionalInt(party, "antalFastaMandat") ?? 0,
                Levelling: OptionalInt(party, "antalUtjamningsmandat") ?? 0,
                TotalPrevious: OptionalInt(party, "antalMandatForegaendeVal"),
                Change: OptionalInt(party, "forandringAntalMandat")));
        }

        return result;
    }

    private static OtherPartiesResult ParseOtherParties(JsonElement votes)
    {
        if (!votes.TryGetProperty("rosterOvrigaPartier", out var other)
            || other.ValueKind != JsonValueKind.Object)
        {
            return new OtherPartiesResult(0, 0m, null, null);
        }

        return new OtherPartiesResult(
            Votes: OptionalInt(other, "antalRoster") ?? 0,
            SharePercent: OptionalDecimal(other, "andelRoster") ?? 0m,
            VotesPrevious: OptionalInt(other, "antalRosterForegaendeVal"),
            SharePreviousPercent: OptionalDecimal(other, "andelRosterForegaendeVal"));
    }

    private static CountingStage ParseStage(string value) => value switch
    {
        "preliminär" => CountingStage.Preliminary,
        "slutlig" => CountingStage.Final,
        _ => throw new ElectionResultFormatException($"Okänt räkningstillfälle: {value}."),
    };

    // --- Läshjälpare. Saknade obligatoriska fält ska stoppa importen, inte bli tysta nollor. ---

    private static JsonDocument ParseDocument(Stream json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ElectionResultFormatException($"Filen är inte giltig JSON: {ex.Message}");
        }
    }

    private static JsonElement Required(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null
            ? value
            : throw new ElectionResultFormatException($"Obligatoriskt fält saknas: {name}.");

    private static JsonElement RequiredObject(JsonElement parent, string name)
    {
        var value = Required(parent, name);
        return value.ValueKind == JsonValueKind.Object
            ? value
            : throw new ElectionResultFormatException($"Fältet {name} är inte ett objekt.");
    }

    private static JsonElement RequiredArray(JsonElement parent, string name)
    {
        var value = Required(parent, name);
        return value.ValueKind == JsonValueKind.Array
            ? value
            : throw new ElectionResultFormatException($"Fältet {name} är inte en lista.");
    }

    private static string RequiredString(JsonElement parent, string name) =>
        Required(parent, name).GetString()
        ?? throw new ElectionResultFormatException($"Fältet {name} är inte en sträng.");

    private static string? OptionalString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int RequiredInt(JsonElement parent, string name) =>
        Required(parent, name).TryGetInt32(out var value)
            ? value
            : throw new ElectionResultFormatException($"Fältet {name} är inte ett heltal.");

    private static int? OptionalInt(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static decimal? OptionalDecimal(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var parsed)
            ? parsed
            : null;

    private static bool? OptionalBool(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateOnly RequiredDate(JsonElement parent, string name) =>
        DateOnly.TryParse(RequiredString(parent, name), CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ElectionResultFormatException($"Fältet {name} är inte ett datum.");

    private static DateTimeOffset RequiredTimestamp(JsonElement parent, string name)
    {
        var raw = RequiredString(parent, name);
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            throw new ElectionResultFormatException($"Fältet {name} är inte en tidsstämpel: {raw}.");
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, SwedishTime.GetUtcOffset(unspecified));
    }
}

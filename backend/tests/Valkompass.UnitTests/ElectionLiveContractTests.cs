using System.Text.Json;
using Valkompass.Application.Dtos;
using Valkompass.Application.Election;

namespace Valkompass.UnitTests;

/// <summary>
/// Låser JSON-formen på fasen. Frontend jämför mot exakta strängar, och en fas som inte känns
/// igen där syns inte som ett fel – sidan visar bara fel läge. Buggen som gav upphov till de
/// här testerna: startsidan visade valvake-kortet flera dagar före valdagen, eftersom
/// backend skickade "PreElection" och frontend väntade sig "preElection".
/// </summary>
public class ElectionLiveContractTests
{
    /// <summary>Ska vara identisk med unionen ElectionPhase i frontend/src/lib/election-api.ts.</summary>
    private static readonly Dictionary<ElectionPhase, string> WireNames = new()
    {
        [ElectionPhase.PreElection] = "preElection",
        [ElectionPhase.ElectionDay] = "electionDay",
        [ElectionPhase.WaitingForResults] = "waitingForResults",
        [ElectionPhase.Live] = "live",
        [ElectionPhase.PreliminaryPaused] = "preliminaryPaused",
        [ElectionPhase.FinalCounting] = "finalCounting",
        [ElectionPhase.Finished] = "finished",
    };

    [Fact]
    public void Fasen_serialiseras_som_camelCase()
    {
        foreach (var (phase, expected) in WireNames)
        {
            Assert.Equal(expected, SerializePhase(phase));
        }
    }

    [Fact]
    public void Alla_faser_finns_i_kontraktet()
    {
        // En ny fas i enumen utan motsvarighet i frontend ger samma tysta fel igen.
        Assert.Empty(Enum.GetValues<ElectionPhase>().Except(WireNames.Keys));
    }

    /// <summary>Serialiserar hela svaret med samma inställningar som API:t och plockar ut fasen.</summary>
    private static string? SerializePhase(ElectionPhase phase)
    {
        var response = new ElectionLiveResponse(
            Phase: phase,
            Source: null,
            Reporting: null,
            Results: [],
            OfficialMandates: [],
            Thresholds: new ElectionThresholdsDto(4m, 12m));

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return JsonDocument.Parse(json).RootElement.GetProperty("phase").GetString();
    }
}

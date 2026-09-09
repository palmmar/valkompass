using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Valkompass.Application.Dtos;

namespace Valkompass.IntegrationTests;

/// <summary>
/// Kontraktet för <c>GET /api/election/live</c>. Databasen är tom i testerna, så det här är
/// läget före rösträkningen – det som faktiskt möter besökare fram till valnatten.
/// </summary>
[Collection(ApiCollection.Name)]
public class ElectionLiveApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Live_KravInteAuthentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/election/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Live_UtanSnapshot_GerFasUtanKalla()
    {
        var client = factory.CreateClient();

        var dto = await client.GetFromJsonAsync<ElectionLiveResponse>("/api/election/live");

        Assert.NotNull(dto);
        // Utan importerad data finns ingen källa att beskriva, men fasen är ändå giltig.
        Assert.Null(dto!.Source);
        Assert.Null(dto.Reporting);
        Assert.Empty(dto.Results);
        Assert.Empty(dto.OfficialMandates);
    }

    [Fact]
    public async Task Live_ExponerarSparrarna()
    {
        var client = factory.CreateClient();

        var dto = await client.GetFromJsonAsync<ElectionLiveResponse>("/api/election/live");

        Assert.Equal(4m, dto!.Thresholds.NationalPercent);
        Assert.Equal(12m, dto.Thresholds.ConstituencyPercent);
    }

    [Fact]
    public async Task Live_HarSeparataFaltForResultatOchPrognos()
    {
        var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/election/live"));
        var root = json.RootElement;

        // Räknat resultat och prognos får aldrig blandas ihop – de är olika fält i kontraktet.
        Assert.True(root.TryGetProperty("results", out _));
        Assert.True(root.TryGetProperty("forecast", out var forecast));
        Assert.Equal(JsonValueKind.Null, forecast.ValueKind);

        // Officiella mandat är Valmyndighetens, inte vår egen räkning, och står för sig.
        Assert.True(root.TryGetProperty("officialMandates", out _));
    }

    [Fact]
    public async Task Live_SerialiserarFasenSomText()
    {
        var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/election/live"));
        var phase = json.RootElement.GetProperty("phase");

        // Frontend ska kunna jämföra mot ett namn, inte mot ett heltal.
        Assert.Equal(JsonValueKind.String, phase.ValueKind);
        Assert.NotEmpty(phase.GetString()!);
    }

    [Fact]
    public async Task Live_SatterKortCacheHeader()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/election/live");

        // Endpointen pollas tätt av många klienter under valnatten.
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Public);
    }
}

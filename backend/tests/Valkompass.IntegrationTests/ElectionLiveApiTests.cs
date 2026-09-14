using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Valkompass.Application.Contracts;
using Valkompass.Application.Dtos;
using Valkompass.Application.Election;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Persistence;

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

        // Och namnet ska komma ut i camelCase som resten av API:t – "preElection", inte
        // "PreElection". Fel skalform ser inte ut som ett fel i frontend, den visar bara fel
        // läge. Exakt sträng per fas låses i ElectionLiveContractTests.
        var name = phase.GetString()!;
        Assert.NotEmpty(name);
        Assert.True(char.IsLower(name[0]), $"Fasen serialiserades som \"{name}\".");
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

    [Fact]
    public async Task Live_GerPartifargFranVarEgenPalett()
    {
        await WithSnapshotAsync(async () =>
        {
            var dto = await factory.CreateClient().GetFromJsonAsync<ElectionLiveResponse>("/api/election/live");

            // Valmyndighetens fargkod för S i snapshoten är "#FF0000", men sidan ska visa samma
            // röd som barometern och resultatsidan – annars byter partierna färg mellan våra
            // egna vyer. Färgen kommer därför från partitabellen.
            var social = Assert.Single(dto!.Results, r => r.PartyCode == "S");
            Assert.Equal("#E8112D", social.Color);
        });
    }

    [Fact]
    public async Task Live_FallerTillbakaPaValmyndighetensFargForOkantParti()
    {
        await WithSnapshotAsync(async () =>
        {
            var dto = await factory.CreateClient().GetFromJsonAsync<ElectionLiveResponse>("/api/election/live");

            // Ett parti som tar sig in i riksdagen men inte finns i kompassen saknar färg hos
            // oss. Då är Valmyndighetens egen bättre än ingen alls.
            var newcomer = Assert.Single(dto!.Results, r => r.PartyCode == "NY");
            Assert.Equal("#123456", newcomer.Color);
        });
    }

    /// <summary>
    /// Kör testet mot en sparad snapshot och städar efter sig: tabellen töms och minnescachen
    /// rensas, så att de andra testerna i kollektionen ser läget före rösträkningen igen.
    /// </summary>
    private async Task WithSnapshotAsync(Func<Task> test)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IElectionSnapshotStore>();

        ClearLiveCache();
        await db.ElectionSnapshots.ExecuteDeleteAsync();
        await store.SaveAsync(Snapshot(), forecast: null);
        try
        {
            await test();
        }
        finally
        {
            await db.ElectionSnapshots.ExecuteDeleteAsync();
            ClearLiveCache();
        }
    }

    /// <summary>
    /// Svaret cachas några sekunder. Utan en rensning skulle ett test antingen läsa
    /// föregående tests tomma läge eller lämna kvar sin snapshot åt nästa.
    /// </summary>
    private void ClearLiveCache()
    {
        if (factory.Services.GetRequiredService<IMemoryCache>() is MemoryCache cache)
        {
            cache.Clear();
        }
    }

    private static ElectionSnapshot Snapshot() => new(
        Source: new ElectionSnapshotSource(
            ElectionDate: new DateOnly(2026, 9, 13),
            PreviousElectionDate: new DateOnly(2022, 9, 11),
            Stage: CountingStage.Preliminary,
            IsTest: false,
            UpdatedAt: new DateTimeOffset(2026, 9, 13, 20, 38, 57, TimeSpan.FromHours(2)),
            IngestedAt: new DateTimeOffset(2026, 9, 13, 20, 39, 17, TimeSpan.FromHours(2)),
            Checksum: Guid.NewGuid().ToString("n"),
            UpdateCount: 3),
        Reporting: new ElectionReporting(
            DistrictsReported: 3,
            DistrictsTotal: 6626,
            EligibleVotersCovered: 1231,
            EligibleVotersTotal: 8_051_238,
            TotalVotes: 1046,
            TotalVotesPrevious: 6_547_801,
            TurnoutPercent: 85.0m,
            TurnoutPercentPrevious: 84.2m),
        Results:
        [
            // Valmyndighetens fargkod, medvetet en annan röd än vår egen.
            new PartyResult("S", "Arbetarepartiet-Socialdemokraterna", 5, "#FF0000", 700, 70.0m, null, null, null),
            new PartyResult("NY", "Nya partiet", 9, "#123456", 300, 30.0m, null, null, null),
        ],
        Mandates: [],
        OtherParties: new OtherPartiesResult(2, 0.2m, null, null),
        ThresholdPercent: 4m,
        ConstituencyThresholdPercent: 12m);
}

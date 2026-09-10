using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
using Valkompass.Application.Election.Nowcast;
using Valkompass.Domain.Enums;
using Valkompass.Infrastructure.Services;

namespace Valkompass.UnitTests;

/// <summary>
/// Kör hela kedjan index.md5 → nedladdning → checksummakontroll → parsning → lagring mot den
/// arkiverade genrepsfilen. Ingen nätverkstrafik: HTTP-lagret är utbytt mot en fejk.
/// </summary>
public class ElectionResultImporterTests
{
    private const string BaseUrl = "https://resultat.val.se/resultatfiler/genrep2026/";
    private const string RdPath = "./p/rd/Genrep_2026_preliminar_00_RD.zip";
    private const string RdChecksum = "3e6407d8abce4a7856a396cd61e02724";
    private static readonly string Index = $"{RdChecksum}  {RdPath}\n";

    [Fact]
    public async Task Importerar_ny_checksumma()
    {
        var store = new FakeStore();
        var importer = Build(store, Index, allowTestData: true, out _);

        var outcome = await importer.ImportAsync();

        Assert.Equal(ImportOutcome.Imported, outcome);
        var saved = Assert.Single(store.Saved);
        Assert.Equal(RdChecksum, saved.Source.Checksum);
        Assert.Equal(6626, saved.Reporting.DistrictsTotal);
        Assert.Equal(349, saved.Mandates.Sum(m => m.Total));
    }

    [Fact]
    public async Task Oforandrad_checksumma_laddar_inte_ned_zip_filen()
    {
        var store = new FakeStore { LatestChecksum = RdChecksum };
        var importer = Build(store, Index, allowTestData: true, out var handler);

        var outcome = await importer.ImportAsync();

        Assert.Equal(ImportOutcome.Unchanged, outcome);
        Assert.Empty(store.Saved);
        // Bara indexet hämtades – hela poängen med index.md5.
        Assert.Equal(new[] { "index.md5" }, handler.RequestedPaths.ToArray());
    }

    [Fact]
    public async Task Testdata_avvisas_nar_den_inte_ar_tillaten()
    {
        // Genrepsfilen är märkt test:true. I produktionskonfiguration får den inte sparas.
        var store = new FakeStore();
        var importer = Build(store, Index, allowTestData: false, out _);

        var outcome = await importer.ImportAsync();

        Assert.Equal(ImportOutcome.RejectedTestData, outcome);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Tomt_index_ar_inte_ett_fel()
    {
        // Så ser val2026/index.md5 ut ända fram till valnatten.
        var store = new FakeStore();
        var importer = Build(store, "d41d8cd98f00b204e9800998ecf8427e  -\n", allowTestData: true, out _);

        var outcome = await importer.ImportAsync();

        Assert.Equal(ImportOutcome.NoResultsPublished, outcome);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Fel_checksumma_avbryter_importen()
    {
        // Avbruten eller manipulerad nedladdning: filen matchar inte indexet.
        var store = new FakeStore();
        var importer = Build(store, $"00000000000000000000000000000000  {RdPath}\n", allowTestData: true, out _);

        var ex = await Assert.ThrowsAsync<ElectionResultFormatException>(() => importer.ImportAsync());

        Assert.Contains("Checksumman", ex.Message, StringComparison.Ordinal);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Serverfel_bubblar_upp_sa_att_backoff_kan_ta_over()
    {
        var store = new FakeStore();
        var handler = new FakeHandler(Index, ZipBytes()) { ZipStatusCode = HttpStatusCode.ServiceUnavailable };
        var importer = Build(store, handler, allowTestData: true);

        await Assert.ThrowsAsync<HttpRequestException>(() => importer.ImportAsync());

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Trasig_zip_forstor_inte_foregaende_snapshot()
    {
        var store = new FakeStore { LatestChecksum = "en-tidigare-summa" };
        var handler = new FakeHandler(Index, "det här är ingen zip"u8.ToArray());
        var importer = Build(store, handler, allowTestData: true);

        await Assert.ThrowsAnyAsync<Exception>(() => importer.ImportAsync());

        Assert.Empty(store.Saved);
        Assert.Equal("en-tidigare-summa", store.LatestChecksum);
    }

    [Fact]
    public async Task Ratt_checksumma_men_manipulerat_innehall_avvisas_av_signaturen()
    {
        // Det här är vad signaturen tillför utöver md5-kontrollen: en fil som stämmer mot
        // indexet men vars innehåll inte är det Valmyndigheten signerade.
        var tampered = TamperedArchive();
        var checksum = Convert.ToHexString(MD5.HashData(tampered)).ToLowerInvariant();
        var store = new FakeStore();
        var importer = Build(store, new FakeHandler($"{checksum}  {RdPath}", tampered), allowTestData: true);

        var ex = await Assert.ThrowsAsync<ElectionResultFormatException>(() => importer.ImportAsync());

        Assert.Contains("Signaturen", ex.Message, StringComparison.Ordinal);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Ingen_prognos_sparas_nar_den_ar_avstangd()
    {
        // Standardläget: valvakan visar räknat resultat och inget annat.
        var store = new FakeStore();
        var importer = Build(store, Index, allowTestData: true, out _);

        await importer.ImportAsync();

        Assert.Null(Assert.Single(store.SavedForecasts));
    }

    [Fact]
    public async Task Prognosmodellen_kors_nar_den_slas_pa()
    {
        var store = new FakeStore();
        var importer = Build(store, new FakeHandler(Index, ZipBytes()), allowTestData: true, forecast: true);

        await importer.ImportAsync();

        // Modellen körs och dess svar sparas tillsammans med snapshotet. Genrepsfilen är
        // färdigräknad, och då är svaret att avstå – det finns inga orapporterade valdistrikt
        // kvar att uppskatta. Att avstå är ett giltigt svar och ska sparas som ett sådant.
        var forecast = Assert.Single(store.SavedForecasts);
        Assert.NotNull(forecast);
        Assert.False(forecast!.Available);
        Assert.Contains("inget kvar", forecast.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bygger om arkivet med ett blanksteg tillagt i mandatfördelningsfilen. JSON:en är
    /// fortfarande giltig, så det är signaturen och inget annat som fäller den.
    /// </summary>
    private static byte[] TamperedArchive()
    {
        var output = new MemoryStream();
        using (var source = new ZipArchive(new MemoryStream(ZipBytes()), ZipArchiveMode.Read))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                using var reader = entry.Open();
                using var buffer = new MemoryStream();
                reader.CopyTo(buffer);

                var bytes = buffer.ToArray();
                if (entry.Name.Contains("mandatfordelning", StringComparison.Ordinal)
                    && entry.Name.EndsWith(".json", StringComparison.Ordinal))
                {
                    bytes = [.. bytes, (byte)' '];
                }

                using var writer = target.CreateEntry(entry.FullName).Open();
                writer.Write(bytes);
            }
        }

        return output.ToArray();
    }

    // --- Uppsättning ---

    private static ElectionResultImporter Build(
        FakeStore store,
        string index,
        bool allowTestData,
        out FakeHandler handler)
    {
        handler = new FakeHandler(index, ZipBytes());
        return Build(store, handler, allowTestData);
    }

    private static ElectionResultImporter Build(
        FakeStore store,
        FakeHandler handler,
        bool allowTestData,
        bool forecast = false)
    {
        var options = Options.Create(new ElectionImport
        {
            BaseUrl = BaseUrl,
            AllowTestData = allowTestData,
            Forecast = forecast,
        });

        return new ElectionResultImporter(
            new HttpClient(handler),
            store,
            Verifier(),
            options,
            TimeProvider.System,
            NullLogger<ElectionResultImporter>.Instance);
    }

    private static ElectionSignatureVerifier Verifier() => new(
        ElectionFixtures.CertificateHttpClientFactory(out _),
        Options.Create(new ElectionImport()),
        TimeProvider.System,
        NullLogger<ElectionSignatureVerifier>.Instance);

    private static byte[] ZipBytes() => ElectionFixtures.ResultArchiveBytes();

    private sealed class FakeHandler(string index, byte[] zip) : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = [];

        public HttpStatusCode ZipStatusCode { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            RequestedPaths.Add(path[(path.LastIndexOf('/') + 1)..]);

            if (path.EndsWith("index.md5", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(index),
                });
            }

            return Task.FromResult(new HttpResponseMessage(ZipStatusCode)
            {
                Content = new ByteArrayContent(zip),
            });
        }
    }

    private sealed class FakeStore : IElectionSnapshotStore
    {
        public List<ElectionSnapshot> Saved { get; } = [];

        public string? LatestChecksum { get; set; }

        public List<NowcastResult?> SavedForecasts { get; } = [];

        public Task<bool> SaveAsync(
            ElectionSnapshot snapshot,
            NowcastResult? forecast,
            CancellationToken ct = default)
        {
            Saved.Add(snapshot);
            SavedForecasts.Add(forecast);
            LatestChecksum = snapshot.Source.Checksum;
            return Task.FromResult(true);
        }

        public Task<StoredElectionSnapshot?> GetLatestAsync(
            CountingStage stage, bool includeTestData, CancellationToken ct = default) =>
            Task.FromResult(Saved.Count == 0
                ? null
                : new StoredElectionSnapshot(Saved[^1], SavedForecasts[^1]));

        public Task<string?> GetLatestChecksumAsync(CountingStage stage, CancellationToken ct = default) =>
            Task.FromResult(LatestChecksum);
    }
}

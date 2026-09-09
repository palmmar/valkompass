using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Valkompass.Application.Contracts;
using Valkompass.Application.Election;
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

    private static ElectionResultImporter Build(FakeStore store, FakeHandler handler, bool allowTestData)
    {
        var options = Options.Create(new ElectionImport
        {
            BaseUrl = BaseUrl,
            AllowTestData = allowTestData,
        });

        return new ElectionResultImporter(
            new HttpClient(handler),
            store,
            options,
            TimeProvider.System,
            NullLogger<ElectionResultImporter>.Instance);
    }

    private static byte[] ZipBytes() => File.ReadAllBytes(Path.Combine(
        FixtureRoot(), "valmyndigheten", "genrep2026", "Genrep_2026_preliminar_00_RD.zip"));

    private static string FixtureRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Hittade ingen fixtures-katalog uppåt från {AppContext.BaseDirectory}.");
    }

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

        public Task<bool> SaveAsync(ElectionSnapshot snapshot, CancellationToken ct = default)
        {
            Saved.Add(snapshot);
            LatestChecksum = snapshot.Source.Checksum;
            return Task.FromResult(true);
        }

        public Task<ElectionSnapshot?> GetLatestAsync(
            CountingStage stage, bool includeTestData, CancellationToken ct = default) =>
            Task.FromResult<ElectionSnapshot?>(Saved.LastOrDefault());

        public Task<string?> GetLatestChecksumAsync(CountingStage stage, CancellationToken ct = default) =>
            Task.FromResult(LatestChecksum);
    }
}

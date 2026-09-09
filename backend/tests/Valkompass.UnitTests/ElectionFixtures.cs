using System.IO.Compression;
using System.Net;

namespace Valkompass.UnitTests;

/// <summary>
/// Delade hjälpare för de arkiverade filerna i <c>backend/tests/fixtures/valmyndigheten/</c>.
/// Genrepet är avslutat och filerna kan försvinna från val.se, så inget test rör nätverket.
/// </summary>
internal static class ElectionFixtures
{
    public const string ResultChecksum = "3e6407d8abce4a7856a396cd61e02724";

    public static byte[] ResultArchiveBytes() => File.ReadAllBytes(ArchivePath());

    /// <summary>Certifikatet som val.se serverar på <c>/keys/val-sign-crt.pem</c>.</summary>
    public static string CertificatePem() => File.ReadAllText(Path.Combine(Root(), "val-sign-crt.pem"));

    /// <summary>Mandatfördelningsfilen och dess signatur, som de ligger i ZIP-arkivet.</summary>
    public static (byte[] Content, byte[] Signature) MandateFileWithSignature() =>
        (ReadZipEntry("mandatfordelning_00_RD.json"), ReadZipEntry("mandatfordelning_00_RD_sign.sha256"));

    /// <summary>Läser den post vars namn slutar på <paramref name="suffix"/>.</summary>
    public static byte[] ReadZipEntry(string suffix)
    {
        using var archive = ZipFile.OpenRead(ArchivePath());
        var entry = archive.Entries.Single(e => e.Name.EndsWith(suffix, StringComparison.Ordinal));

        using var buffer = new MemoryStream();
        using (var stream = entry.Open())
        {
            stream.CopyTo(buffer);
        }

        return buffer.ToArray();
    }

    /// <summary>En <see cref="IHttpClientFactory"/> som bara svarar med certifikatet.</summary>
    public static IHttpClientFactory CertificateHttpClientFactory(out CountingHandler handler)
    {
        handler = new CountingHandler(CertificatePem());
        return new SingleHandlerFactory(handler);
    }

    private static string ArchivePath() =>
        Path.Combine(Root(), "genrep2026", "Genrep_2026_preliminar_00_RD.zip");

    /// <summary>Letar upp fixtures-katalogen uppåt från testassemblyn.</summary>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "fixtures", "valmyndigheten");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Hittade ingen fixtures-katalog uppåt från {AppContext.BaseDirectory}.");
    }

    internal sealed class CountingHandler(string pem) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(pem),
            });
        }
    }

    private sealed class SingleHandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

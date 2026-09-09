using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Valkompass.Application.Election;
using Valkompass.Infrastructure.Services;

namespace Valkompass.UnitTests;

/// <summary>
/// Verifierar mot Valmyndighetens riktiga signaturer i den arkiverade genrepsfilen.
/// Certifikatet serveras från en fixture i stället för från val.se – testerna ska inte
/// behöva nätverk.
/// </summary>
public class ElectionSignatureVerifierTests
{
    [Fact]
    public async Task Verifierar_genrepets_signatur()
    {
        var (content, signature) = ElectionFixtures.MandateFileWithSignature();

        // Kastar inte.
        await Build().VerifyAsync(content, signature, "mandatfordelning.json");
    }

    [Fact]
    public async Task Avvisar_manipulerad_fil()
    {
        var (content, signature) = ElectionFixtures.MandateFileWithSignature();
        content[^2] ^= 0xFF;

        var ex = await Assert.ThrowsAsync<ElectionResultFormatException>(
            () => Build().VerifyAsync(content, signature, "mandatfordelning.json"));

        Assert.Contains("Signaturen", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Avvisar_signatur_som_hor_till_en_annan_fil()
    {
        var (content, _) = ElectionFixtures.MandateFileWithSignature();
        var otherSignature = ElectionFixtures.ReadZipEntry("summering_RD_sign.sha256");

        await Assert.ThrowsAsync<ElectionResultFormatException>(
            () => Build().VerifyAsync(content, otherSignature, "mandatfordelning.json"));
    }

    [Fact]
    public async Task Hoppar_over_verifiering_nar_den_ar_avstangd()
    {
        // Nödutgången: hellre visa resultat med bara checksummekontroll än ingenting alls.
        var (content, signature) = ElectionFixtures.MandateFileWithSignature();
        content[0] ^= 0xFF;

        await Build(verifySignatures: false).VerifyAsync(content, signature, "mandatfordelning.json");
    }

    [Fact]
    public async Task Hamtar_certifikatet_en_gang_och_ateranvander_det()
    {
        var factory = ElectionFixtures.CertificateHttpClientFactory(out var handler);
        var verifier = Build(factory: factory);
        var (content, signature) = ElectionFixtures.MandateFileWithSignature();

        await verifier.VerifyAsync(content, signature, "a.json");
        await verifier.VerifyAsync(content, signature, "b.json");
        await verifier.VerifyAsync(content, signature, "c.json");

        Assert.Equal(1, handler.RequestCount);
    }

    private static ElectionSignatureVerifier Build(
        bool verifySignatures = true,
        IHttpClientFactory? factory = null)
    {
        var options = Options.Create(new ElectionImport
        {
            VerifySignatures = verifySignatures,
            CertificateUrl = "https://resultat.val.se/keys/val-sign-crt.pem",
        });

        return new ElectionSignatureVerifier(
            factory ?? ElectionFixtures.CertificateHttpClientFactory(out _),
            options,
            TimeProvider.System,
            NullLogger<ElectionSignatureVerifier>.Instance);
    }
}

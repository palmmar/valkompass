using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valkompass.Application.Election;

namespace Valkompass.Infrastructure.Services;

/// <summary>
/// Verifierar Valmyndighetens signaturfiler (<c>*_sign.sha256</c>): en RSA-signatur över
/// resultatfilens bytes, gjord med certifikatet på
/// <c>https://resultat.val.se/keys/val-sign-crt.pem</c>.
/// </summary>
/// <remarks>
/// Certifikatet hämtas från val.se och cachas för processens livstid. Notera att det kommer
/// från samma värd som resultatfilerna: kontrollen fångar en trasig eller ofullständig fil,
/// men skyddar inte mot någon som kontrollerar val.se. Certifikatets utfärdare och
/// fingeravtryck loggas när det hämtas, så att ett oväntat byte syns i efterhand.
/// </remarks>
public class ElectionSignatureVerifier(
    IHttpClientFactory httpClientFactory,
    IOptions<ElectionImport> options,
    TimeProvider time,
    ILogger<ElectionSignatureVerifier> logger)
{
    private readonly ElectionImport _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RSA? _publicKey;

    /// <summary>
    /// Kastar <see cref="ElectionResultFormatException"/> om signaturen inte stämmer. Gör
    /// ingenting när verifiering är avstängd.
    /// </summary>
    public async Task VerifyAsync(
        byte[] content,
        byte[] signature,
        string fileName,
        CancellationToken ct = default)
    {
        if (!_options.VerifySignatures)
        {
            return;
        }

        var key = await GetPublicKeyAsync(ct);
        var valid = key.VerifyData(content, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (!valid)
        {
            throw new ElectionResultFormatException(
                $"Signaturen för {fileName} kunde inte verifieras mot Valmyndighetens certifikat.");
        }
    }

    private async Task<RSA> GetPublicKeyAsync(CancellationToken ct)
    {
        if (_publicKey is not null)
        {
            return _publicKey;
        }

        await _gate.WaitAsync(ct);
        try
        {
            // Dubbelkoll: en annan tråd kan ha hunnit hämta certifikatet under väntan.
            if (_publicKey is not null)
            {
                return _publicKey;
            }

            using var http = httpClientFactory.CreateClient();
            http.Timeout = _options.RequestTimeout;
            var pem = await http.GetStringAsync(_options.CertificateUrl, ct);

            using var certificate = X509Certificate2.CreateFromPem(pem);
            LogCertificate(certificate);

            _publicKey = certificate.GetRSAPublicKey()
                ?? throw new ElectionResultFormatException(
                    "Valmyndighetens certifikat innehåller ingen RSA-nyckel.");

            return _publicKey;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void LogCertificate(X509Certificate2 certificate)
    {
        logger.LogInformation(
            "Hämtade signeringscertifikat från {Url}: {Subject}, utfärdat av {Issuer}, "
            + "giltigt {NotBefore:yyyy-MM-dd}–{NotAfter:yyyy-MM-dd}, SHA256 {Thumbprint}.",
            _options.CertificateUrl,
            certificate.Subject,
            certificate.Issuer,
            certificate.NotBefore,
            certificate.NotAfter,
            certificate.GetCertHashString(HashAlgorithmName.SHA256));

        // Ett utgånget certifikat stoppar inte verifieringen – signaturen kan fortfarande vara
        // äkta – men det är värt att märka, särskilt mitt i en valnatt.
        var now = time.GetUtcNow().UtcDateTime;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            logger.LogWarning(
                "Signeringscertifikatet är utanför sin giltighetstid ({NotBefore:u}–{NotAfter:u}).",
                certificate.NotBefore.ToUniversalTime(),
                certificate.NotAfter.ToUniversalTime());
        }
    }
}

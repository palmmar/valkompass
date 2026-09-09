using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valkompass.Application.Election;

namespace Valkompass.Infrastructure.Services;

/// <summary>
/// Pollar Valmyndighetens index och importerar nya resultatfiler under rösträkningen.
/// </summary>
/// <remarks>
/// Endast backend pratar med Valmyndigheten: hur många som än läser vårt API blir det aldrig
/// mer än ett anrop per intervall uppströms. Fel loggas och leder till exponentiell backoff –
/// senast sparade snapshot ligger kvar och fortsätter serveras under tiden.
/// </remarks>
public class ElectionImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ElectionImport> options,
    ILogger<ElectionImportBackgroundService> logger)
    : BackgroundService
{
    private readonly ElectionImport _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Valresultatimporten är avstängd (ElectionImport:Enabled = false).");
            return;
        }

        logger.LogInformation(
            "Startar valresultatimport mot {BaseUrl} var {Interval}. Testdata tillåten: {AllowTestData}.",
            _options.BaseUrl, _options.PollInterval, _options.AllowTestData);

        var retryDelay = _options.InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var importer = scope.ServiceProvider.GetRequiredService<ElectionResultImporter>();

                await importer.ImportAsync(ct: stoppingToken);

                retryDelay = _options.InitialRetryDelay;
                delay = _options.PollInterval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal nedstängning.
                break;
            }
            catch (Exception ex)
            {
                // Allt fångas medvetet: en trasig fil, en timeout eller ett 5xx från
                // Valmyndigheten får inte döda importen för resten av valnatten.
                logger.LogError(ex, "Importen misslyckades. Försöker igen om {Delay}.", retryDelay);
                delay = retryDelay;
                retryDelay = Min(retryDelay * 2, _options.MaxRetryDelay);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Valresultatimporten stoppad.");
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}

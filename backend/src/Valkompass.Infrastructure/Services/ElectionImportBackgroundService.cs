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
/// Tjänsten startar av sig själv strax före vallokalerna stänger och håller på genom onsdagens
/// uppsamlingsräkning och den slutliga räkningen – ingen behöver slå på den, och framför allt
/// behöver ingen starta om servern mitt under rösträkningen. När fönstret ska öppna och stänga
/// bestäms av <see cref="ElectionImportSchedule"/>.
///
/// Endast backend pratar med Valmyndigheten: hur många som än läser vårt API blir det aldrig
/// mer än ett anrop per pollintervall uppströms. Fel loggas och leder till exponentiell backoff –
/// senast sparade snapshot ligger kvar och fortsätter serveras under tiden.
/// </remarks>
public class ElectionImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ElectionImport> importOptions,
    IOptions<ElectionTimeline> timeline,
    TimeProvider time,
    ILogger<ElectionImportBackgroundService> logger)
    : BackgroundService
{
    private readonly ElectionTimeline _timeline = timeline.Value;

    /// <summary>Så att en pausad import inte loggar samma rad varje pollintervall.</summary>
    private bool _pauseLogged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Valresultatimport igång mot {BaseUrl}. Fönster {Start:u} till {End:u}, intervall {Interval}.",
            importOptions.CurrentValue.BaseUrl,
            ElectionImportSchedule.WindowStart(_timeline),
            ElectionImportSchedule.WindowEnd(_timeline),
            importOptions.CurrentValue.PollInterval);

        var retryDelay = importOptions.CurrentValue.InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Läses om varje varv, så att en ändrad flagga slår igenom utan omstart.
            var options = importOptions.CurrentValue;
            var decision = ElectionImportSchedule.Decide(_timeline, options, time.GetUtcNow());

            if (decision.Action == ImportAction.Finished)
            {
                logger.LogInformation("Importfönstret är passerat. Avslutar importen.");
                break;
            }

            var delay = decision.Delay;

            if (decision.Action == ImportAction.Paused)
            {
                if (!_pauseLogged)
                {
                    logger.LogWarning(
                        "Importen är pausad (ElectionImport:Enabled = false). Senast sparade "
                        + "snapshot fortsätter serveras.");
                    _pauseLogged = true;
                }
            }
            else if (decision.Action == ImportAction.Import)
            {
                _pauseLogged = false;

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var importer = scope.ServiceProvider.GetRequiredService<ElectionResultImporter>();

                    await importer.ImportAsync(ct: stoppingToken);

                    retryDelay = options.InitialRetryDelay;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Allt fångas medvetet: en trasig fil, en timeout eller ett 5xx från
                    // Valmyndigheten får inte döda importen för resten av valnatten.
                    logger.LogError(ex, "Importen misslyckades. Försöker igen om {Delay}.", retryDelay);
                    delay = retryDelay;
                    retryDelay = Min(retryDelay * 2, options.MaxRetryDelay);
                }
            }

            try
            {
                await Task.Delay(delay, time, stoppingToken);
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

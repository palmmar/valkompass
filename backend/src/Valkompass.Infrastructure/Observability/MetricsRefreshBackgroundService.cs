using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Valkompass.Application.Election;
using Valkompass.Infrastructure.Persistence;

namespace Valkompass.Infrastructure.Observability;

/// <summary>
/// Läser med jämna mellanrum de värden som måste överleva en omstart – totalt antal sparade
/// kompasser och valvakans senaste snapshot – och lämnar dem till <see cref="ValkompassMetrics"/>.
/// </summary>
/// <remarks>
/// Varför en bakgrundstjänst och inte en databasfråga per skrapning: observerbara mätare har
/// synkrona callbacks, och en skrapning ska aldrig kunna blockera på databasen. Intervallet är
/// kortare än Prometheus normala skrapintervall, så siffrorna hinner aldrig bli gamla i grafen.
///
/// Ett databasfel loggas och lämnar föregående ögonblicksbild orörd – hellre ett värde som står
/// still en stund än en graf som hoppar ned till noll för att databasen tillfälligt strulade.
/// </remarks>
public class MetricsRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ValkompassMetrics metrics,
    IOptionsMonitor<ElectionImport> importOptions,
    IOptions<MetricsOptions> metricsOptions,
    TimeProvider time,
    ILogger<MetricsRefreshBackgroundService> logger)
    : BackgroundService
{
    /// <summary>Så att en nere databas inte loggar samma rad varje uppdateringsvarv.</summary>
    private bool _failureLogged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(metricsOptions.Value.EffectiveRefreshInterval, time);

        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var sessions = await db.QuizSessions.CountAsync(ct);

            // Samma urval som ElectionLiveService gör: slutlig räkning går före preliminär, och
            // genrepsdata räknas bara när den uttryckligen är tillåten. Mätvärdet ska beskriva
            // det sidan faktiskt visar, inte vad som råkar ligga i tabellen.
            var allowTestData = importOptions.CurrentValue.AllowTestData;
            var election = await db.ElectionSnapshots
                .AsNoTracking()
                .Where(e => allowTestData || !e.IsTest)
                .OrderByDescending(e => e.Stage)
                .ThenByDescending(e => e.IngestedAt)
                .ThenByDescending(e => e.Id)
                .Select(e => new ElectionSnapshotMetrics(
                    e.IngestedAt, e.SourceUpdatedAt, e.DistrictsReported, e.DistrictsTotal))
                .FirstOrDefaultAsync(ct);

            metrics.UpdateDatabaseSnapshot(new MetricsSnapshot(sessions, election));
            _failureLogged = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nedstängning, inget att rapportera.
        }
        catch (Exception ex)
        {
            if (!_failureLogged)
            {
                logger.LogWarning(ex, "Kunde inte uppdatera mätvärden ur databasen. Behåller föregående värden.");
                _failureLogged = true;
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

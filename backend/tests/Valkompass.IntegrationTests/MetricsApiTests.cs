using System.Globalization;
using System.Net.Http.Json;
using Valkompass.Application.Dtos;

namespace Valkompass.IntegrationTests;

/// <summary>
/// Kontrollerar att /metrics faktiskt serverar det Prometheus ska skrapa.
/// </summary>
/// <remarks>
/// Testerna delar värd med övriga integrationstester, så räknarna är kumulativa över hela
/// testkörningen. Därför jämförs alltid mot ett tidigare avläst värde i stället för mot ett
/// exakt tal – ett test som kräver "exakt 1" hade gått sönder så fort ett annat test skickade
/// in ett quiz.
/// </remarks>
[Collection(ApiCollection.Name)]
public class MetricsApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Metrics_ServesPrometheusExposition()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType!.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        // ASP.NET Cores inbyggda HTTP-mätvärden: grunden för trafik- och latenspanelerna.
        Assert.Contains("http_server_request_duration_seconds", body);
    }

    [Fact]
    public async Task QuizStart_IncrementsStartedCounter()
    {
        var client = factory.CreateClient();
        var before = Sum(await ScrapeAsync(client), "valkompass_quiz_started_total");

        var started = await client.PostAsJsonAsync("/api/quiz/start", new StartQuizRequest(25));
        started.EnsureSuccessStatusCode();

        var scrape = await ScrapeUntilAsync(
            client, body => Sum(body, "valkompass_quiz_started_total") > before);

        Assert.True(
            Sum(scrape, "valkompass_quiz_started_total") > before,
            $"valkompass_quiz_started_total ökade inte. Skrapning:\n{scrape}");
        // Läge och variant måste följa med, annars går funneln inte att bryta ned i Grafana.
        Assert.Contains("mode=\"25\"", scrape);
        Assert.Contains("variant=\"standard\"", scrape);
    }

    [Fact]
    public async Task SubmittedQuiz_IncrementsCompletedCounterAndStoredGauge()
    {
        var client = factory.CreateClient();
        var questionnaire = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire?mode=25");
        var before = Sum(await ScrapeAsync(client), "valkompass_quiz_completed_total");

        var request = new SubmitQuizRequest(
            [.. questionnaire!.Questions.Select(q => new SubmitAnswerDto(q.Id, 3, false, false))],
            Simplified: false,
            Mode: 25);
        var submit = await client.PostAsJsonAsync("/api/quiz/results", request);
        submit.EnsureSuccessStatusCode();

        var scrape = await ScrapeUntilAsync(
            client, body => Sum(body, "valkompass_quiz_completed_total") > before);
        Assert.True(
            Sum(scrape, "valkompass_quiz_completed_total") > before,
            $"valkompass_quiz_completed_total ökade inte. Skrapning:\n{scrape}");

        // Mätaren läses ur databasen av MetricsRefreshBackgroundService och överlever därför en
        // omstart, till skillnad från räknaren ovan. Den kan släpa ett uppdateringsvarv.
        var withGauge = await ScrapeUntilAsync(
            client, body => Sum(body, "valkompass_quiz_sessions_stored") >= 1);
        Assert.True(
            Sum(withGauge, "valkompass_quiz_sessions_stored") >= 1,
            $"valkompass_quiz_sessions_stored saknas eller är noll. Skrapning:\n{withGauge}");
    }

    private static async Task<string> ScrapeAsync(HttpClient client)
    {
        var response = await client.GetAsync("/metrics");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Skrapar tills villkoret uppfylls eller tiden går ut. Exportören cachar svaret en kort
    /// stund, och de databasgrundade mätarna uppdateras av en bakgrundstjänst – båda gör att ett
    /// värde kan dröja en tick. Returnerar sista skrapningen så att en miss går att läsa.
    /// </summary>
    private static async Task<string> ScrapeUntilAsync(HttpClient client, Func<string, bool> until)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        string scrape;
        do
        {
            scrape = await ScrapeAsync(client);
            if (until(scrape))
            {
                return scrape;
            }

            await Task.Delay(250);
        }
        while (DateTime.UtcNow < deadline);

        return scrape;
    }

    /// <summary>
    /// Summerar alla tidsserier för ett mätvärde, oavsett labels. En rad ser ut så här:
    /// <c>valkompass_quiz_started_total{mode="25",variant="standard"} 3</c>, ibland med en
    /// tidsstämpel efter värdet.
    /// </summary>
    private static double Sum(string scrape, string metric)
    {
        var total = 0d;

        foreach (var raw in scrape.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#') || !line.StartsWith(metric, StringComparison.Ordinal))
            {
                continue;
            }

            // Namnet måste matcha helt, annars skulle "..._total" fånga "..._total_annat".
            var rest = line[metric.Length..];
            if (rest.StartsWith('{'))
            {
                // Hoppa över labelblocket: ett labelvärde får innehålla mellanslag.
                var end = rest.IndexOf('}');
                if (end < 0)
                {
                    continue;
                }

                rest = rest[(end + 1)..];
            }
            else if (!rest.StartsWith(' '))
            {
                continue;
            }

            // Kvar: " <värde>" eller " <värde> <tidsstämpel>".
            var value = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                total += parsed;
            }
        }

        return total;
    }
}

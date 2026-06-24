using System.Net;
using System.Net.Http.Json;
using Valkompass.Application.Dtos;

namespace Valkompass.IntegrationTests;

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Questionnaire_ReturnsSeededContent()
    {
        var client = factory.CreateClient();

        var dto = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire");

        Assert.NotNull(dto);
        Assert.Equal(75, dto!.Questions.Count);
        Assert.Equal(11, dto.Categories.Count);
    }

    [Fact]
    public async Task Questionnaire_ModesAreNestedSubsets()
    {
        var client = factory.CreateClient();

        var snabb = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire?mode=25");
        var standard = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire?mode=50");
        var full = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire?mode=75");

        Assert.Equal(25, snabb!.Questions.Count);
        Assert.Equal(50, standard!.Questions.Count);
        Assert.Equal(75, full!.Questions.Count);

        // Lägena är nästlade: 25 ⊂ 50 ⊂ 75.
        var standardIds = standard.Questions.Select(q => q.Id).ToHashSet();
        var fullIds = full.Questions.Select(q => q.Id).ToHashSet();
        Assert.All(snabb.Questions, q => Assert.Contains(q.Id, standardIds));
        Assert.All(standard.Questions, q => Assert.Contains(q.Id, fullIds));

        // Alla kategorier finns med även i snabbläget.
        Assert.Equal(11, snabb.Categories.Count);
    }

    [Fact]
    public async Task Questionnaire_InvalidMode_ReturnsValidationProblem()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/questionnaire?mode=30");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SubmitAndFetchResult_ComputesAndPersists()
    {
        var client = factory.CreateClient();
        var questionnaire = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire");

        var request = new SubmitQuizRequest(
            questionnaire!.Questions
                .Select(q => new SubmitAnswerDto(q.Id, 4, false, false))
                .ToList());

        var submit = await client.PostAsJsonAsync("/api/quiz/results", request);
        submit.EnsureSuccessStatusCode();
        var token = (await submit.Content.ReadFromJsonAsync<SubmitQuizResponse>())!.ShareToken;
        Assert.False(string.IsNullOrWhiteSpace(token));

        var result = await client.GetFromJsonAsync<ResultDocument>($"/api/results/{token}");
        Assert.NotNull(result);
        Assert.Equal(8, result!.Overall.Count);
        Assert.All(result.Overall, p => Assert.True(p.AgreementPct is >= 0 and <= 100));

        // Samma token ger samma (frysta) resultat.
        var again = await client.GetAsync($"/api/results/{token}");
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task UnknownResultToken_Returns404()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/results/finns-inte");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();
        var res = await client.GetAsync("/api/admin/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_GrantsAccessToAdminEndpoints()
    {
        var client = factory.CreateClient(); // hanterar cookies mellan anrop

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await client.GetFromJsonAsync<AuthUserDto>("/api/auth/me");
        Assert.Contains("Admin", me!.Roles);

        var categories = await client.GetAsync("/api/admin/categories");
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
    }

    [Fact]
    public async Task QuizStart_InvalidMode_ReturnsValidationProblem()
    {
        var client = factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/quiz/start", new StartQuizRequest(30));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task QuizFunnel_CountsStartsAndCompletions()
    {
        var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest(ApiFactory.AdminEmail, ApiFactory.AdminPassword));

        // Tester delar databas → mät förändring i stället för absoluta tal.
        var before = await client.GetFromJsonAsync<QuizFunnelDto>("/api/admin/quiz/funnel");

        // Två påbörjade-signaler (olika läge/variant).
        (await client.PostAsJsonAsync("/api/quiz/start", new StartQuizRequest(25)))
            .EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/quiz/start", new StartQuizRequest(50, Simplified: true)))
            .EnsureSuccessStatusCode();

        // En slutförd kompass i läge 50 (standard) → skriver en Completed-händelse serverside.
        var questionnaire = await client.GetFromJsonAsync<QuestionnaireDto>("/api/questionnaire?mode=50");
        var submit = await client.PostAsJsonAsync("/api/quiz/results", new SubmitQuizRequest(
            questionnaire!.Questions.Select(q => new SubmitAnswerDto(q.Id, 3, false, false)).ToList(),
            Simplified: false,
            Mode: 50));
        submit.EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<QuizFunnelDto>("/api/admin/quiz/funnel");

        Assert.Equal(before!.Started + 2, after!.Started);
        Assert.Equal(before.Completed + 1, after.Completed);

        // Lägesfördelningen har en slutförd i läge 50 standard.
        var mode50 = after.ByMode.FirstOrDefault(m => m is { Mode: 50, Variant: "standard" });
        Assert.NotNull(mode50);
        Assert.True(mode50!.Completed >= 1);
    }
}

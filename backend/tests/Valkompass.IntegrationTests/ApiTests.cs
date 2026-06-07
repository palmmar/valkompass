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
        Assert.Equal(50, dto!.Questions.Count);
        Assert.Equal(11, dto.Categories.Count);
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
}

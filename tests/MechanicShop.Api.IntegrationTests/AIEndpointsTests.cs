using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Application.Features.Identity.Dtos;

namespace MechanicShop.Api.IntegrationTests;

[Collection("ApiDb")]
public sealed class AIEndpointsTests
{
    private const string ManagerEmail = "manager@mechanicshop.test";
    private const string ManagerPassword = "ManagerP@ss1";

    private readonly ApiDbFactory _factory;

    public AIEndpointsTests(ApiDbFactory factory) => _factory = factory;

    [Fact]
    public async Task DiagnoseText_ReturnsStructuredDiagnosis()
    {
        using var client = _factory.CreateClient();
        var token = await GetAccessTokenAsync(client);
        using var request = Authorized(HttpMethod.Post, "/api/ai/diagnose", token);
        request.Content = JsonContent.Create(new { description = "Grinding noise when braking" });

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("diagnosis").GetProperty("urgency").GetString().Should().Be("High");
        body.GetProperty("responseText").GetString().Should().Contain("identified the issue");
    }

    [Fact]
    public async Task DiagnoseImage_AcceptsAuthenticatedMultipartUpload()
    {
        using var client = _factory.CreateClient();
        var token = await GetAccessTokenAsync(client);
        using var request = Authorized(HttpMethod.Post, "/api/ai/diagnose-image", token);
        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "image", "warning.png");
        form.Add(new StringContent("Dashboard warning light"), "description");
        request.Content = form;

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("diagnosis").GetProperty("title").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Transcribe_AcceptsAuthenticatedMultipartUpload()
    {
        using var client = _factory.CreateClient();
        var token = await GetAccessTokenAsync(client);
        using var request = Authorized(HttpMethod.Post, "/api/ai/transcribe", token);
        using var form = new MultipartFormDataContent();
        var audio = new ByteArrayContent([1, 2, 3, 4]);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "audio", "symptom.wav");
        request.Content = form;

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("transcript").GetString().Should().NotBeNullOrWhiteSpace();
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string uri, string token)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetAccessTokenAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(ManagerEmail, ManagerPassword),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponseDto>(TestContext.Current.CancellationToken);
        return token!.AccessToken;
    }
}

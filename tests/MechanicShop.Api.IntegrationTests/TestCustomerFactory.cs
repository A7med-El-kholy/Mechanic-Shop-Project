using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using MechanicShop.Api.Features.Auth.Contracts;
using MechanicShop.Api.Features.Customers.Contracts;

namespace MechanicShop.Api.IntegrationTests;

internal static class TestCustomerFactory
{
    public static async Task<(Guid CustomerId, Guid VehicleId, string Token)> RegisterWithVehicleAsync(
        HttpClient client,
        string managerToken,
        string namePrefix,
        string emailPrefix,
        string make,
        string model,
        string platePrefix,
        string vinPrefix)
    {
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@mechanicshop.test";
        var name = $"{namePrefix} {tag}";

        using var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Passw0rd!", name, "01000000000"),
            TestContext.Current.CancellationToken);
        registered.EnsureSuccessStatusCode();

        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Passw0rd!"),
            TestContext.Current.CancellationToken);
        login.EnsureSuccessStatusCode();
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var token = loginJson.GetProperty("accessToken").GetString()!;

        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/api/customers/me");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var profileResponse = await client.SendAsync(profileRequest, TestContext.Current.CancellationToken);
        profileResponse.EnsureSuccessStatusCode();
        var profile = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var customerId = profile.GetProperty("customerId").GetGuid();

        using var addVehicle = new HttpRequestMessage(HttpMethod.Post, $"/api/customers/{customerId}/vehicles")
        {
            Content = JsonContent.Create(new CreateVehicleRequest(
                make, model, 2022, $"{platePrefix}-{tag}", $"{vinPrefix}{tag}")),
        };
        addVehicle.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        using var vehicleResponse = await client.SendAsync(addVehicle, TestContext.Current.CancellationToken);
        vehicleResponse.EnsureSuccessStatusCode();
        var vehicle = await vehicleResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return (customerId, vehicle.GetProperty("vehicleId").GetGuid(), token);
    }
}

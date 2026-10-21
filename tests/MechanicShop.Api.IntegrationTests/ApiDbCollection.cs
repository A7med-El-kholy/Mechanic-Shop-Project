namespace MechanicShop.Api.IntegrationTests;

/// <summary>One SQL Server container + API host shared by all auth/user test classes.</summary>
[CollectionDefinition("ApiDb")]
public sealed class ApiDbCollection : ICollectionFixture<ApiDbFactory>;

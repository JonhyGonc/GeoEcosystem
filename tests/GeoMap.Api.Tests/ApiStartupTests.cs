using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GeoMap.Api.Tests;

public sealed class ApiStartupTests
{
    [Fact]
    public async Task Health_endpoint_returns_ok_when_database_configuration_is_present()
    {
        using var factory = CreateFactoryWithDatabaseConfiguration();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Startup_fails_with_a_clear_message_when_database_configuration_is_missing()
    {
        using var factory = new WebApplicationFactory<Program>();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            "Database:ConnectionString must be provided",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactoryWithDatabaseConfiguration()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Database:ConnectionString"] =
                                "Host=localhost;Port=5432;Database=geomap;Username=test;Password=test"
                        });
                });
            });
    }
}

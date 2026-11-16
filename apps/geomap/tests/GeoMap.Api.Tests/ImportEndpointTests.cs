using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GeoMap.Api.Imports;
using GeoMap.Contract;
using GeoMap.Importer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GeoMap.Api.Tests;

/// <summary>Importação pelo site (M4.5): a rota usa o mesmo caminho do importador do terminal.</summary>
public sealed class ImportEndpointTests
{
    private static readonly Guid ProjectId = Guid.Parse("70000000-0000-4000-8000-000000000001");
    private static readonly Guid NetworkId = Guid.Parse("70000000-0000-4000-8000-000000000002");

    [Fact]
    public async Task Valid_document_is_imported_and_returns_the_summary()
    {
        var store = new RecordingStore();
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(ImportEndpoints.Route, Example("valid-geoautoengine-polyline.geomap.json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var summary = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(summary.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, summary.RootElement.GetProperty("imported").GetInt32());
        Assert.Equal(ProjectId, summary.RootElement.GetProperty("projectId").GetGuid());
        Assert.Equal(NetworkId, summary.RootElement.GetProperty("networkId").GetGuid());
        var document = Assert.Single(store.Documents);
        Assert.Equal("project-001:aguas_pluviais", document.Network.ExternalId);
        Assert.Equal("Host=unused", store.ConnectionStrings.Single());
        Assert.Matches("^[0-9a-f]{64}$", document.SourceFileSha256);
    }

    [Fact]
    public async Task Invalid_document_returns_the_contract_error_and_is_not_imported()
    {
        var store = new RecordingStore();
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(ImportEndpoints.Route, Example("invalid-crs.geomap.json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ContractErrorCodes.CrsInvalid, await ErrorCodesAsync(response));
        Assert.Empty(store.Documents);
    }

    [Fact]
    public async Task Empty_body_is_rejected()
    {
        using var factory = CreateFactory(new RecordingStore());
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(ImportEndpoints.Route, new ByteArrayContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new string?[] { ImportErrorCodes.FileInvalid }, await ErrorCodesAsync(response));
    }

    [Fact]
    public async Task Document_over_50_MiB_is_rejected_before_import()
    {
        var store = new RecordingStore();
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();
        var content = new ByteArrayContent(new byte[GeoMapContractValidator.MaximumDocumentBytes + 1]);

        using var response = await client.PostAsync(ImportEndpoints.Route, content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(new string?[] { ContractErrorCodes.LimitExceeded }, await ErrorCodesAsync(response));
        Assert.Empty(store.Documents);
    }

    [Fact]
    public async Task Database_failure_returns_server_error_with_the_importer_code()
    {
        var store = new RecordingStore { Failure = new InvalidOperationException("database is down") };
        using var factory = CreateFactory(store);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(ImportEndpoints.Route, Example("valid-geoautoengine-polyline.geomap.json"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(new string?[] { ImportErrorCodes.PersistenceFailed }, await ErrorCodesAsync(response));
    }

    private static async Task<string?[]> ErrorCodesAsync(HttpResponseMessage response)
    {
        using var summary = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(summary.RootElement.GetProperty("success").GetBoolean());
        return summary.RootElement.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
    }

    private static ByteArrayContent Example(string fileName)
    {
        var content = new ByteArrayContent(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "examples", fileName)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static WebApplicationFactory<Program> CreateFactory(RecordingStore store) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=unused",
                }));
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IGeoMapImportStore>(store));
        });

    private sealed class RecordingStore : IGeoMapImportStore
    {
        public List<GeoMapImportDocument> Documents { get; } = [];

        public List<string> ConnectionStrings { get; } = [];

        public Exception? Failure { get; init; }

        public Task<GeoMapImportResult> ImportAsync(
            string connectionString,
            GeoMapImportDocument document,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            ConnectionStrings.Add(connectionString);
            Documents.Add(document);
            return Task.FromResult(new GeoMapImportResult(ProjectId, NetworkId, document.Features.Count, 0));
        }
    }
}

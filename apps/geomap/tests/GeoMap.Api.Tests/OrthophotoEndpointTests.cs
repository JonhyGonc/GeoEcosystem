using System.Net;
using System.Text.Json;
using GeoMap.Api.Orthophotos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GeoMap.Api.Tests;

/// <summary>Ortofoto em MBTiles por projeto (DEC-035).</summary>
public sealed class OrthophotoEndpointTests : IDisposable
{
    private static readonly Guid ProjectId = Guid.Parse("71000000-0000-4000-8000-000000000001");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private readonly string directory = Directory.CreateTempSubdirectory("geomap-orto-").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task Project_without_file_reports_the_expected_file_name()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/projects/{ProjectId}/orthophoto");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("available").GetBoolean());
        Assert.Equal("Obra GF-CV-02.mbtiles", body.RootElement.GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Project_with_file_named_after_the_project_describes_the_layer()
    {
        CreateMbtiles("Obra GF-CV-02.mbtiles", withZoomMetadata: true);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/projects/{ProjectId}/orthophoto");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.True(root.GetProperty("available").GetBoolean());
        Assert.Equal($"/api/projects/{ProjectId}/orthophoto/tiles/{{z}}/{{x}}/{{y}}", root.GetProperty("tileUrl").GetString());
        Assert.Equal("png", root.GetProperty("format").GetString());
        Assert.Equal(new[] { -39.1, -14.2, -39.0, -14.1 }, root.GetProperty("bounds").EnumerateArray().Select(value => value.GetDouble()));
        Assert.Equal(0, root.GetProperty("minZoom").GetInt32());
        Assert.Equal(1, root.GetProperty("maxZoom").GetInt32());
    }

    [Fact]
    public async Task File_named_after_the_project_id_also_works_and_zooms_come_from_the_tiles()
    {
        CreateMbtiles($"{ProjectId}.mbtiles", withZoomMetadata: false);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/projects/{ProjectId}/orthophoto");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("available").GetBoolean());
        Assert.Equal(1, body.RootElement.GetProperty("maxZoom").GetInt32());
    }

    [Fact]
    public async Task Tile_is_read_with_the_tms_row_flipped()
    {
        CreateMbtiles("Obra GF-CV-02.mbtiles", withZoomMetadata: true);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // z=1, y=0 (XYZ, de cima) é a linha 1 no MBTiles (TMS, de baixo).
        using var present = await client.GetAsync($"/api/projects/{ProjectId}/orthophoto/tiles/1/0/0");
        using var missing = await client.GetAsync($"/api/projects/{ProjectId}/orthophoto/tiles/1/0/1");
        using var invalid = await client.GetAsync($"/api/projects/{ProjectId}/orthophoto/tiles/1/2/0");

        Assert.Equal(HttpStatusCode.OK, present.StatusCode);
        Assert.Equal("image/png", present.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await present.Content.ReadAsByteArrayAsync());
        Assert.True(present.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromHours(1), present.Headers.CacheControl?.MaxAge);
        Assert.Equal(HttpStatusCode.NoContent, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Unknown_project_is_not_found()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/projects/{Guid.NewGuid()}/orthophoto");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Obra GF-CV-02", "Obra GF-CV-02.mbtiles")]
    [InlineData(" A/B:C*D ", "A-B-C-D.mbtiles")]
    public void File_name_replaces_characters_windows_does_not_accept(string projectName, string fileName)
    {
        Assert.Equal(fileName, OrthophotoStore.FileNameFor(projectName));
    }

    private void CreateMbtiles(string fileName, bool withZoomMetadata)
    {
        using var connection = new SqliteConnection($"Data Source={Path.Combine(directory, fileName)}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE metadata (name text, value text);
            CREATE TABLE tiles (zoom_level integer, tile_column integer, tile_row integer, tile_data blob);
            INSERT INTO metadata VALUES ('format', 'png'), ('bounds', '-39.1,-14.2,-39.0,-14.1');
            INSERT INTO tiles VALUES (0, 0, 0, $png), (1, 0, 1, $png);
            """;
        command.Parameters.AddWithValue("$png", Png);
        command.ExecuteNonQuery();
        if (withZoomMetadata)
        {
            using var zooms = connection.CreateCommand();
            zooms.CommandText = "INSERT INTO metadata VALUES ('minzoom', '0'), ('maxzoom', '1');";
            zooms.ExecuteNonQuery();
        }
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = "Host=unused",
                    ["Orthophotos:Directory"] = directory,
                }));
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IProjectNameLookup>(new FakeProjects()));
        });

    private sealed class FakeProjects : IProjectNameLookup
    {
        public Task<string?> FindNameAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(projectId == ProjectId ? "Obra GF-CV-02" : null);
    }
}

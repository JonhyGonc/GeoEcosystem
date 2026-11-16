using System.Text.Json;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Tests;

public sealed class GeoMapExportTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_OpenPolyline_ProducesGeoMapVersion01Contract()
    {
        var json = GeoMapExportSerializer.Serialize(CreateRequest(
            new GeoMapExportFeature(
                SampleFeatureId,
                "2A7F",
                "GF-Rede Pluvial",
                [new(688100.25, 7086400.75), new(688112.5, 7086411)],
                false,
                0,
                false)));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("0.1", root.GetProperty("contractVersion").GetString());
        Assert.Equal("GeoAutoEngine", root.GetProperty("sourceSystem").GetString());
        Assert.Equal("EPSG:31982", root.GetProperty("sourceCrs").GetString());
        Assert.Equal("metre", root.GetProperty("units").GetString());
        Assert.EndsWith("Z", root.GetProperty("exportedAt").GetString(), StringComparison.Ordinal);

        var features = root.GetProperty("features");
        Assert.Equal(1, features.GetArrayLength());
        var feature = features[0];
        Assert.Equal(SampleFeatureId, feature.GetProperty("sourceId").GetString());
        Assert.Equal("2A7F", feature.GetProperty("properties").GetProperty("cadHandle").GetString());
        Assert.Equal("Polyline", feature.GetProperty("sourceType").GetString());
        Assert.Equal("GF-Rede Pluvial", feature.GetProperty("sourceLayer").GetString());
        Assert.Equal("LineString", feature.GetProperty("geometry").GetProperty("type").GetString());
        Assert.Equal(2, feature.GetProperty("geometry").GetProperty("coordinates").GetArrayLength());
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_ClosedPolyline_AppendsFirstPositionAndPreservesProjectionMetadata()
    {
        var json = GeoMapExportSerializer.Serialize(CreateRequest(
            new GeoMapExportFeature(
                "0e0c0000-0000-4000-8000-0000000000a1",
                "30",
                "REDE-DRENAGEM",
                [new(0, 0), new(10, 0), new(10, 10)],
                true,
                5.5,
                true)));

        using var document = JsonDocument.Parse(json);
        var feature = document.RootElement.GetProperty("features")[0];
        var coordinates = feature.GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(4, coordinates.GetArrayLength());
        Assert.Equal(coordinates[0].GetRawText(), coordinates[3].GetRawText());
        Assert.Equal(3, feature.GetProperty("properties").GetProperty("vertexCount").GetInt32());
        Assert.True(feature.GetProperty("properties").GetProperty("closed").GetBoolean());
        Assert.True(feature.GetProperty("properties").GetProperty("projectedTo2D").GetBoolean());
        Assert.Equal(5.5, feature.GetProperty("properties").GetProperty("cadElevation").GetDouble());
    }

    [Theory]
    [InlineData("31982")]
    [InlineData("EPSG:")]
    [InlineData("SRID:31982")]
    [Trait("Category", "FAST")]
    public void Serialize_InvalidCrs_IsRejected(string sourceCrs)
    {
        var request = CreateRequest(
            new GeoMapExportFeature(SampleFeatureId, "1", "0", [new(0, 0), new(1, 1)], false, 0, false)) with
        {
            SourceCrs = sourceCrs,
        };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(request));
        Assert.Contains("EPSG", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_DuplicateFeatureId_IsRejected()
    {
        var feature = new GeoMapExportFeature(SampleFeatureId, "1A", "0", [new(0, 0), new(1, 1)], false, 0, false);
        var request = CreateRequest(feature) with { Features = [feature, feature] };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(request));
        Assert.Contains("mais de uma vez", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_SampleRequest_ProducesTheVersionedSample()
    {
        // samples/geomap/polyline-export.geomap.json é igual à fixture do GeoMap
        // (apps/geomap/contracts/geomap/0.1/examples/valid-geoautoengine-polyline.geomap.json),
        // que passa pelo validador, pelo importador e pela API na CI do GeoMap.
        var json = GeoMapExportSerializer.Serialize(CreateRequest(
            new GeoMapExportFeature(
                SampleFeatureId,
                "2A7F",
                "GF-Rede Pluvial",
                [new(688100.25, 7086400.75), new(688112.5, 7086411)],
                false,
                0,
                false)));

        using var actual = JsonDocument.Parse(json);
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "samples", "geomap", "polyline-export.geomap.json")));
        Assert.Equal(
            JsonSerializer.Serialize(expected.RootElement),
            JsonSerializer.Serialize(actual.RootElement));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_WithNetworkColor_ProducesVersion02AndTheVersionedSample()
    {
        var json = GeoMapExportSerializer.Serialize(CreateRequest(SampleFeature()) with { NetworkColor = "#1F77B4" });

        using var actual = JsonDocument.Parse(json);
        Assert.Equal("0.2", actual.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal("#1F77B4", actual.RootElement.GetProperty("network").GetProperty("color").GetString());
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "samples", "geomap", "polyline-export-color.geomap.json")));
        Assert.Equal(
            JsonSerializer.Serialize(expected.RootElement),
            JsonSerializer.Serialize(actual.RootElement));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_WithoutNetworkColor_StaysVersion01WithoutTheField()
    {
        using var document = JsonDocument.Parse(GeoMapExportSerializer.Serialize(CreateRequest(SampleFeature())));

        Assert.Equal("0.1", document.RootElement.GetProperty("contractVersion").GetString());
        Assert.False(document.RootElement.GetProperty("network").TryGetProperty("color", out _));
    }

    [Theory]
    [InlineData("#1f77b4")]
    [InlineData("1F77B4")]
    [InlineData("#1F77B")]
    [Trait("Category", "FAST")]
    public void Serialize_InvalidNetworkColor_IsRejected(string color)
    {
        var request = CreateRequest(SampleFeature()) with { NetworkColor = color };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(request));
        Assert.Contains("#RRGGBB", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("polyline-export.geomap.json", "0.1", "valid-geoautoengine-polyline.geomap.json")]
    [InlineData("polyline-export-color.geomap.json", "0.2", "valid-geoautoengine-color.geomap.json")]
    [Trait("Category", "FAST")]
    public void Samples_AreIdenticalToTheExamplesValidatedByGeoMap(string sample, string version, string example)
    {
        // Teste cruzado (M4.3, GM-10): a saída do GAE é a mesma fixture que o GeoMap valida e importa na CI.
        var geoMapExample = Path.Combine(
            RepositoryRoot(), "..", "geomap", "contracts", "geomap", version, "examples", example);

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(RepositoryRoot(), "samples", "geomap", sample)),
            File.ReadAllBytes(geoMapExample));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NetworkColor_ComesFromTheLayerWithMostPolylines()
    {
        GeoMapExportFeature[] features =
        [
            SampleFeature("A", "REDE-B"),
            SampleFeature("B", "REDE-A"),
            SampleFeature("C", "REDE-A"),
        ];
        var colors = new Dictionary<string, string?> { ["REDE-A"] = "#FF7F00", ["REDE-B"] = "#000000" };

        Assert.Equal(new GeoMapNetworkColorChoice("#FF7F00", "REDE-A"), GeoMapLayerColor.ChooseNetworkColor(features, colors));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NetworkColor_TieGoesToTheFirstLayerAndMissingColorMeansNone()
    {
        GeoMapExportFeature[] features = [SampleFeature("A", "REDE-B"), SampleFeature("B", "REDE-A")];

        Assert.Equal(
            new GeoMapNetworkColorChoice("#00FF00", "REDE-A"),
            GeoMapLayerColor.ChooseNetworkColor(features, new Dictionary<string, string?> { ["REDE-A"] = "#00FF00" }));
        Assert.Equal(
            new GeoMapNetworkColorChoice(null, "REDE-A"),
            GeoMapLayerColor.ChooseNetworkColor(features, new Dictionary<string, string?> { ["REDE-B"] = "#00FF00" }));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LayerColor_IsWrittenAsUppercaseHex()
    {
        Assert.Equal("#FF7F00", GeoMapLayerColor.ToHex(255, 127, 0));
        Assert.Equal("#000000", GeoMapLayerColor.ForegroundColor);
        Assert.True(GeoMapLayerColor.IsValid("#0A0B0C"));
        Assert.False(GeoMapLayerColor.IsValid("#0a0b0c"));
    }

    private static GeoMapExportFeature SampleFeature(string handle = "2A7F", string layer = "GF-Rede Pluvial") => new(
        handle == "2A7F" ? SampleFeatureId : $"00000000-0000-4000-8000-00000000000{handle}",
        handle,
        layer,
        [new(688100.25, 7086400.75), new(688112.5, 7086411)],
        false,
        0,
        false);

    private const string SampleFeatureId = "7c1e4b2a-3d5f-4a6b-9c8d-0e1f2a3b4c5d";

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GeoAutoEngine.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate the GeoAutoEngine repository root.");
    }

    private static GeoMapExportRequest CreateRequest(params GeoMapExportFeature[] features) => new(
        "EPSG:31982",
        "metre",
        "project-001",
        "Projeto exemplo",
        "project-001:aguas_pluviais",
        "Águas pluviais",
        "stormwater",
        new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero),
        features);
}

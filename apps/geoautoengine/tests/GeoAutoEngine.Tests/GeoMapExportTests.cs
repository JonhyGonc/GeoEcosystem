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
    [InlineData("block-export.geomap.json", "0.3", "valid-geoautoengine-blocks.geomap.json")]
    [InlineData("curve-export.geomap.json", "0.1", "valid-geoautoengine-curves.geomap.json")]
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

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_Blocks_ProducesVersion03PointsAndTheVersionedSample()
    {
        var request = CreateRequest(
            BlockFeature(
                "3b9a1c7e-5d2f-4e8a-b1c4-6d7e8f9a0b1c",
                "2B10",
                new(688100.25, 7086400.75),
                0,
                new GeoMapBlockInfo("TE", 90, 1, 1, false)),
            BlockFeature(
                "9f8e7d6c-5b4a-4321-8fed-cba987654321",
                "2B11",
                new(688112.5, 7086411),
                1000,
                new GeoMapBlockInfo("REGISTRO", 0, 2, 2, true))) with
        {
            NetworkExternalId = "project-001:aguas_pluviais:blocos",
            NetworkName = "Águas pluviais (blocos)",
            NetworkColor = "#FF0000",
        };

        using var actual = JsonDocument.Parse(GeoMapExportSerializer.Serialize(request));
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "samples", "geomap", "block-export.geomap.json")));
        Assert.Equal(
            JsonSerializer.Serialize(expected.RootElement),
            JsonSerializer.Serialize(actual.RootElement));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ContractVersion_IsTheLowestThatAcceptsTheFile()
    {
        var line = CreateRequest(SampleFeature());
        var block = CreateRequest(BlockFeature(SampleFeatureId, "2B10", new(1, 2), 0, new GeoMapBlockInfo("TE", 0, 1, 1, false)));

        Assert.Equal("0.1", GeoMapExportSerializer.ContractVersionFor(line));
        Assert.Equal("0.2", GeoMapExportSerializer.ContractVersionFor(line with { NetworkColor = "#000000" }));
        Assert.Equal("0.3", GeoMapExportSerializer.ContractVersionFor(block));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_BlockWithMoreThanOnePosition_IsRejected()
    {
        var feature = BlockFeature(SampleFeatureId, "2B10", new(1, 2), 0, new GeoMapBlockInfo("TE", 0, 1, 1, false)) with
        {
            Vertices = [new(1, 2), new(3, 4)],
        };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(CreateRequest(feature)));
        Assert.Contains("exatamente uma posição", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("_TG-PONTOS", "TE", "layer de levantamento (_TG*)")]
    [InlineData("_tg_cotas", "TE", "layer de levantamento (_TG*)")]
    [InlineData("RD_AGUA", "SIMB_PONTO", "ponto levantado (SIMB_PONTO)")]
    [InlineData("RD_AGUA", "*U12", "bloco anônimo")]
    [InlineData("RD_AGUA", " ", "bloco anônimo")]
    [InlineData("RD_AGUA", "TE", null)]
    [Trait("Category", "FAST")]
    public void BlockRules_LeaveSurveyAndAnonymousBlocksOut(string layer, string name, string? reason)
    {
        Assert.Equal(reason, GeoMapBlockRules.SkipReason(layer, name));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void BlockRules_KeepTheInsertionPointForOrdinaryBlocks()
    {
        var extents = new GeoMapBlockDefinitionExtents(0, 0, -0.5, -0.5, 0.5, 0.5);

        var position = GeoMapBlockRules.Resolve(new(100, 200), extents, 45, 2, 2);

        Assert.Equal(new GeoMapBlockPosition(new(100, 200), false), position);
        Assert.Equal(new GeoMapBlockPosition(new(100, 200), false), GeoMapBlockRules.Resolve(new(100, 200), null, 0, 1, 1));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void BlockRules_UseTheSymbolCenterWhenTheDrawingIsFarFromTheBase()
    {
        // Desenho em coordenadas reais (centro 688100, 7086400) com o ponto base em 0,0, como os tês da DEC-029.
        var extents = new GeoMapBlockDefinitionExtents(0, 0, 688099, 7086399, 688101, 7086401);

        var plain = GeoMapBlockRules.Resolve(new(0, 0), extents, 0, 1, 1);
        var rotated = GeoMapBlockRules.Resolve(new(10, 20), extents, 90, 1, 1);

        Assert.True(plain.FromSymbol);
        Assert.Equal(688100, plain.Position.X, 6);
        Assert.Equal(7086400, plain.Position.Y, 6);
        Assert.True(rotated.FromSymbol);
        Assert.Equal(10 - 7086400, rotated.Position.X, 6);
        Assert.Equal(20 + 688100, rotated.Position.Y, 6);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void BlockRules_NameTheBlockNetworkAndFile()
    {
        Assert.Equal("obra:aguas_pluviais:blocos", GeoMapBlockRules.BlockNetworkExternalId("obra:aguas_pluviais"));
        Assert.Equal("Águas pluviais (blocos)", GeoMapBlockRules.BlockNetworkName("Águas pluviais"));
        Assert.Equal(@"C:\obra\rede-blocos.geomap.json", GeoMapBlockRules.BlockFilePath(@"C:\obra\rede.geomap.json"));
        Assert.Equal(@"C:\obra\rede-blocos.geomap.json", GeoMapBlockRules.BlockFilePath(@"C:\obra\rede.GEOMAP.JSON"));
        Assert.Equal("rede.json-blocos.geomap.json", GeoMapBlockRules.BlockFilePath("rede.json"));
        Assert.Equal(90, GeoMapBlockRules.RotationDegrees(Math.PI / 2));
        Assert.Equal(
            "MINSERT=1; bloco anônimo=3",
            GeoMapBlockRules.Describe(new Dictionary<string, int> { ["bloco anônimo"] = 3, ["MINSERT"] = 1 }));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_CurvesAndLines_ProduceTheVersionedSample()
    {
        var arc = Math.Tan(Math.PI / 8);
        var request = CreateRequest(
            new GeoMapExportFeature(
                "5a1d2c3e-4b5f-4c6d-8e7f-9a0b1c2d3e4f",
                "2C01",
                "GF-Rede Pluvial",
                [new(688100, 7086400), new(688110, 7086400), new(688110, 7086410)],
                false,
                0,
                false,
                Bulges: [arc, 0, 0]),
            new GeoMapExportFeature(
                "6b2e3d4f-5c6a-4d7e-9f8a-0b1c2d3e4f5a",
                "2C02",
                "GF-Rede Pluvial",
                [new(688120, 7086400), new(688130, 7086405)],
                false,
                1.5,
                true,
                EntityType: GeoMapEntityTypes.Line,
                EndElevation: 2),
            new GeoMapExportFeature(
                "7c3f4e5a-6d7b-4e8f-a09b-1c2d3e4f5a6b",
                "2C03",
                "GF-Rede Pluvial",
                [new(688210, 7086400), new(688200, 7086410)],
                false,
                0,
                false,
                Bulges: [GeoMapArcTessellation.ArcBulge(0, Math.PI / 2, false), 0],
                EntityType: GeoMapEntityTypes.Arc));

        using var actual = JsonDocument.Parse(GeoMapExportSerializer.Serialize(request));
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "samples", "geomap", "curve-export.geomap.json")));
        // Os pontos dos arcos vêm de seno e cosseno: a comparação aceita diferença no último dígito.
        AssertJsonEquivalent(expected.RootElement, actual.RootElement, "$");
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Tessellation_StaysWithinFiveMillimetresOfTheArc()
    {
        // Meia-volta de raio 50 m: o centro fica no meio da corda.
        GeoMapCoordinate start = new(0, 0);
        GeoMapCoordinate end = new(100, 0);
        var tolerance = GeoMapArcTessellation.ToleranceFor("metre");

        var points = GeoMapArcTessellation.InteriorPoints(start, end, 1, tolerance);
        GeoMapCoordinate[] all = [start, .. points, end];

        Assert.Equal(0.005, tolerance);
        Assert.All(points, point => Assert.Equal(50, Math.Sqrt(((point.X - 50) * (point.X - 50)) + (point.Y * point.Y)), 6));
        Assert.All(points, point => Assert.True(point.Y < 0, "bulge positivo: arco anti-horário, abaixo da corda"));
        for (var index = 1; index < all.Length; index++)
        {
            var half = Math.Sqrt(Math.Pow(all[index].X - all[index - 1].X, 2) + Math.Pow(all[index].Y - all[index - 1].Y, 2)) / 2;
            var sagitta = 50 - Math.Sqrt((50 * 50) - (half * half));
            Assert.True(sagitta <= tolerance + 1e-9, $"flecha {sagitta} no trecho {index}");
        }

        Assert.Equal(points.Count, GeoMapArcTessellation.InteriorPoints(end, start, -1, tolerance).Count);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Tessellation_KeepsStraightLinesAndClosingAsBefore()
    {
        GeoMapCoordinate[] square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

        Assert.Equal(
            new GeoMapCoordinate[] { new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(0, 0) },
            GeoMapArcTessellation.Positions(square, null, true, 0.005));
        Assert.Equal(square, GeoMapArcTessellation.Positions(square, [0, 0, 0, 0], false, 0.005));
        Assert.Equal(
            new GeoMapCoordinate[] { new(0, 0), new(1, 0), new(0, 0) },
            GeoMapArcTessellation.Positions([new(0, 0), new(1, 0), new(0, 0)], null, true, 0.005));
        Assert.Empty(GeoMapArcTessellation.InteriorPoints(new(0, 0), new(0, 0), 1, 0.005));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Tessellation_ClosedPolylineWithArcEndsOnTheFirstVertex()
    {
        // Círculo feito com duas meias-voltas (como o AutoCAD desenha uma Polyline circular).
        GeoMapCoordinate[] vertices = [new(0, 0), new(10, 0)];

        var positions = GeoMapArcTessellation.Positions(vertices, [1, 1], true, 0.005);

        Assert.Equal(vertices[0], positions[0]);
        Assert.Equal(vertices[0], positions[^1]);
        Assert.Contains(vertices[1], positions);
        Assert.All(positions, point => Assert.Equal(5, Math.Sqrt(((point.X - 5) * (point.X - 5)) + (point.Y * point.Y)), 6));
    }

    [Theory]
    [InlineData(0, Math.PI / 2, false, 0.41421356237309503)]
    [InlineData(0, Math.PI / 2, true, -0.41421356237309503)]
    [InlineData(3 * Math.PI / 2, Math.PI / 2, false, 1)]
    [Trait("Category", "FAST")]
    public void ArcBulge_IsAQuarterOfTheSweepAndFlipsWhenMirrored(double start, double end, bool down, double bulge)
    {
        Assert.Equal(bulge, GeoMapArcTessellation.ArcBulge(start, end, down), 12);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Tolerance_IsFiveMillimetresInTheDrawingUnit()
    {
        Assert.Equal(0.005 / 0.3048, GeoMapArcTessellation.ToleranceFor("foot"), 12);
        Assert.Equal(0.005 * 3937 / 1200, GeoMapArcTessellation.ToleranceFor("us-survey-foot"), 12);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Serialize_LineWithoutEndElevation_IsRejected()
    {
        var line = SampleFeature() with { EntityType = GeoMapEntityTypes.Line };

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(CreateRequest(line)));
        Assert.Contains("duas elevações", error.Message, StringComparison.Ordinal);
    }

    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind, $"{path}: {expected.ValueKind} ≠ {actual.ValueKind}");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                Assert.Equal(
                    expected.EnumerateObject().Select(property => property.Name),
                    actual.EnumerateObject().Select(property => property.Name));
                foreach (var property in expected.EnumerateObject())
                {
                    AssertJsonEquivalent(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                }

                break;
            case JsonValueKind.Array:
                Assert.True(expected.GetArrayLength() == actual.GetArrayLength(), $"{path}: tamanho diferente");
                for (var index = 0; index < expected.GetArrayLength(); index++)
                {
                    AssertJsonEquivalent(expected[index], actual[index], $"{path}[{index}]");
                }

                break;
            case JsonValueKind.Number:
                Assert.True(
                    Math.Abs(expected.GetDouble() - actual.GetDouble()) <= 1e-6,
                    $"{path}: {expected.GetDouble()} ≠ {actual.GetDouble()}");
                break;
            default:
                Assert.Equal(expected.ToString(), actual.ToString());
                break;
        }
    }

    private static GeoMapExportFeature BlockFeature(
        string id,
        string handle,
        GeoMapCoordinate position,
        double elevation,
        GeoMapBlockInfo block) => new(
        id,
        handle,
        "GF-Conexoes Pluvial",
        [position],
        false,
        elevation,
        Math.Abs(elevation) > 1e-10,
        block);

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

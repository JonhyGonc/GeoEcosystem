using System.Diagnostics;
using System.Text.Json;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Tests;

// Profundidades dos pontos _TG (entrega 1): leitura do código, associação pelo NODE, conflitos e exportação.
public sealed class PipeDepthTests
{
    private static readonly PipeDepthOptions Metres = PipeDepthOptions.ForUnits("metre");

    [Theory]
    [InlineData("AP1.20D300", 1.2, "Águas pluviais", 300d)]
    [InlineData("AP1.45D300", 1.45, "Águas pluviais", 300d)]
    [InlineData("ES0.85D150", 0.85, "Esgoto sanitário", 150d)]
    [InlineData("E1.22D150", 1.22, "Esgoto sanitário", 150d)]
    [InlineData("NI0.83", 0.83, "Não identificada", null)]
    [InlineData("ap 1,20 d300", 1.2, "Águas pluviais", 300d)]
    [Trait("Category", "FAST")]
    public void Classifier_ReadsThePipeDepthWithTheExistingInterpreter(
        string description,
        double depth,
        string network,
        double? diameter)
    {
        var reading = new SurveyDepthClassifier().Classify(description, 10);

        Assert.Equal(SurveyDepthKind.PipeDepth, reading.Kind);
        Assert.Equal(depth, reading.DepthMetres);
        Assert.Equal(network, reading.Network);
        Assert.Equal(diameter, reading.DiameterMm);
    }

    [Theory]
    [InlineData("CXAP1.50", SurveyDepthKind.ObjectDepth)]
    [InlineData("CXAP", SurveyDepthKind.NoDepth)]
    [InlineData("GUIA", SurveyDepthKind.NoDepth)]
    [InlineData("POSSIVEL AP1.26", SurveyDepthKind.UnknownCode)]
    [InlineData("XYZ123", SurveyDepthKind.UnknownCode)]
    [InlineData("AP12.5", SurveyDepthKind.InvalidDepth)]
    [InlineData("AP0", SurveyDepthKind.InvalidDepth)]
    [InlineData("", SurveyDepthKind.NoCode)]
    [InlineData("   ", SurveyDepthKind.NoCode)]
    [Trait("Category", "FAST")]
    public void Classifier_KeepsBoxDepthsUnknownCodesAndInvalidValuesOutOfThePipes(string description, SurveyDepthKind kind)
    {
        Assert.Equal(kind, new SurveyDepthClassifier().Classify(description, 10).Kind);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ExactNode_IsAssociatedToTheVertex()
    {
        var analysis = Analyze([Line("L1", (0, 0), (10, 0), (20, 0))], [Point("P1", "AP1.20D300", 10, 0)]);

        var point = Assert.Single(analysis.Points);
        Assert.Equal(PipeDepthPointStatus.Associated, point.Status);
        var measurement = Assert.Single(analysis.ByHandle["L1"].Measurements);
        Assert.Equal(1.2, measurement.DepthMetres);
        Assert.Equal(1, measurement.VertexIndex);
        Assert.Equal(PipeDepthMethods.Vertex, measurement.Method);
        Assert.Equal(10, measurement.Station, 9);
        Assert.Equal("P1", measurement.PointName);
        Assert.Equal("AP1.20D300", measurement.OriginalCode);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NodeWithASmallNumericDifference_IsStillTheVertex()
    {
        var analysis = Analyze([Line("L1", (0, 0), (10, 0))], [Point("P1", "AP1.20", 10.0004, -0.0003)]);

        Assert.Equal(PipeDepthPointStatus.Associated, Assert.Single(analysis.Points).Status);
        Assert.Equal(1, Assert.Single(analysis.ByHandle["L1"].Measurements).VertexIndex);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SeveralNodes_KeepEveryDepthInOrderWithoutAverage()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0), (20, 0))],
            [Point("P3", "AP1.70", 20, 0), Point("P1", "AP1.20", 0, 0), Point("P2", "AP1.45", 10, 0)]);

        var pipe = analysis.ByHandle["L1"];
        Assert.Equal(new[] { 1.2, 1.45, 1.7 }, pipe.Measurements.Select(measurement => measurement.DepthMetres));
        Assert.Equal(new[] { 0, 1, 2 }, pipe.Measurements.Select(measurement => measurement.VertexIndex ?? -1));
        Assert.Equal(1.2, pipe.StartDepthMetres);
        Assert.Equal(1.7, pipe.EndDepthMetres);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PointInTheMiddleOfAStraightSegment_IsAssociatedWithItsPosition()
    {
        var analysis = Analyze([Line("L1", (0, 0), (10, 0), (10, 10))], [Point("P1", "AP0.90", 10, 4)]);

        var measurement = Assert.Single(analysis.ByHandle["L1"].Measurements);
        Assert.Equal(PipeDepthMethods.Segment, measurement.Method);
        Assert.Null(measurement.VertexIndex);
        Assert.Equal(1, measurement.SegmentIndex);
        Assert.Equal(14, measurement.Station, 9);
        Assert.Null(analysis.ByHandle["L1"].StartDepthMetres);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PointOnAnArc_IsMeasuredOnTheTrueCurve()
    {
        // Semicírculo de raio 5 de (0,0) a (10,0), passando por (5,-5) (bulge 1 = anti-horário).
        var arc = Line("L1", (0, 0), (10, 0)) with { Bulges = [1, 0] };

        var analysis = Analyze([arc], [Point("P1", "AP1.10", 5, -5)]);

        var measurement = Assert.Single(analysis.ByHandle["L1"].Measurements);
        Assert.Equal(PipeDepthMethods.Segment, measurement.Method);
        Assert.Equal(Math.PI * 5 / 2, measurement.Station, 6);
        Assert.Equal(0, measurement.Distance, 6);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PointsWithoutDepthOrOutsideAnyLine_AreReportedAndTheRestContinues()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0))],
            [
                Point("P1", "CXAP", 0, 0),
                Point("P2", "CXAP1.50", 0, 0.5),
                Point("P3", "AP1.20", 50, 50),
                Point("P4", "AP1.20", 5, 0.05),
                Point("P5", "COISA ESTRANHA", 5, 0),
                Point("P6", "AP1.30", 10, 0),
            ]);

        Assert.Equal(
            new[]
            {
                PipeDepthPointStatus.NoDepth,
                PipeDepthPointStatus.ObjectDepth,
                PipeDepthPointStatus.NoPipe,
                PipeDepthPointStatus.NearNotAssociated,
                PipeDepthPointStatus.UnknownCode,
                PipeDepthPointStatus.Associated,
            },
            analysis.Points.Select(point => point.Status));
        Assert.Equal(0.05, analysis.Points[3].Distance!.Value, 9);
        Assert.Equal(new[] { "L1" }, analysis.Points[3].PipeHandles);
        Assert.Equal(1.3, Assert.Single(analysis.ByHandle["L1"].Measurements).DepthMetres);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CotaIsNeverReadAsDepth()
    {
        var analysis = Analyze([Line("L1", (0, 0), (10, 0))], [Point("P1", "GUIA", 0, 0, elevation: "8.522")]);

        Assert.Equal(PipeDepthPointStatus.NoDepth, Assert.Single(analysis.Points).Status);
        Assert.Empty(analysis.ByHandle["L1"].Measurements);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NodeAtTheEndOfOneLineAndTheStartOfTheNext_CountsForBoth()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0)), Line("L2", (10, 0), (20, 0))],
            [Point("P1", "AP1.20", 10, 0)]);

        var point = Assert.Single(analysis.Points);
        Assert.Equal(PipeDepthPointStatus.Shared, point.Status);
        Assert.Equal(new[] { "L1", "L2" }, point.PipeHandles);
        Assert.True(Assert.Single(analysis.ByHandle["L1"].Measurements).Shared);
        Assert.Equal(1.2, analysis.ByHandle["L1"].EndDepthMetres);
        Assert.Equal(1.2, analysis.ByHandle["L2"].StartDepthMetres);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NodeSharedWithoutAClearOwner_IsAmbiguousAndNotAssociated()
    {
        // Três linhas no mesmo ponto, e um ponto no meio de uma linha que também é ponta de outra.
        var analysis = Analyze(
            [
                Line("L1", (0, 0), (10, 0)),
                Line("L2", (10, 0), (20, 0)),
                Line("L3", (10, 0), (10, 10)),
                Line("L4", (30, 0), (40, 0), (50, 0)),
                Line("L5", (40, 0), (40, 10)),
            ],
            [Point("P1", "AP1.20", 10, 0), Point("P2", "AP1.20", 40, 0)]);

        Assert.All(analysis.Points, point => Assert.Equal(PipeDepthPointStatus.Ambiguous, point.Status));
        Assert.All(analysis.Pipes, pipe => Assert.Empty(pipe.Measurements));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DiameterFromTheCode_PicksTheRightLineAtAJunction()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0), layer: "RD_200"), Line("L2", (10, 0), (20, 0), layer: "RD_300")],
            [Point("P1", "AP1.20D300", 10, 0)]);

        var point = Assert.Single(analysis.Points);
        Assert.Equal(PipeDepthPointStatus.Associated, point.Status);
        Assert.Equal(new[] { "L2" }, point.PipeHandles);
        Assert.Empty(analysis.ByHandle["L1"].Measurements);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ConflictingTechnicalSources_AreNotAssociated()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0), layer: "GF-Rede Pluvial"), Line("L2", (0, 10), (10, 10), layer: "RD_200")],
            [Point("P1", "ES1.20D150", 0, 0), Point("P2", "AP1.20D300", 0, 10)]);

        Assert.Equal(
            new[] { PipeDepthPointStatus.NetworkConflict, PipeDepthPointStatus.DiameterConflict },
            analysis.Points.Select(point => point.Status));
        Assert.All(analysis.Pipes, pipe => Assert.Empty(pipe.Measurements));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PointsAtTheSamePlace_DuplicatesCountOnceAndDifferentValuesAreAConflict()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0)), Line("L2", (0, 10), (10, 10))],
            [
                Point("P1", "AP1.20", 0, 0),
                Point("P2", "AP1.20", 0, 0.0002),
                Point("P3", "AP1.20", 0, 10),
                Point("P4", "AP1.50", 0, 10),
            ]);

        Assert.Equal(
            new[]
            {
                PipeDepthPointStatus.Associated,
                PipeDepthPointStatus.Duplicate,
                PipeDepthPointStatus.PointConflict,
                PipeDepthPointStatus.PointConflict,
            },
            analysis.Points.Select(point => point.Status));
        Assert.Single(analysis.ByHandle["L1"].Measurements);
        Assert.Empty(analysis.ByHandle["L2"].Measurements);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RunningTwice_GivesTheSameResult()
    {
        GeoMapExportFeature[] lines = [Line("L1", (0, 0), (10, 0), (20, 0)), Line("L2", (20, 0), (30, 0))];
        SurveyPointInput[] points = [Point("P1", "AP1.20", 0, 0), Point("P2", "AP1.45", 20, 0), Point("P3", "X", 5, 0)];

        var first = Analyze(lines, points);
        var second = Analyze(lines, points);

        Assert.Equal(
            first.Pipes.SelectMany(pipe => pipe.Measurements),
            second.Pipes.SelectMany(pipe => pipe.Measurements));
        Assert.Equal(
            first.Points.Select(point => point.Status + ":" + string.Join(",", point.PipeHandles)),
            second.Points.Select(point => point.Status + ":" + string.Join(",", point.PipeHandles)));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DrawingInFeet_UsesTheToleranceInMetres()
    {
        var feet = PipeDepthOptions.ForUnits("foot");
        var analysis = PipeDepthAssociator.Analyze(
            [Line("L1", (0, 0), (100, 0))],
            [Point("P1", "AP1.20", 50, 0.002), Point("P2", "AP1.20", 70, 0.005)],
            feet);

        Assert.Equal(PipeDepthPointStatus.Associated, analysis.Points[0].Status);
        Assert.Equal(PipeDepthPointStatus.NearNotAssociated, analysis.Points[1].Status);
        Assert.Throws<InvalidOperationException>(() => PipeDepthOptions.ForUnits("degree"));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ManyLinesAndPoints_AreProcessedWithTheSpatialIndex()
    {
        // 25.000 linhas e 50.000 pontos: com um laço de todas as linhas contra todos os pontos seriam
        // 1,25 bilhão de comparações.
        var lines = new List<GeoMapExportFeature>(25_000);
        var points = new List<SurveyPointInput>(50_000);
        for (var index = 0; index < 25_000; index++)
        {
            var x = 700_000d + ((index % 250) * 100);
            var y = 7_080_000d + ((index / 250) * 100);
            lines.Add(Line($"L{index}", (x, y), (x + 20, y), (x + 40, y + 5), (x + 60, y + 5)));
            points.Add(Point($"A{index}", "AP1.20D300", x, y));
            points.Add(Point($"B{index}", "AP1.45D300", x + 40, y + 5));
        }

        var watch = Stopwatch.StartNew();
        var analysis = PipeDepthAssociator.Analyze(lines, points, Metres);
        watch.Stop();

        Assert.Equal(50_000, analysis.CountPoints(PipeDepthPointStatus.Associated));
        Assert.Equal(25_000, analysis.PipesWithMeasurements);
        Assert.All(analysis.Pipes, pipe => Assert.Equal(new[] { 0, 2 }, pipe.Measurements.Select(m => m.VertexIndex ?? -1)));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"Levou {watch.Elapsed}.");
    }

    [Theory]
    [InlineData("GF-02-EP01-18-530.26-001.raw.json", 38, 271, 96, 52, 2, 0, 32, 10, 23)]
    [InlineData("GF-02-EP01-22-530.26-001.raw.json", 110, 374, 279, 126, 15, 1, 119, 18, 62)]
    [Trait("Category", "FAST")]
    public void RealAuditDrawings_GiveTheCheckedCountsAndEveryAssociationIsOnTheLine(
        string file,
        int pipes,
        int points,
        int depthPoints,
        int associated,
        int shared,
        int ambiguous,
        int near,
        int noPipe,
        int pipesWithMeasurements)
    {
        // Desenhos reais da auditoria CAD → GIS (docs/audit), redes GF-Rede* sem as cópias -VIDEO.
        // As contagens foram conferidas com uma reimplementação independente (em Python) da mesma regra.
        var (lines, surveyPoints) = ReadAuditDrawing(file);

        var analysis = PipeDepthAssociator.Analyze(lines, surveyPoints, Metres);

        var actual = new[]
        {
            analysis.Pipes.Count, analysis.Points.Count, analysis.PipeDepthPoints,
            analysis.CountPoints(PipeDepthPointStatus.Associated), analysis.CountPoints(PipeDepthPointStatus.Shared),
            analysis.CountPoints(PipeDepthPointStatus.Ambiguous), analysis.CountPoints(PipeDepthPointStatus.NearNotAssociated),
            analysis.CountPoints(PipeDepthPointStatus.NoPipe), analysis.PipesWithMeasurements,
        };
        Assert.Equal(new[] { pipes, points, depthPoints, associated, shared, ambiguous, near, noPipe, pipesWithMeasurements }, actual);
        Assert.All(analysis.Pipes.SelectMany(pipe => pipe.Measurements), measurement =>
            Assert.True(measurement.Distance <= PipeDepthOptions.DefaultToleranceMetres));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void DiagnosticCsv_HasOneRowPerPointAndPerLine()
    {
        var analysis = Analyze(
            [Line("L1", (0, 0), (10, 0)), Line("L2", (0, 10), (10, 10))],
            [Point("P1", "AP1.20", 0, 0), Point("P;2", "ES\"1\"", 50, 50)]);

        var lines = PipeDepthReport.DiagnosticCsv(analysis).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(5, lines.Length);
        Assert.StartsWith("tipo;estado;ponto;", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("PONTO;ASSOCIADO;P1;", lines[1], StringComparison.Ordinal);
        Assert.Contains(";1,2;", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("PONTO;CODIGO_DESCONHECIDO;\"P;2\";", lines[2], StringComparison.Ordinal);
        Assert.StartsWith("TUBULACAO;COM_MEDICAO;", lines[3], StringComparison.Ordinal);
        Assert.StartsWith("TUBULACAO;SEM_MEDICAO;", lines[4], StringComparison.Ordinal);
        Assert.Contains(
            PipeDepthReport.SummaryLines(analysis),
            line => line.StartsWith("Tubulações com medições:", StringComparison.Ordinal) && line.EndsWith(" 1", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Export_CarriesEveryMeasurementAndMatchesTheSampleValidatedByGeoMap()
    {
        GeoMapExportFeature[] lines =
        [
            Line("2A7F", (688100, 7086400), (688110, 7086400), (688120, 7086400)) with
            {
                SourceId = "7c1e4b2a-3d5f-4a6b-9c8d-0e1f2a3b4c5d",
            },
            Line("2A80", (688120, 7086400), (688120, 7086380)) with
            {
                SourceId = "7c1e4b2a-3d5f-4a6b-9c8d-0e1f2a3b4c5e",
            },
        ];
        var analysis = PipeDepthAssociator.Analyze(
            lines,
            [
                Point("Pt1", "AP1.20D300", 688100, 7086400),
                Point("Pt2", "AP1.45D300", 688110, 7086400),
                Point("Pt3", "AP1.70D300", 688115, 7086400),
            ],
            Metres);
        var request = new GeoMapExportRequest(
            "EPSG:31982",
            "metre",
            "project-001",
            "Projeto exemplo",
            "project-001:GF-Rede Pluvial",
            "GF-Rede Pluvial",
            "stormwater",
            new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero),
            PipeDepthExport.Attach(lines, analysis),
            "#1F77B4");

        using var actual = JsonDocument.Parse(GeoMapExportSerializer.Serialize(request));
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "samples", "geomap", "depths-export.geomap.json")));
        Assert.Equal(
            JsonSerializer.Serialize(expected.RootElement),
            JsonSerializer.Serialize(actual.RootElement));
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(RepositoryRoot(), "samples", "geomap", "depths-export.geomap.json")),
            File.ReadAllBytes(Path.Combine(
                RepositoryRoot(), "..", "geomap", "contracts", "geomap", "0.2", "examples", "valid-geoautoengine-depths.geomap.json")));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Export_StopsInsteadOfDroppingMeasurementsThatDoNotFitInGeoMap()
    {
        var vertices = Enumerable.Range(0, 10_000).Select(index => ((double)index, 0d)).ToArray();
        var line = Line("L1", vertices);
        var points = vertices.Select((vertex, index) => Point($"P{index}", "AP1.20D300", vertex.Item1, vertex.Item2)).ToArray();
        var analysis = PipeDepthAssociator.Analyze([line], points, Metres);
        var request = new GeoMapExportRequest(
            "EPSG:31982", "metre", "p", "p", "p:n", "n", "stormwater", DateTimeOffset.UnixEpoch,
            PipeDepthExport.Attach([line], analysis));

        var error = Assert.Throws<InvalidOperationException>(() => GeoMapExportSerializer.Serialize(request));
        Assert.Contains("10000 medições", error.Message, StringComparison.Ordinal);
    }

    private static PipeDepthAnalysis Analyze(
        IReadOnlyList<GeoMapExportFeature> lines,
        IReadOnlyList<SurveyPointInput> points) => PipeDepthAssociator.Analyze(lines, points, Metres);

    private static GeoMapExportFeature Line(string handle, params (double X, double Y)[] vertices) =>
        Line(handle, vertices, "GF-Rede Pluvial");

    private static GeoMapExportFeature Line(string handle, (double X, double Y) start, (double X, double Y) end, string layer) =>
        Line(handle, [start, end], layer);

    private static GeoMapExportFeature Line(string handle, (double X, double Y)[] vertices, string layer) => new(
        $"00000000-0000-4000-8000-{handle.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF:D12}",
        handle,
        layer,
        vertices.Select(vertex => new GeoMapCoordinate(vertex.X, vertex.Y)).ToArray(),
        false,
        0,
        false);

    private static SurveyPointInput Point(string name, string description, double x, double y, string elevation = "") =>
        new($"H{name}", name, description, elevation, new GeoMapCoordinate(x, y), SurveyNodeSources.BlockPoint);

    private static (GeoMapExportFeature[] Lines, SurveyPointInput[] Points) ReadAuditDrawing(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(RepositoryRoot(), "docs", "audit", "cad_gis_raw", file)));
        var model = document.RootElement.GetProperty("spaces").EnumerateArray()
            .Single(space => space.GetProperty("name").GetString() == "*MODEL_SPACE");
        var lines = new List<GeoMapExportFeature>();
        var points = new List<SurveyPointInput>();
        foreach (var entity in model.GetProperty("entities").EnumerateArray())
        {
            var type = entity.GetProperty("object_type").GetString();
            var layer = entity.GetProperty("layer").GetString() ?? string.Empty;
            var handle = entity.GetProperty("handle").GetString() ?? string.Empty;
            if (type == "AcDbPolyline" &&
                layer.StartsWith("GF-Rede", StringComparison.Ordinal) &&
                !layer.EndsWith("-VIDEO", StringComparison.OrdinalIgnoreCase))
            {
                var vertices = entity.GetProperty("vertices").EnumerateArray()
                    .Select(vertex => vertex.GetProperty("point"))
                    .Select(point => new GeoMapCoordinate(point[0].GetDouble(), point[1].GetDouble()))
                    .ToArray();
                var closed = entity.TryGetProperty("closed", out var closedValue) && closedValue.GetBoolean();
                lines.Add(new GeoMapExportFeature(handle, handle, layer, vertices, closed, 0, false));
            }
            else if (type == "AcDbBlockReference" && layer.StartsWith("_TG", StringComparison.OrdinalIgnoreCase))
            {
                var attributes = entity.GetProperty("attributes").EnumerateArray().ToDictionary(
                    attribute => attribute.GetProperty("tag").GetString()!.ToUpperInvariant(),
                    attribute => attribute.GetProperty("text").GetString() ?? string.Empty);
                var position = entity.GetProperty("position");
                points.Add(new SurveyPointInput(
                    handle,
                    attributes.GetValueOrDefault("PONTO", handle),
                    attributes.GetValueOrDefault("DESC", string.Empty),
                    attributes.GetValueOrDefault("COTA", string.Empty),
                    new GeoMapCoordinate(position[0].GetDouble(), position[1].GetDouble()),
                    SurveyNodeSources.BlockPoint));
            }
        }

        return (lines.ToArray(), points.ToArray());
    }

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
}

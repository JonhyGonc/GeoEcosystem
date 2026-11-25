using System.Text.Json;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Tests;

// Profundidades gravadas no desenho (M7.2): o que gravar, correções à mão, cópia escondida e exportação.
public sealed class PipeDepthStorageTests
{
    private static readonly PipeDepthOptions Metres = PipeDepthOptions.ForUnits("metre");

    [Fact]
    [Trait("Category", "FAST")]
    public void Summary_IsTheSimpleAverageRoundedToTheCentimetre()
    {
        var analysis = Analyze(("AP1.00", 0), ("AP1.00", 10), ("AP1.01", 20));

        var summary = PipeDepthSummary.From(analysis.ByHandle["L1"]);

        Assert.NotNull(summary);
        Assert.Equal(1.0, summary.AverageMetres);
        Assert.Equal(1.0, summary.StartMetres);
        Assert.Equal(1.01, summary.EndMetres);
        Assert.Equal(3, summary.PointCount);
        Assert.Null(PipeDepthSummary.From(analysis.ByHandle["L2"]));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void FirstRun_CreatesOnlyLinesWithMeasurements()
    {
        var plan = PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0), ("AP1.40", 20)), Empty());

        var created = Item(plan, "L1");
        Assert.Equal(PipeDepthWriteAction.Create, created.Action);
        Assert.Equal(new PipeDepthFields(1.3, "1.20", "1.40", "2", PipeDepthOrigins.Automatic), created.Fields);
        Assert.Equal(2, created.Snapshot!.Measurements.Count);
        Assert.Equal(PipeDepthWriteAction.Nothing, Item(plan, "L2").Action);
        Assert.Single(plan.Writes);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RunningAgainWithoutChanges_WritesNothing()
    {
        var analysis = Analyze(("AP1.20", 0), ("AP1.40", 20));
        var stored = Apply(PipeDepthWritePlanner.Plan(analysis, Empty()), Empty());

        var again = PipeDepthWritePlanner.Plan(analysis, stored);

        Assert.Equal(PipeDepthWriteAction.Unchanged, Item(again, "L1").Action);
        Assert.Equal(PipeDepthWriteAction.Nothing, Item(again, "L2").Action);
        Assert.Empty(again.Writes);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NewSurveyValues_UpdateAnAutomaticLine()
    {
        var stored = Apply(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), Empty()), Empty());

        var plan = PipeDepthWritePlanner.Plan(Analyze(("AP1.50", 0)), stored);

        var item = Item(plan, "L1");
        Assert.Equal(PipeDepthWriteAction.Update, item.Action);
        Assert.Equal(1.5, item.Fields!.AverageMetres);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ValueEditedByHand_IsKeptAndMarkedManual()
    {
        var stored = Apply(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), Empty()), Empty());
        stored["L1"] = stored["L1"] with { Fields = stored["L1"].Fields! with { AverageMetres = 1.35 } };

        var plan = PipeDepthWritePlanner.Plan(Analyze(("AP1.50", 0)), stored);

        var item = Item(plan, "L1");
        Assert.Equal(PipeDepthWriteAction.MarkManual, item.Action);
        Assert.Equal(1.35, item.Fields!.AverageMetres);
        Assert.Equal(PipeDepthOrigins.Manual, item.Fields.Origin);
        Assert.Null(item.Snapshot);

        var next = PipeDepthWritePlanner.Plan(Analyze(("AP1.50", 0)), Apply(plan, stored));
        Assert.Equal(PipeDepthWriteAction.KeepManual, Item(next, "L1").Action);
        Assert.Empty(next.Writes);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void SameValueTypedWithComma_IsNotAManualEdit()
    {
        var stored = Apply(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0), ("AP1.40", 20)), Empty()), Empty());
        stored["L1"] = stored["L1"] with { Fields = stored["L1"].Fields! with { Start = " 1,2 " } };

        var plan = PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0), ("AP1.40", 20)), stored);

        Assert.Equal(PipeDepthWriteAction.Unchanged, Item(plan, "L1").Action);
    }

    [Theory]
    [InlineData("MANUAL", PipeDepthWriteAction.KeepManual)]
    [InlineData("manual", PipeDepthWriteAction.KeepManual)]
    [InlineData("RECALCULAR", PipeDepthWriteAction.Update)]
    [Trait("Category", "FAST")]
    public void OriginTypedByTheUser_DecidesWhetherThePluginMayRewrite(string origin, PipeDepthWriteAction expected)
    {
        var stored = Apply(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), Empty()), Empty());
        stored["L1"] = stored["L1"] with { Fields = stored["L1"].Fields! with { AverageMetres = 2, Origin = origin } };

        Assert.Equal(expected, Item(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), stored), "L1").Action);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void FieldsThePluginDidNotWrite_AreTreatedAsManual()
    {
        var stored = Empty();
        stored["L1"] = new PipeDepthStoredState(
            new PipeDepthFields(1.8, string.Empty, string.Empty, "1", PipeDepthOrigins.Automatic),
            null);

        var item = Item(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), stored), "L1");

        Assert.Equal(PipeDepthWriteAction.MarkManual, item.Action);
        Assert.Equal(1.8, item.Fields!.AverageMetres);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LineThatLostItsPoints_IsMarkedWithoutMeasurementInsteadOfDeleted()
    {
        var stored = Apply(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), Empty()), Empty());

        var plan = PipeDepthWritePlanner.Plan(Analyze(), stored);

        var item = Item(plan, "L1");
        Assert.Equal(PipeDepthWriteAction.MarkWithoutMeasurement, item.Action);
        Assert.Equal(PipeDepthFields.WithoutMeasurement(), item.Fields);
        Assert.Null(item.Fields!.ToSummary());

        var again = PipeDepthWritePlanner.Plan(Analyze(), Apply(plan, stored));
        Assert.Equal(PipeDepthWriteAction.Unchanged, Item(again, "L1").Action);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ValuesTypedIntoALineWithoutMeasurement_AreManual()
    {
        var first = Apply(PipeDepthWritePlanner.Plan(Analyze(("AP1.20", 0)), Empty()), Empty());
        var stored = Apply(PipeDepthWritePlanner.Plan(Analyze(), first), first);
        stored["L1"] = stored["L1"] with { Fields = stored["L1"].Fields! with { AverageMetres = 0.9, Points = "1" } };

        Assert.Equal(PipeDepthWriteAction.MarkManual, Item(PipeDepthWritePlanner.Plan(Analyze(), stored), "L1").Action);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Snapshot_SurvivesTheTripThroughTheDrawingInAsciiPieces()
    {
        var analysis = Analyze(Enumerable.Range(0, 3).Select(index => ($"AP1.{20 + index}", index * 10d)).ToArray());
        var snapshot = PipeDepthSnapshot.From(analysis.ByHandle["L1"], 1);

        var chunks = PipeDepthSnapshotCodec.Encode(snapshot);
        var decoded = PipeDepthSnapshotCodec.Decode(chunks);

        Assert.True(snapshot.SameAs(decoded));
        Assert.All(chunks, chunk =>
        {
            Assert.InRange(chunk.Length, 1, PipeDepthSnapshotCodec.ChunkLength);
            Assert.All(chunk, character => Assert.True(character < 128));
        });
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LongSnapshot_IsSplitAndJoinedBack()
    {
        var measurements = Enumerable.Range(0, 200)
            .Select(index => new PipeDepthSnapshotMeasurement(
                $"H{index}", $"Ponto ção {index}", 1.2, index, index, "AP1.20D300", PipeDepthMethods.Vertex, false))
            .ToArray();
        var snapshot = new PipeDepthSnapshot(new PipeDepthSummary(1.2, 1.2, 1.2, 200), measurements);

        var chunks = PipeDepthSnapshotCodec.Encode(snapshot);

        Assert.True(chunks.Count > 1);
        Assert.True(snapshot.SameAs(PipeDepthSnapshotCodec.Decode(chunks)));
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("{\"versao\":2,\"pontos\":0,\"medicoes\":[]}")]
    [Trait("Category", "FAST")]
    public void BrokenOrUnknownSnapshot_IsIgnored(string text)
    {
        Assert.Null(PipeDepthSnapshotCodec.Decode([text]));
    }

    [Theory]
    [InlineData("1,25", 1.25)]
    [InlineData(" 1.25 ", 1.25)]
    [InlineData("", null)]
    [Trait("Category", "FAST")]
    public void TypedNumbers_AcceptCommaAndBlank(string text, double? expected)
    {
        Assert.True(PipeDepthFields.TryParse(text, out var value));
        Assert.Equal(expected, value);
        Assert.False(PipeDepthFields.TryParse("abc", out _));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Export_FromTheDrawingCarriesManualValuesAndTheirOrigin()
    {
        GeoMapExportFeature[] lines = [Line("L1", (0, 0), (10, 0)), Line("L2", (0, 10), (10, 10)), Line("L3", (0, 20), (10, 20))];
        var stored = new Dictionary<string, PipeDepthFields>(StringComparer.Ordinal)
        {
            ["L1"] = new(1.35, "1,30", "", "2", "manual"),
            ["L2"] = PipeDepthFields.WithoutMeasurement(),
            ["L3"] = new(0.8, "0.80", "0.80", "1", PipeDepthOrigins.Automatic),
        };
        var request = new GeoMapExportRequest(
            "EPSG:31982", "metre", "p", "p", "p:n", "n", "stormwater", DateTimeOffset.UnixEpoch,
            PipeDepthExport.FromStored(lines, stored));

        using var document = JsonDocument.Parse(GeoMapExportSerializer.Serialize(request));
        var features = document.RootElement.GetProperty("features");
        var manual = features[0].GetProperty("properties");
        Assert.Equal(1.35, manual.GetProperty("profundidadeMediaM").GetDouble());
        Assert.Equal(1.3, manual.GetProperty("profundidadeInicialM").GetDouble());
        Assert.False(manual.TryGetProperty("profundidadeFinalM", out _));
        Assert.Equal(PipeDepthOrigins.Manual, manual.GetProperty("profundidadeOrigem").GetString());
        Assert.False(features[1].GetProperty("properties").TryGetProperty("profundidadeMediaM", out _));
        Assert.Equal(PipeDepthOrigins.Automatic, features[2].GetProperty("properties").GetProperty("profundidadeOrigem").GetString());
    }

    // L1: (0,0) → (10,0) → (20,0); L2: longe, sem pontos. Cada ponto é (código, x) sobre L1.
    private static PipeDepthAnalysis Analyze(params (string Code, double X)[] points) => PipeDepthAssociator.Analyze(
        [Line("L1", (0, 0), (10, 0), (20, 0)), Line("L2", (0, 50), (10, 50))],
        points.Select((point, index) => new SurveyPointInput(
            $"H{index}", $"P{index}", point.Code, string.Empty, new GeoMapCoordinate(point.X, 0), SurveyNodeSources.BlockPoint))
            .ToArray(),
        Metres);

    private static Dictionary<string, PipeDepthStoredState> Empty() => new(StringComparer.Ordinal);

    // O que o desenho guarda depois de aplicar o plano (campos e, quando ela muda, a cópia escondida).
    private static Dictionary<string, PipeDepthStoredState> Apply(
        PipeDepthWritePlan plan,
        Dictionary<string, PipeDepthStoredState> previous)
    {
        var stored = new Dictionary<string, PipeDepthStoredState>(previous, StringComparer.Ordinal);
        foreach (var item in plan.Writes)
        {
            stored.TryGetValue(item.Handle, out var before);
            stored[item.Handle] = new PipeDepthStoredState(item.Fields, item.Snapshot ?? before?.Snapshot);
        }

        return stored;
    }

    private static PipeDepthWriteItem Item(PipeDepthWritePlan plan, string handle) =>
        plan.Items.Single(item => item.Handle == handle);

    private static GeoMapExportFeature Line(string handle, params (double X, double Y)[] vertices) => new(
        $"00000000-0000-4000-8000-{handle.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF:D12}",
        handle,
        "GF-Rede Pluvial",
        vertices.Select(vertex => new GeoMapCoordinate(vertex.X, vertex.Y)).ToArray(),
        false,
        0,
        false);
}

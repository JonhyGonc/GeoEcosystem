using System.Text.Json;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Tests;

// Tipo das peças pelo nome do bloco: lista de nomes, gravação sem perder o manual e exportação (tipoPeca).
public sealed class FittingTypesTests
{
    [Theory]
    [InlineData("TE", "Tê")]
    [InlineData("TÊ", "Tê")]
    [InlineData("te_100", "Tê")]
    [InlineData("TEE-AF", "Tê")]
    [InlineData("CRUZETA", "Cruzeta")]
    [InlineData("CRUZ-50", "Cruzeta")]
    [InlineData("RG", "Registro")]
    [InlineData("REGISTRO GAVETA", "Registro")]
    [InlineData("VT", "Ventosa")]
    [InlineData("VENTOSA", "Ventosa")]
    [InlineData("REDUÇÃO", "Redução")]
    [InlineData("RED_150X100", "Redução")]
    [InlineData("CURVA 90", "Curva")]
    [InlineData("TERMINAL", null)]
    [InlineData("REDE", null)]
    [InlineData("XYZ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [Trait("Category", "FAST")]
    public void BlockName_GivesTheFittingType(string? blockName, string? expected)
    {
        Assert.Equal(expected, FittingTypes.Resolve(blockName));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void FirstRun_WritesKnownNamesAndListsTheUnknownOnes()
    {
        var plan = FittingWritePlanner.Plan([("1", "TE"), ("2", "TE"), ("3", "XYZ")], Empty());

        Assert.Equal(
            new[] { FittingWriteAction.Create, FittingWriteAction.Create, FittingWriteAction.UnknownName },
            plan.Items.Select(item => item.Action));
        Assert.Equal(2, plan.Writes.Count);
        var summary = plan.SummaryLines();
        Assert.Contains(summary, line => line.StartsWith("  Tê:", StringComparison.Ordinal) && line.EndsWith(" 2", StringComparison.Ordinal));
        Assert.Contains(summary, line => line.StartsWith("  XYZ:", StringComparison.Ordinal) && line.EndsWith(" 1", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RunningAgain_WritesNothing()
    {
        var stored = Apply(FittingWritePlanner.Plan([("1", "TE")], Empty()), Empty());

        var again = FittingWritePlanner.Plan([("1", "TE")], stored);

        Assert.Equal(FittingWriteAction.Unchanged, Assert.Single(again.Items).Action);
        Assert.Empty(again.Writes);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void TypeEditedByHand_IsKeptAndMarkedManual()
    {
        var stored = Apply(FittingWritePlanner.Plan([("1", "TE")], Empty()), Empty());
        stored["1"] = stored["1"] with { Type = "Registro" };

        var plan = FittingWritePlanner.Plan([("1", "TE")], stored);

        var item = Assert.Single(plan.Items);
        Assert.Equal(FittingWriteAction.MarkManual, item.Action);
        Assert.Equal("Registro", item.Type);
        var next = FittingWritePlanner.Plan([("1", "TE")], Apply(plan, stored));
        Assert.Equal(FittingWriteAction.KeepManual, Assert.Single(next.Items).Action);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Recalculate_TurnsAManualTypeBackToAutomatic()
    {
        var stored = Empty();
        stored["1"] = new FittingStoredState("Registro", PipeDepthOrigins.Recalculate, "Tê");

        var item = Assert.Single(FittingWritePlanner.Plan([("1", "TE")], stored).Items);

        Assert.Equal(FittingWriteAction.Update, item.Action);
        Assert.Equal("Tê", item.Type);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void TypeThePluginDidNotWrite_IsTreatedAsManual()
    {
        var stored = Empty();
        stored["1"] = new FittingStoredState("Ventosa", PipeDepthOrigins.Automatic, null);

        Assert.Equal(FittingWriteAction.MarkManual, Assert.Single(FittingWritePlanner.Plan([("1", "TE")], stored).Items).Action);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Export_UsesTheStoredTypeFirstThenTheBlockName()
    {
        GeoMapExportFeature[] features =
        [
            Block("A1", "TE"),
            Block("A2", "TE"),
            Block("A3", "XYZ"),
        ];
        var stored = new Dictionary<string, string>(StringComparer.Ordinal) { ["A2"] = "Registro" };
        var request = new GeoMapExportRequest(
            "EPSG:31982", "metre", "p", "p", "p:n", "n", "water", DateTimeOffset.UnixEpoch,
            FittingExport.Apply(features, stored));

        using var document = JsonDocument.Parse(GeoMapExportSerializer.Serialize(request));
        var properties = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties"))
            .ToArray();
        Assert.Equal("Tê", properties[0].GetProperty("tipoPeca").GetString());
        Assert.Equal("Registro", properties[1].GetProperty("tipoPeca").GetString());
        Assert.False(properties[2].TryGetProperty("tipoPeca", out _));
    }

    private static Dictionary<string, FittingStoredState> Empty() => new(StringComparer.Ordinal);

    private static Dictionary<string, FittingStoredState> Apply(
        FittingWritePlan plan,
        Dictionary<string, FittingStoredState> previous)
    {
        var stored = new Dictionary<string, FittingStoredState>(previous, StringComparer.Ordinal);
        foreach (var item in plan.Writes)
        {
            stored.TryGetValue(item.Handle, out var before);
            stored[item.Handle] = item.Action == FittingWriteAction.MarkManual
                ? new FittingStoredState(item.Type, PipeDepthOrigins.Manual, before?.WrittenType)
                : new FittingStoredState(item.Type, PipeDepthOrigins.Automatic, item.Type);
        }

        return stored;
    }

    private static GeoMapExportFeature Block(string handle, string name) => new(
        $"00000000-0000-4000-8000-{handle.GetHashCode(StringComparison.Ordinal) & 0x7FFFFFFF:D12}",
        handle,
        "PECAS",
        [new GeoMapCoordinate(688100, 7086400)],
        false,
        0,
        false,
        new GeoMapBlockInfo(name, 0, 1, 1, false));
}

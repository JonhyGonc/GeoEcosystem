using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Tests;

// Código próprio de cada feição exportada para o GeoMap (DEC-ECO-0003, D-04).
public sealed class GeoMapFeatureIdentityTests
{
    private static readonly Guid First = Guid.Parse("0e0c0000-0000-4000-8000-0000000000f1");
    private static readonly Guid Second = Guid.Parse("0e0c0000-0000-4000-8000-0000000000f2");

    [Fact]
    [Trait("Category", "FAST")]
    public void FirstExport_AssignsANewIdToEveryEntity()
    {
        var result = GeoMapFeatureIdentity.Resolve(
            [new("2A7F", null), new("2A80", null)],
            Sequence(First, Second));

        Assert.Equal([First, Second], result.Select(item => item.FeatureId));
        Assert.All(result, item => Assert.Equal(GeoMapFeatureIdStatus.Assigned, item.Status));
        Assert.All(result, item => Assert.True(item.MustBeWritten));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Reexport_KeepsTheExistingIdsAndWritesNothing()
    {
        // Também vale para Polylines movidas ou editadas: a XData acompanha a entidade.
        var result = GeoMapFeatureIdentity.Resolve(
            [new("2A7F", First), new("2A80", Second)],
            () => throw new InvalidOperationException("Não deveria gerar código."));

        Assert.Equal([First, Second], result.Select(item => item.FeatureId));
        Assert.All(result, item => Assert.Equal(GeoMapFeatureIdStatus.Kept, item.Status));
        Assert.All(result, item => Assert.False(item.MustBeWritten));
    }

    [Theory]
    [InlineData("2A7F", "3B10")] // cópia: a nova entidade tem handle maior
    [InlineData("3B10", "2A7F")] // a ordem da seleção não importa
    [Trait("Category", "FAST")]
    public void CopyOrBreak_TheOldestHandleKeepsTheIdAndTheOthersAreRenewed(string firstSelected, string secondSelected)
    {
        var result = GeoMapFeatureIdentity.Resolve(
            [new(firstSelected, First), new(secondSelected, First)],
            Sequence(Second));

        var original = Assert.Single(result, item => item.CadHandle == "2A7F");
        var copy = Assert.Single(result, item => item.CadHandle == "3B10");
        Assert.Equal(First, original.FeatureId);
        Assert.Equal(GeoMapFeatureIdStatus.Kept, original.Status);
        Assert.Equal(Second, copy.FeatureId);
        Assert.Equal(GeoMapFeatureIdStatus.Renewed, copy.Status);
        Assert.True(copy.MustBeWritten);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void HandlesAreComparedAsHexadecimalNumbers()
    {
        // "FF" (255) é mais antigo que "100" (256), embora venha depois em ordem alfabética.
        var result = GeoMapFeatureIdentity.Resolve([new("100", First), new("FF", First)], Sequence(Second));

        Assert.Equal(First, Assert.Single(result, item => item.CadHandle == "FF").FeatureId);
        Assert.Equal(Second, Assert.Single(result, item => item.CadHandle == "100").FeatureId);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NewIds_NeverRepeatAnIdAlreadyInTheSelection()
    {
        var result = GeoMapFeatureIdentity.Resolve(
            [new("10", First), new("11", null)],
            Sequence(First, Guid.Empty, Second));

        Assert.Equal(Second, result[1].FeatureId);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void XData_RoundTripsAndIgnoresInvalidValues()
    {
        var values = GeoMapFeatureIdentity.XDataValues(First);

        Assert.Equal(["SCHEMA_VERSION=1", $"FEATURE_ID={First:D}"], values);
        Assert.Equal(First, GeoMapFeatureIdentity.ParseXData(values));
        Assert.Null(GeoMapFeatureIdentity.ParseXData(null));
        Assert.Null(GeoMapFeatureIdentity.ParseXData(["FEATURE_ID=não-é-guid"]));
        Assert.Null(GeoMapFeatureIdentity.ParseXData([$"FEATURE_ID={Guid.Empty:D}"]));
        Assert.Throws<ArgumentException>(() => GeoMapFeatureIdentity.XDataValues(Guid.Empty));
    }

    private static Func<Guid> Sequence(params Guid[] values)
    {
        var queue = new Queue<Guid>(values);
        return () => queue.Dequeue();
    }
}

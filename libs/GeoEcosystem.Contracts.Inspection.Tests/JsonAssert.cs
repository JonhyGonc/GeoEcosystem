using System.Text.Json;

namespace GeoEcosystem.Contracts.Inspection.Tests;

// Compara a estrutura de dois JSON: mesmos nomes de propriedade (em qualquer ordem) e mesmos
// valores; números pelo valor decimal (12.5 == 12.50).
internal static class JsonAssert
{
    public static void Equivalent(byte[] expected, byte[] actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Equivalent(expectedDocument.RootElement, actualDocument.RootElement, "$");
    }

    private static void Equivalent(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(
            expected.ValueKind == actual.ValueKind,
            $"{path}: esperado {expected.ValueKind}, obtido {actual.ValueKind}.");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(property => property.Name)
                    .OrderBy(property => property, StringComparer.Ordinal).ToArray();
                var actualNames = actual.EnumerateObject().Select(property => property.Name)
                    .OrderBy(property => property, StringComparer.Ordinal).ToArray();
                Assert.True(
                    expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal),
                    $"{path}: propriedades esperadas [{string.Join(", ", expectedNames)}], " +
                    $"obtidas [{string.Join(", ", actualNames)}].");
                foreach (var property in expected.EnumerateObject())
                {
                    Equivalent(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                }

                break;
            case JsonValueKind.Array:
                Assert.True(
                    expected.GetArrayLength() == actual.GetArrayLength(),
                    $"{path}: tamanho esperado {expected.GetArrayLength()}, obtido {actual.GetArrayLength()}.");
                var index = 0;
                foreach (var (expectedItem, actualItem) in expected.EnumerateArray().Zip(actual.EnumerateArray()))
                {
                    Equivalent(expectedItem, actualItem, $"{path}[{index++}]");
                }

                break;
            case JsonValueKind.Number:
                Assert.True(
                    expected.GetDecimal() == actual.GetDecimal(),
                    $"{path}: esperado {expected.GetRawText()}, obtido {actual.GetRawText()}.");
                break;
            case JsonValueKind.String:
                Assert.True(
                    string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal),
                    $"{path}: esperado \"{expected.GetString()}\", obtido \"{actual.GetString()}\".");
                break;
        }
    }
}

using System.Globalization;
using System.Text.Json;

namespace GeoMap.Importer;

public sealed record GeoMapImportDocument(
    string SourceSystem,
    int SourceSrid,
    string Units,
    ImportProject Project,
    ImportNetwork Network,
    DateTimeOffset ExportedAt,
    IReadOnlyList<GeoMapImportFeature> Features)
{
    public static GeoMapImportDocument Parse(ReadOnlyMemory<byte> utf8Json)
    {
        using var document = JsonDocument.Parse(utf8Json);
        var root = document.RootElement;
        var sourceCrs = root.GetProperty("sourceCrs").GetString()!;

        var project = root.GetProperty("project");
        var network = root.GetProperty("network");
        var features = root.GetProperty("features")
            .EnumerateArray()
            .Select((feature, index) => new GeoMapImportFeature(
                index,
                OptionalString(feature, "sourceId"),
                feature.GetProperty("sourceType").GetString()!,
                OptionalString(feature, "sourceLayer"),
                feature.GetProperty("geometry").GetRawText(),
                feature.GetProperty("properties").GetRawText()))
            .ToArray();

        return new(
            root.GetProperty("sourceSystem").GetString()!,
            int.Parse(sourceCrs.AsSpan("EPSG:".Length), CultureInfo.InvariantCulture),
            root.GetProperty("units").GetString()!,
            new(
                project.GetProperty("externalId").GetString()!,
                project.GetProperty("name").GetString()!),
            new(
                network.GetProperty("externalId").GetString()!,
                network.GetProperty("name").GetString()!,
                NormalizeNetworkType(network.GetProperty("type").GetString()!)),
            DateTimeOffset.Parse(
                root.GetProperty("exportedAt").GetString()!,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            features);
    }

    public static int CountReceivedFeatures(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("features", out var features) &&
                   features.ValueKind == JsonValueKind.Array
                ? features.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;

    private static string NormalizeNetworkType(string value) =>
        value.Trim().ToLowerInvariant();
}

public sealed record ImportProject(string ExternalId, string Name);

public sealed record ImportNetwork(string ExternalId, string Name, string Type);

public sealed record GeoMapImportFeature(
    int Sequence,
    string? SourceId,
    string SourceType,
    string? SourceLayer,
    string GeometryJson,
    string PropertiesJson);

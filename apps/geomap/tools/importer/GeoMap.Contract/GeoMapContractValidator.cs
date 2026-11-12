using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GeoMap.Contract;

public static partial class GeoMapContractValidator
{
    public const string SupportedVersion = "0.1";
    public const string ColorVersion = "0.2";
    public const string PointVersion = "0.3";
    public const int MaximumDocumentBytes = 50 * 1024 * 1024;
    public const int MaximumFeatures = 10_000;
    public const int MaximumVerticesPerFeature = 100_000;
    public const int MaximumProperties = 128;
    public const int MaximumPropertiesBytes = 64 * 1024;
    public const int MaximumPropertyDepth = 4;
    public const int MaximumPropertyStringLength = 4_096;
    public const int MaximumStreamingMetadataBytes = 64 * 1024;
    public const int MaximumStreamingFeatureBytes = 16 * 1024 * 1024;
    public const int MaximumStreamingFeatures = 5_000_000;

    private static readonly HashSet<string> SupportedUnits =
        ["metre", "foot", "us-survey-foot", "degree"];
    private static readonly HashSet<string> RootFields =
        ["contractVersion", "sourceSystem", "sourceCrs", "units", "project", "network", "exportedAt", "features"];
    private static readonly HashSet<string> MetadataFields =
        ["contractVersion", "sourceSystem", "sourceCrs", "units", "project", "network", "exportedAt"];
    private static readonly HashSet<string> ProjectFields = ["externalId", "name"];
    private static readonly HashSet<string> NetworkFields = ["externalId", "name", "type"];
    private static readonly HashSet<string> NetworkFieldsV02 = ["externalId", "name", "type", "color"];
    private static readonly HashSet<string> SupportedVersions = [SupportedVersion, ColorVersion, PointVersion];

    /// <summary>A partir da 0.3, feições podem ser Point ou MultiPoint (blocos do CAD).</summary>
    public static bool AllowsPoints(string? contractVersion) => contractVersion == PointVersion;

    private static bool AllowsColor(string? contractVersion) =>
        contractVersion is ColorVersion or PointVersion;
    private static readonly HashSet<string> FeatureFields =
        ["sourceId", "sourceType", "sourceLayer", "geometry", "properties"];
    private static readonly HashSet<string> GeometryFields = ["type", "coordinates"];

    public static ContractValidationResult Validate(ReadOnlyMemory<byte> utf8Json)
    {
        var errors = new List<ContractValidationError>();
        if (utf8Json.Length > MaximumDocumentBytes)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, "$", "Document exceeds 50 MiB.");
            return new(errors);
        }

        try
        {
            using var document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
            ValidateRoot(document.RootElement, errors);
        }
        catch (JsonException exception)
        {
            Add(
                errors,
                ContractErrorCodes.DocumentInvalid,
                "$",
                $"Invalid UTF-8 JSON document: {exception.Message}");
        }

        return new(errors);
    }

    public static ContractValidationResult ValidateStreamingMetadata(
        ReadOnlyMemory<byte> utf8Json)
    {
        var errors = new List<ContractValidationError>();
        if (utf8Json.Length > MaximumStreamingMetadataBytes)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, "$", "Streaming metadata exceeds 64 KiB.");
            return new(errors);
        }

        try
        {
            using var document = ParseJson(utf8Json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Add(errors, ContractErrorCodes.DocumentInvalid, "$", "Metadata record must be an object.");
            }
            else
            {
                ValidateKnownFields(root, MetadataFields, "$", errors);
                ValidateMetadataFields(root, errors);
            }
        }
        catch (JsonException exception)
        {
            Add(errors, ContractErrorCodes.DocumentInvalid, "$", $"Invalid UTF-8 JSON metadata: {exception.Message}");
        }

        return new(errors);
    }

    public static ContractValidationResult ValidateStreamingFeature(
        ReadOnlyMemory<byte> utf8Json,
        int index,
        string contractVersion = SupportedVersion)
    {
        var errors = new List<ContractValidationError>();
        var path = $"$.features[{index}]";
        if (utf8Json.Length > MaximumStreamingFeatureBytes)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, path, "Streaming feature record exceeds 16 MiB.");
            return new(errors);
        }

        try
        {
            using var document = ParseJson(utf8Json);
            ValidateFeature(
                document.RootElement,
                index,
                new HashSet<string>(StringComparer.Ordinal),
                AllowsPoints(contractVersion),
                errors);
        }
        catch (JsonException exception)
        {
            Add(errors, ContractErrorCodes.DocumentInvalid, path, $"Invalid UTF-8 JSON feature: {exception.Message}");
        }

        return new(errors);
    }

    private static JsonDocument ParseJson(ReadOnlyMemory<byte> utf8Json) =>
        JsonDocument.Parse(
            utf8Json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });

    private static void ValidateRoot(
        JsonElement root,
        List<ContractValidationError> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Add(errors, ContractErrorCodes.DocumentInvalid, "$", "Root must be an object.");
            return;
        }

        ValidateKnownFields(root, RootFields, "$", errors);
        ValidateMetadataFields(root, errors);
        var version = root.TryGetProperty("contractVersion", out var versionElement) &&
                      versionElement.ValueKind == JsonValueKind.String
            ? versionElement.GetString()
            : null;
        ValidateFeatures(root, AllowsPoints(version), errors);
    }

    private static void ValidateMetadataFields(
        JsonElement root,
        List<ContractValidationError> errors)
    {
        var version = RequiredString(root, "contractVersion", 16, "$", errors);
        if (version is not null && !SupportedVersions.Contains(version))
        {
            Add(errors, ContractErrorCodes.VersionUnsupported, "$.contractVersion", "Only contract versions 0.1, 0.2 and 0.3 are supported.");
        }

        RequiredString(root, "sourceSystem", 100, "$", errors);
        var sourceCrs = RequiredString(root, "sourceCrs", 16, "$", errors);
        if (sourceCrs is not null && !EpsgIdentifier().IsMatch(sourceCrs))
        {
            Add(errors, ContractErrorCodes.CrsInvalid, "$.sourceCrs", "CRS must use the EPSG:<code> form.");
        }

        var units = RequiredString(root, "units", 32, "$", errors);
        if (units is not null && !SupportedUnits.Contains(units))
        {
            Add(errors, ContractErrorCodes.UnitsUnsupported, "$.units", "Declared unit is not supported by the contract.");
        }

        var exportedAt = RequiredString(root, "exportedAt", 64, "$", errors);
        if (exportedAt is not null &&
            (!exportedAt.EndsWith('Z') ||
             !DateTimeOffset.TryParse(
                 exportedAt,
                 CultureInfo.InvariantCulture,
                 DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                 out _)))
        {
            Add(errors, ContractErrorCodes.FieldInvalid, "$.exportedAt", "Export time must be an ISO 8601 UTC timestamp ending in Z.");
        }

        ValidateProjectOrNetwork(root, "project", ProjectFields, false, errors);
        var networkFields = AllowsColor(version) ? NetworkFieldsV02 : NetworkFields;
        ValidateProjectOrNetwork(root, "network", networkFields, true, errors);
        if (AllowsColor(version)) ValidateNetworkColor(root, errors);
    }

    private static void ValidateNetworkColor(
        JsonElement root,
        List<ContractValidationError> errors)
    {
        if (!root.TryGetProperty("network", out var network) ||
            network.ValueKind != JsonValueKind.Object ||
            !network.TryGetProperty("color", out var color))
        {
            return;
        }

        if (color.ValueKind != JsonValueKind.String || !HexColor().IsMatch(color.GetString()!))
        {
            Add(errors, ContractErrorCodes.FieldInvalid, "$.network.color", "Color must use the #RRGGBB form.");
        }
    }

    private static void ValidateProjectOrNetwork(
        JsonElement root,
        string field,
        HashSet<string> knownFields,
        bool requiresType,
        List<ContractValidationError> errors)
    {
        var path = $"$.{field}";
        if (!root.TryGetProperty(field, out var value))
        {
            Add(errors, ContractErrorCodes.RequiredField, path, "Required object is missing.");
            return;
        }
        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, path, "Value must be an object.");
            return;
        }

        ValidateKnownFields(value, knownFields, path, errors);
        RequiredString(value, "externalId", 255, path, errors);
        RequiredString(value, "name", 200, path, errors);
        if (requiresType) RequiredString(value, "type", 100, path, errors);
    }

    private static void ValidateFeatures(
        JsonElement root,
        bool allowPoints,
        List<ContractValidationError> errors)
    {
        if (!root.TryGetProperty("features", out var features))
        {
            Add(errors, ContractErrorCodes.RequiredField, "$.features", "Required array is missing.");
            return;
        }
        if (features.ValueKind != JsonValueKind.Array)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, "$.features", "Value must be an array.");
            return;
        }

        var count = features.GetArrayLength();
        if (count == 0)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, "$.features", "At least one feature is required.");
            return;
        }
        if (count > MaximumFeatures)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, "$.features", "Document exceeds 10,000 features.");
        }

        var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var feature in features.EnumerateArray())
        {
            if (index >= MaximumFeatures) break;
            ValidateFeature(feature, index, sourceKeys, allowPoints, errors);
            index++;
        }
    }

    private static void ValidateFeature(
        JsonElement feature,
        int index,
        HashSet<string> sourceKeys,
        bool allowPoints,
        List<ContractValidationError> errors)
    {
        var path = $"$.features[{index}]";
        if (feature.ValueKind != JsonValueKind.Object)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, path, "Feature must be an object.");
            return;
        }

        ValidateKnownFields(feature, FeatureFields, path, errors);
        var sourceId = OptionalString(feature, "sourceId", 255, path, errors);
        var sourceType = RequiredString(feature, "sourceType", 100, path, errors);
        OptionalString(feature, "sourceLayer", 255, path, errors);
        if (sourceId is not null && sourceType is not null &&
            !sourceKeys.Add($"{sourceType}\0{sourceId}"))
        {
            Add(errors, ContractErrorCodes.SourceIdDuplicate, $"{path}.sourceId", "sourceId must be unique for each sourceType in the document.");
        }

        ValidateGeometry(feature, path, allowPoints, errors);
        ValidateProperties(feature, path, errors);
    }

    private static void ValidateGeometry(
        JsonElement feature,
        string featurePath,
        bool allowPoints,
        List<ContractValidationError> errors)
    {
        var path = $"{featurePath}.geometry";
        if (!feature.TryGetProperty("geometry", out var geometry))
        {
            Add(errors, ContractErrorCodes.RequiredField, path, "Geometry is required.");
            return;
        }
        if (geometry.ValueKind != JsonValueKind.Object)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, path, "Geometry must be an object.");
            return;
        }

        ValidateKnownFields(geometry, GeometryFields, path, errors);
        var type = RequiredString(geometry, "type", 32, path, errors);
        if (!geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array)
        {
            Add(errors, ContractErrorCodes.RequiredField, $"{path}.coordinates", "Coordinate array is required.");
            return;
        }

        var vertices = 0;
        if (type == "LineString")
        {
            ValidateLine(coordinates, $"{path}.coordinates", ref vertices, errors);
        }
        else if (type == "MultiLineString")
        {
            if (coordinates.GetArrayLength() == 0)
            {
                Add(errors, ContractErrorCodes.GeometryEmpty, $"{path}.coordinates", "MultiLineString requires at least one line.");
            }
            var lineIndex = 0;
            foreach (var line in coordinates.EnumerateArray())
            {
                ValidateLine(line, $"{path}.coordinates[{lineIndex}]", ref vertices, errors);
                lineIndex++;
            }
        }
        else if (allowPoints && type == "Point")
        {
            vertices++;
            ValidatePosition(coordinates, $"{path}.coordinates", errors);
        }
        else if (allowPoints && type == "MultiPoint")
        {
            if (coordinates.GetArrayLength() == 0)
            {
                Add(errors, ContractErrorCodes.GeometryEmpty, $"{path}.coordinates", "MultiPoint requires at least one position.");
            }
            var pointIndex = 0;
            foreach (var position in coordinates.EnumerateArray())
            {
                vertices++;
                ValidatePosition(position, $"{path}.coordinates[{pointIndex}]", errors);
                pointIndex++;
            }
        }
        else if (type is not null)
        {
            Add(
                errors,
                ContractErrorCodes.GeometryTypeUnsupported,
                $"{path}.type",
                allowPoints
                    ? "Only LineString, MultiLineString, Point and MultiPoint are supported."
                    : "Only LineString and MultiLineString are supported in this contract version.");
        }

        if (vertices > MaximumVerticesPerFeature)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, $"{path}.coordinates", "Feature exceeds 100,000 vertices.");
        }
    }

    private static void ValidateLine(
        JsonElement line,
        string path,
        ref int vertices,
        List<ContractValidationError> errors)
    {
        if (line.ValueKind != JsonValueKind.Array || line.GetArrayLength() < 2)
        {
            Add(errors, ContractErrorCodes.GeometryEmpty, path, "LineString requires at least two positions.");
            return;
        }

        var positionIndex = 0;
        foreach (var position in line.EnumerateArray())
        {
            vertices++;
            ValidatePosition(position, $"{path}[{positionIndex}]", errors);
            positionIndex++;
        }
    }

    private static void ValidatePosition(
        JsonElement position,
        string positionPath,
        List<ContractValidationError> errors)
    {
        if (position.ValueKind != JsonValueKind.Array)
        {
            Add(errors, ContractErrorCodes.GeometryCoordinateInvalid, positionPath, "Position must be an array.");
        }
        else if (position.GetArrayLength() != 2)
        {
            Add(errors, ContractErrorCodes.GeometryDimensionUnsupported, positionPath, "The contract accepts exactly two coordinates and rejects Z.");
        }
        else
        {
            foreach (var coordinate in position.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Number ||
                    !coordinate.TryGetDouble(out var value) ||
                    !double.IsFinite(value))
                {
                    Add(errors, ContractErrorCodes.GeometryCoordinateInvalid, positionPath, "Coordinates must be finite JSON numbers.");
                    break;
                }
            }
        }
    }

    private static void ValidateProperties(
        JsonElement feature,
        string featurePath,
        List<ContractValidationError> errors)
    {
        var path = $"{featurePath}.properties";
        if (!feature.TryGetProperty("properties", out var properties))
        {
            Add(errors, ContractErrorCodes.RequiredField, path, "Properties object is required.");
            return;
        }
        if (properties.ValueKind != JsonValueKind.Object)
        {
            Add(errors, ContractErrorCodes.PropertiesInvalid, path, "Properties must be an object.");
            return;
        }
        if (Encoding.UTF8.GetByteCount(properties.GetRawText()) > MaximumPropertiesBytes)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, path, "Properties exceed 64 KiB.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var property in properties.EnumerateObject())
        {
            count++;
            if (!names.Add(property.Name) || string.IsNullOrWhiteSpace(property.Name) || property.Name.Length > 128)
            {
                Add(errors, ContractErrorCodes.PropertiesInvalid, $"{path}.{property.Name}", "Property names must be unique, non-blank and at most 128 characters.");
            }
            ValidatePropertyValue(property.Value, $"{path}.{property.Name}", 1, errors);
        }
        if (count > MaximumProperties)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, path, "Properties exceed 128 top-level fields.");
        }
    }

    private static void ValidatePropertyValue(
        JsonElement value,
        string path,
        int depth,
        List<ContractValidationError> errors)
    {
        if (depth > MaximumPropertyDepth &&
            value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, path, "Properties exceed nesting depth 4.");
            return;
        }
        if (value.ValueKind == JsonValueKind.String &&
            value.GetString()!.Length > MaximumPropertyStringLength)
        {
            Add(errors, ContractErrorCodes.LimitExceeded, path, "Property string exceeds 4,096 characters.");
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                ValidatePropertyValue(property.Value, $"{path}.{property.Name}", depth + 1, errors);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                ValidatePropertyValue(item, $"{path}[{index}]", depth + 1, errors);
                index++;
            }
        }
    }

    private static string? RequiredString(
        JsonElement parent,
        string name,
        int maximumLength,
        string parentPath,
        List<ContractValidationError> errors)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            Add(errors, ContractErrorCodes.RequiredField, $"{parentPath}.{name}", "Required field is missing.");
            return null;
        }
        return ValidateString(value, name, maximumLength, parentPath, errors);
    }

    private static string? OptionalString(
        JsonElement parent,
        string name,
        int maximumLength,
        string parentPath,
        List<ContractValidationError> errors)
    {
        return parent.TryGetProperty(name, out var value)
            ? ValidateString(value, name, maximumLength, parentPath, errors)
            : null;
    }

    private static string? ValidateString(
        JsonElement value,
        string name,
        int maximumLength,
        string parentPath,
        List<ContractValidationError> errors)
    {
        var path = $"{parentPath}.{name}";
        if (value.ValueKind != JsonValueKind.String)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, path, "Value must be a string.");
            return null;
        }
        var text = value.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximumLength)
        {
            Add(errors, ContractErrorCodes.FieldInvalid, path, $"Value must be non-blank and at most {maximumLength} characters.");
            return null;
        }
        return text;
    }

    private static void ValidateKnownFields(
        JsonElement value,
        HashSet<string> knownFields,
        string path,
        List<ContractValidationError> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !knownFields.Contains(property.Name))
            {
                Add(errors, ContractErrorCodes.FieldInvalid, $"{path}.{property.Name}", "Field is duplicated or unknown in this contract version.");
            }
        }
    }

    private static void Add(
        List<ContractValidationError> errors,
        string code,
        string path,
        string message) => errors.Add(new(code, path, message));

    [GeneratedRegex("^EPSG:[1-9][0-9]{2,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex EpsgIdentifier();

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColor();
}

using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace GeoMap.Contract.Tests;

public sealed class GeoMapContractValidatorTests
{
    [Theory]
    [InlineData("valid-linestring.geomap.json")]
    [InlineData("valid-multilinestring.geomap.json")]
    public void Valid_examples_pass(string fileName)
    {
        var result = ValidateExample(fileName);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData("invalid-version.geomap.json", ContractErrorCodes.VersionUnsupported)]
    [InlineData("invalid-crs.geomap.json", ContractErrorCodes.CrsInvalid)]
    [InlineData("invalid-z.geomap.json", ContractErrorCodes.GeometryDimensionUnsupported)]
    [InlineData("invalid-geometry-type.geomap.json", ContractErrorCodes.GeometryTypeUnsupported)]
    public void Invalid_examples_return_stable_error_codes(
        string fileName,
        string expectedCode)
    {
        var result = ValidateExample(fileName);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }

    [Fact]
    public void Properties_at_the_declared_field_limit_pass()
    {
        var root = ReadValidRoot();
        var properties = root["features"]![0]!["properties"]!.AsObject();
        properties.Clear();
        for (var index = 0; index < GeoMapContractValidator.MaximumProperties; index++)
        {
            properties[$"field-{index}"] = index;
        }

        var result = Validate(root);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Properties_above_the_declared_field_limit_fail()
    {
        var root = ReadValidRoot();
        var properties = root["features"]![0]!["properties"]!.AsObject();
        properties.Clear();
        for (var index = 0; index <= GeoMapContractValidator.MaximumProperties; index++)
        {
            properties[$"field-{index}"] = index;
        }

        var result = Validate(root);

        Assert.Contains(result.Errors, error => error.Code == ContractErrorCodes.LimitExceeded);
    }

    [Fact]
    public void Unknown_fields_are_rejected_for_version_compatibility()
    {
        var root = ReadValidRoot();
        root["futureField"] = true;

        var result = Validate(root);

        Assert.Contains(result.Errors, error =>
            error.Code == ContractErrorCodes.FieldInvalid &&
            error.Path == "$.futureField");
    }

    [Fact]
    public void Duplicate_source_identity_is_rejected()
    {
        var root = ReadValidRoot();
        var features = root["features"]!.AsArray();
        features.Add(features[0]!.DeepClone());

        var result = Validate(root);

        Assert.Contains(result.Errors, error => error.Code == ContractErrorCodes.SourceIdDuplicate);
    }

    [Fact]
    public void Streaming_metadata_reuses_contract_rules_without_features_array()
    {
        var root = ReadValidRoot();
        root.Remove("features");

        var result = GeoMapContractValidator.ValidateStreamingMetadata(
            Encoding.UTF8.GetBytes(root.ToJsonString()));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Streaming_feature_reports_its_global_index()
    {
        var root = ReadValidRoot();
        var feature = root["features"]![0]!.DeepClone().AsObject();
        feature["geometry"]!["coordinates"]![0]!.AsArray().Add(10);

        var result = GeoMapContractValidator.ValidateStreamingFeature(
            Encoding.UTF8.GetBytes(feature.ToJsonString()), 123);

        Assert.Contains(result.Errors, error =>
            error.Code == ContractErrorCodes.GeometryDimensionUnsupported &&
            error.Path.StartsWith("$.features[123]", StringComparison.Ordinal));
    }

    [Fact]
    public void Streaming_example_validates_record_by_record()
    {
        var lines = File.ReadAllLines(ExamplePath("valid-stream.geomap.ndjson"));

        var metadata = GeoMapContractValidator.ValidateStreamingMetadata(
            Encoding.UTF8.GetBytes(lines[0]));
        var features = lines.Skip(1)
            .Select((line, index) => GeoMapContractValidator.ValidateStreamingFeature(
                Encoding.UTF8.GetBytes(line), index))
            .ToArray();

        Assert.True(metadata.IsValid, string.Join(Environment.NewLine, metadata.Errors));
        Assert.All(features, result =>
            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors)));
    }

    private static ContractValidationResult ValidateExample(string fileName) =>
        GeoMapContractValidator.Validate(File.ReadAllBytes(ExamplePath(fileName)));

    private static JsonObject ReadValidRoot() =>
        JsonNode.Parse(File.ReadAllText(ExamplePath("valid-linestring.geomap.json")))!
            .AsObject();

    private static ContractValidationResult Validate(JsonObject root) =>
        GeoMapContractValidator.Validate(Encoding.UTF8.GetBytes(root.ToJsonString()));

    private static string ExamplePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "examples", fileName);
}

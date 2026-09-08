using Xunit;

namespace GeoMap.Importer.Tests;

public sealed class ImporterCommandOptionsTests
{
    [Fact]
    public void TryParse_accepts_file_and_connection_from_environment()
    {
        var success = ImporterCommandOptions.TryParse(
            ["--file", "sample.geomap.json"],
            "Host=localhost",
            out var options,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal("sample.geomap.json", options!.FilePath);
        Assert.Equal("Host=localhost", options.ConnectionString);
        Assert.Equal(ImporterCommandOptions.DefaultBatchSize, options.BatchSize);
        Assert.False(options.IsStreaming);
    }

    [Fact]
    public void TryParse_rejects_missing_connection_string()
    {
        var success = ImporterCommandOptions.TryParse(
            ["--file", "sample.geomap.json"],
            null,
            out var options,
            out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.Contains(
            ImporterCommandOptions.ConnectionStringEnvironmentVariable,
            error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_accepts_streaming_file_and_configurable_batch()
    {
        var success = ImporterCommandOptions.TryParse(
            ["--batch-size", "25000", "--file", "large.geomap.ndjson"],
            "Host=localhost",
            out var options,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.True(options!.IsStreaming);
        Assert.Equal(25_000, options.BatchSize);
    }

    [Fact]
    public void TryParse_rejects_batch_outside_limits()
    {
        var success = ImporterCommandOptions.TryParse(
            ["--file", "large.geomap.ndjson", "--batch-size", "1"],
            "Host=localhost",
            out var options,
            out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.Contains("between", error, StringComparison.Ordinal);
    }
}

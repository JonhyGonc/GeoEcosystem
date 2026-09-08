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
}

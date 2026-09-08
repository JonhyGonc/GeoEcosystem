using System.Text.Json;

namespace GeoMap.Importer.Tests;

public sealed class ImporterApplicationTests
{
    [Fact]
    public async Task RunAsync_does_not_persist_an_invalid_document()
    {
        var path = Path.GetTempFileName();
        var previous = Environment.GetEnvironmentVariable(
            ImporterCommandOptions.ConnectionStringEnvironmentVariable);
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "contractVersion": "0.2",
                  "features": []
                }
                """);
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                "Host=unused");
            var store = new RecordingStore();
            var output = new StringWriter();

            var exitCode = await new ImporterApplication(store)
                .RunAsync(["--file", path], output, TextWriter.Null);

            Assert.Equal(2, exitCode);
            Assert.False(store.WasCalled);
            using var summary = JsonDocument.Parse(output.ToString());
            Assert.False(summary.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(0, summary.RootElement.GetProperty("imported").GetInt32());
            Assert.True(summary.RootElement.GetProperty("errors").GetArrayLength() > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                previous);
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_emits_success_summary_from_store_result()
    {
        var path = Path.GetTempFileName();
        var previous = Environment.GetEnvironmentVariable(
            ImporterCommandOptions.ConnectionStringEnvironmentVariable);
        try
        {
            await File.WriteAllBytesAsync(path, GeoMapImportDocumentTests.ValidDocument());
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                "Host=unused");
            var output = new StringWriter();
            var store = new RecordingStore
            {
                Result = new(
                    Guid.Parse("00000000-0000-0000-0000-000000000101"),
                    Guid.Parse("00000000-0000-0000-0000-000000000102"),
                    1,
                    1)
            };

            var exitCode = await new ImporterApplication(store)
                .RunAsync(["--file", path], output, TextWriter.Null);

            Assert.Equal(0, exitCode);
            Assert.True(store.WasCalled);
            using var summary = JsonDocument.Parse(output.ToString());
            Assert.True(summary.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(1, summary.RootElement.GetProperty("received").GetInt32());
            Assert.Equal(1, summary.RootElement.GetProperty("imported").GetInt32());
            Assert.Equal(1, summary.RootElement.GetProperty("replaced").GetInt32());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                previous);
            File.Delete(path);
        }
    }

    private sealed class RecordingStore : IGeoMapImportStore
    {
        public bool WasCalled { get; private set; }
        public GeoMapImportResult Result { get; init; } = new(Guid.Empty, Guid.Empty, 0, 0);

        public Task<GeoMapImportResult> ImportAsync(
            string connectionString,
            GeoMapImportDocument document,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(Result);
        }
    }
}

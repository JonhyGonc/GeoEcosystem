using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Xunit;

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
            Assert.Equal(
                Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path))),
                store.Document!.SourceFileSha256);
            Assert.Equal(store.Document.SourceFileSha256, store.Document.Metadata.SourceFileSha256);
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
    public async Task RunAsync_streams_features_and_emits_progress()
    {
        var path = await CreateStreamingFileAsync();
        var previous = Environment.GetEnvironmentVariable(
            ImporterCommandOptions.ConnectionStringEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                "Host=unused");
            var streamingStore = new RecordingStreamingStore();
            var output = new StringWriter();
            var progress = new StringWriter();

            var exitCode = await new ImporterApplication(
                    new RecordingStore(), streamingStore)
                .RunAsync(
                    ["--file", path, "--batch-size", "100"],
                    output,
                    progress);

            Assert.Equal(0, exitCode);
            Assert.Equal(1, streamingStore.Received);
            Assert.Equal(
                Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path))),
                streamingStore.Metadata!.SourceFileSha256);
            Assert.Contains("\"event\":\"progress\"", progress.ToString(), StringComparison.Ordinal);
            using var summary = JsonDocument.Parse(output.ToString());
            Assert.Equal(1, summary.RootElement.GetProperty("imported").GetInt32());
            Assert.Equal(1, summary.RootElement.GetProperty("batches").GetInt32());
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
    public async Task RunAsync_reports_cancellation_without_success()
    {
        var path = await CreateStreamingFileAsync();
        var previous = Environment.GetEnvironmentVariable(
            ImporterCommandOptions.ConnectionStringEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                "Host=unused");
            var output = new StringWriter();

            var exitCode = await new ImporterApplication(
                    new RecordingStore(), new CancellingStreamingStore())
                .RunAsync(["--file", path], output, TextWriter.Null);

            Assert.Equal(130, exitCode);
            using var summary = JsonDocument.Parse(output.ToString());
            Assert.False(summary.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal(
                ImportErrorCodes.Cancelled,
                summary.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                ImporterCommandOptions.ConnectionStringEnvironmentVariable,
                previous);
            File.Delete(path);
        }
    }

    private static async Task<string> CreateStreamingFileAsync()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"geomap-{Guid.NewGuid():N}.geomap.ndjson");
        var content = Encoding.UTF8.GetString(GeoMapImportDocumentTests.StreamingMetadata()) +
                      "\n" +
                      Encoding.UTF8.GetString(GeoMapImportDocumentTests.StreamingFeature());
        await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        return path;
    }

    private sealed class RecordingStore : IGeoMapImportStore
    {
        public bool WasCalled { get; private set; }
        public GeoMapImportDocument? Document { get; private set; }
        public GeoMapImportResult Result { get; init; } = new(Guid.Empty, Guid.Empty, 0, 0);

        public Task<GeoMapImportResult> ImportAsync(
            string connectionString,
            GeoMapImportDocument document,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            Document = document;
            return Task.FromResult(Result);
        }
    }

    private sealed class RecordingStreamingStore : IGeoMapStreamingImportStore
    {
        public int Received { get; private set; }
        public GeoMapImportMetadata? Metadata { get; private set; }

        public async Task<GeoMapImportResult> ImportAsync(
            string connectionString,
            GeoMapImportMetadata metadata,
            IAsyncEnumerable<GeoMapImportFeature> features,
            int batchSize,
            Action<ImportProgress>? reportProgress = null,
            CancellationToken cancellationToken = default)
        {
            Metadata = metadata;
            await foreach (var _ in features.WithCancellation(cancellationToken))
            {
                Received++;
            }
            reportProgress?.Invoke(new("progress", Received, 1, Received, 1));
            return new(Guid.NewGuid(), Guid.NewGuid(), Received, 0, 1);
        }
    }

    private sealed class CancellingStreamingStore : IGeoMapStreamingImportStore
    {
        public async Task<GeoMapImportResult> ImportAsync(
            string connectionString,
            GeoMapImportMetadata metadata,
            IAsyncEnumerable<GeoMapImportFeature> features,
            int batchSize,
            Action<ImportProgress>? reportProgress = null,
            CancellationToken cancellationToken = default)
        {
            await foreach (var _ in features.WithCancellation(cancellationToken))
            {
                throw new OperationCanceledException();
            }
            throw new OperationCanceledException();
        }
    }
}

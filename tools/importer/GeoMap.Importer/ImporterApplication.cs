using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GeoMap.Contract;

namespace GeoMap.Importer;

public sealed class ImporterApplication(
    IGeoMapImportStore store,
    IGeoMapStreamingImportStore? streamingStore = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ProgressJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!ImporterCommandOptions.TryParse(
                args,
                Environment.GetEnvironmentVariable(
                    ImporterCommandOptions.ConnectionStringEnvironmentVariable),
                out var options,
                out var optionError))
        {
            await error.WriteLineAsync(optionError);
            return 64;
        }

        var stopwatch = Stopwatch.StartNew();
        var state = new ImportState();
        try
        {
            var file = new FileInfo(options!.FilePath);
            if (!file.Exists)
            {
                throw new GeoMapImportException(
                    ImportErrorCodes.FileInvalid,
                    "$",
                    "Import file does not exist.");
            }

            if (options.IsStreaming)
            {
                if (streamingStore is null)
                {
                    throw new InvalidOperationException(
                        "Streaming import store is not configured.");
                }
                return await RunStreamingAsync(
                    options, output, error, stopwatch, state, cancellationToken);
            }

            if (file.Length > GeoMapContractValidator.MaximumDocumentBytes)
            {
                return await WriteValidationFailureAsync(
                    output,
                    0,
                    stopwatch,
                    [new(ContractErrorCodes.LimitExceeded, "$", "Document exceeds 50 MiB.")]);
            }

            var utf8Json = await File.ReadAllBytesAsync(options.FilePath, cancellationToken);
            state.Received = GeoMapImportDocument.CountReceivedFeatures(utf8Json);
            var validation = GeoMapContractValidator.Validate(utf8Json);
            if (!validation.IsValid)
            {
                return await WriteValidationFailureAsync(
                    output, state.Received, stopwatch, validation.Errors);
            }

            var document = GeoMapImportDocument.Parse(utf8Json);
            var result = await store.ImportAsync(
                options.ConnectionString, document, cancellationToken);
            return await WriteSuccessAsync(
                output, state.Received, result, stopwatch);
        }
        catch (OperationCanceledException)
        {
            return await WriteFailureAsync(
                output, state.Received, stopwatch,
                new(ImportErrorCodes.Cancelled, "$", "Import was cancelled; the previous network snapshot was preserved."),
                130);
        }
        catch (GeoMapImportException exception)
        {
            return await WriteFailureAsync(
                output, state.Received, stopwatch,
                new(exception.Code, exception.Path, exception.Message),
                exception.Code.StartsWith("GMC_", StringComparison.Ordinal) ? 2 : 3);
        }
        catch (IOException exception)
        {
            return await WriteFailureAsync(
                output, state.Received, stopwatch,
                new(ImportErrorCodes.FileInvalid, "$", exception.Message),
                66);
        }
        catch (UnauthorizedAccessException exception)
        {
            return await WriteFailureAsync(
                output, state.Received, stopwatch,
                new(ImportErrorCodes.FileInvalid, "$", exception.Message),
                66);
        }
        catch (Exception exception)
        {
            return await WriteFailureAsync(
                output, state.Received, stopwatch,
                new(ImportErrorCodes.PersistenceFailed, "$", exception.Message),
                3);
        }
    }

    private async Task<int> RunStreamingAsync(
        ImporterCommandOptions options,
        TextWriter output,
        TextWriter error,
        Stopwatch stopwatch,
        ImportState state,
        CancellationToken cancellationToken)
    {
        await using var lines = BoundedNdjsonReader
            .ReadLinesAsync(options.FilePath, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        if (!await lines.MoveNextAsync())
        {
            throw new GeoMapImportException(
                ImportErrorCodes.FileInvalid,
                "$",
                "Streaming import file is empty.");
        }

        var metadataValidation = GeoMapContractValidator.ValidateStreamingMetadata(
            lines.Current.Utf8Json);
        if (!metadataValidation.IsValid)
        {
            return await WriteValidationFailureAsync(
                output, 0, stopwatch, metadataValidation.Errors);
        }
        var metadata = GeoMapImportDocument.ParseStreamingMetadata(
            lines.Current.Utf8Json);

        async IAsyncEnumerable<GeoMapImportFeature> ReadFeaturesAsync(
            [EnumeratorCancellation] CancellationToken featureCancellationToken = default)
        {
            var sequence = 0;
            while (await lines.MoveNextAsync())
            {
                featureCancellationToken.ThrowIfCancellationRequested();
                state.Received++;
                var validation = GeoMapContractValidator.ValidateStreamingFeature(
                    lines.Current.Utf8Json, sequence, metadata.ContractVersion);
                if (!validation.IsValid)
                {
                    var first = validation.Errors[0];
                    throw new GeoMapImportException(first.Code, first.Path, first.Message);
                }

                yield return GeoMapImportDocument.ParseStreamingFeature(
                    lines.Current.Utf8Json, sequence);
                sequence++;
            }
        }

        void ReportProgress(ImportProgress progress) =>
            error.WriteLine(JsonSerializer.Serialize(progress, ProgressJsonOptions));

        var result = await streamingStore!.ImportAsync(
            options.ConnectionString,
            metadata,
            ReadFeaturesAsync(cancellationToken),
            options.BatchSize,
            ReportProgress,
            cancellationToken);
        return await WriteSuccessAsync(output, state.Received, result, stopwatch);
    }

    private static async Task<int> WriteSuccessAsync(
        TextWriter output,
        int received,
        GeoMapImportResult result,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        await WriteSummaryAsync(
            output,
            new(true, received, result.Imported, 0, result.Replaced,
                result.ProjectId, result.NetworkId, stopwatch.ElapsedMilliseconds, [], result.Batches));
        return 0;
    }

    private static async Task<int> WriteValidationFailureAsync(
        TextWriter output,
        int received,
        Stopwatch stopwatch,
        IReadOnlyList<ContractValidationError> errors)
    {
        stopwatch.Stop();
        await WriteSummaryAsync(
            output,
            new(false, received, 0, received, 0, null, null,
                stopwatch.ElapsedMilliseconds, errors));
        return 2;
    }

    private static async Task<int> WriteFailureAsync(
        TextWriter output,
        int received,
        Stopwatch stopwatch,
        ContractValidationError error,
        int exitCode)
    {
        stopwatch.Stop();
        await WriteSummaryAsync(
            output,
            new(false, received, 0, received, 0, null, null,
                stopwatch.ElapsedMilliseconds, [error]));
        return exitCode;
    }

    private static Task WriteSummaryAsync(TextWriter output, ImportSummary summary) =>
        output.WriteLineAsync(JsonSerializer.Serialize(summary, JsonOptions));

    private sealed class ImportState
    {
        public int Received { get; set; }
    }
}

using System.Diagnostics;
using System.Text.Json;
using GeoMap.Contract;

namespace GeoMap.Importer;

public sealed class ImporterApplication(IGeoMapImportStore store)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
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
        byte[] utf8Json;
        try
        {
            var file = new FileInfo(options!.FilePath);
            if (!file.Exists)
            {
                return await WriteFailureAsync(
                    output, 0, stopwatch,
                    new(ImportErrorCodes.FileInvalid, "$", "Import file does not exist."),
                    66);
            }
            if (file.Length > GeoMapContractValidator.MaximumDocumentBytes)
            {
                return await WriteFailureAsync(
                    output, 0, stopwatch,
                    new(ContractErrorCodes.LimitExceeded, "$", "Document exceeds 50 MiB."),
                    2);
            }

            utf8Json = await File.ReadAllBytesAsync(options.FilePath, cancellationToken);
        }
        catch (IOException exception)
        {
            return await WriteFailureAsync(
                output, 0, stopwatch,
                new(ImportErrorCodes.FileInvalid, "$", exception.Message),
                66);
        }
        catch (UnauthorizedAccessException exception)
        {
            return await WriteFailureAsync(
                output, 0, stopwatch,
                new(ImportErrorCodes.FileInvalid, "$", exception.Message),
                66);
        }

        var received = GeoMapImportDocument.CountReceivedFeatures(utf8Json);
        var validation = GeoMapContractValidator.Validate(utf8Json);
        if (!validation.IsValid)
        {
            stopwatch.Stop();
            await WriteSummaryAsync(
                output,
                new(false, received, 0, received, 0, null, null,
                    stopwatch.ElapsedMilliseconds, validation.Errors));
            return 2;
        }

        var document = GeoMapImportDocument.Parse(utf8Json);
        try
        {
            var result = await store.ImportAsync(
                options.ConnectionString, document, cancellationToken);
            stopwatch.Stop();
            await WriteSummaryAsync(
                output,
                new(true, received, result.Imported, 0, result.Replaced,
                    result.ProjectId, result.NetworkId, stopwatch.ElapsedMilliseconds, []));
            return 0;
        }
        catch (GeoMapImportException exception)
        {
            return await WriteFailureAsync(
                output, received, stopwatch,
                new(exception.Code, exception.Path, exception.Message),
                3);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await WriteFailureAsync(
                output, received, stopwatch,
                new(ImportErrorCodes.PersistenceFailed, "$", exception.Message),
                3);
        }
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
}

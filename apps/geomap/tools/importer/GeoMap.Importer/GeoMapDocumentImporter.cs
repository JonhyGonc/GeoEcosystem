using System.Diagnostics;
using GeoMap.Contract;

namespace GeoMap.Importer;

/// <summary>Resumo da importação e o código de saída que o terminal devolve para ele.</summary>
public sealed record GeoMapDocumentImportOutcome(ImportSummary Summary, int ExitCode);

/// <summary>
/// Importação de um documento <c>.geomap.json</c> inteiro: limite de tamanho, validação do contrato,
/// leitura, hash e gravação transacional. É o mesmo caminho do importador do terminal e da importação
/// pelo site (M4.5), para que os dois tenham as mesmas regras e os mesmos códigos de erro.
/// </summary>
public static class GeoMapDocumentImporter
{
    public static async Task<GeoMapDocumentImportOutcome> ImportAsync(
        IGeoMapImportStore store,
        string connectionString,
        byte[] utf8Json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(utf8Json);

        var stopwatch = Stopwatch.StartNew();
        var received = 0;
        try
        {
            if (utf8Json.Length > GeoMapContractValidator.MaximumDocumentBytes)
            {
                return ValidationFailure(
                    0,
                    stopwatch,
                    [new(ContractErrorCodes.LimitExceeded, "$", "Document exceeds 50 MiB.")]);
            }

            received = GeoMapImportDocument.CountReceivedFeatures(utf8Json);
            var validation = GeoMapContractValidator.Validate(utf8Json);
            if (!validation.IsValid)
            {
                return ValidationFailure(received, stopwatch, validation.Errors);
            }

            var document = GeoMapImportDocument.Parse(utf8Json) with
            {
                SourceFileSha256 = SourceFileHash.Compute(utf8Json.AsSpan())
            };
            var result = await store.ImportAsync(connectionString, document, cancellationToken);
            stopwatch.Stop();
            return new(
                new(true, received, result.Imported, 0, result.Replaced,
                    result.ProjectId, result.NetworkId, stopwatch.ElapsedMilliseconds, [], result.Batches),
                0);
        }
        catch (OperationCanceledException)
        {
            return Failure(
                received, stopwatch,
                new(ImportErrorCodes.Cancelled, "$", "Import was cancelled; the previous network snapshot was preserved."),
                130);
        }
        catch (GeoMapImportException exception)
        {
            return Failure(
                received, stopwatch,
                new(exception.Code, exception.Path, exception.Message),
                exception.Code.StartsWith("GMC_", StringComparison.Ordinal) ? 2 : 3);
        }
        catch (Exception exception)
        {
            return Failure(
                received, stopwatch,
                new(ImportErrorCodes.PersistenceFailed, "$", exception.Message),
                3);
        }
    }

    private static GeoMapDocumentImportOutcome ValidationFailure(
        int received,
        Stopwatch stopwatch,
        IReadOnlyList<ContractValidationError> errors)
    {
        stopwatch.Stop();
        return new(
            new(false, received, 0, received, 0, null, null, stopwatch.ElapsedMilliseconds, errors),
            2);
    }

    private static GeoMapDocumentImportOutcome Failure(
        int received,
        Stopwatch stopwatch,
        ContractValidationError error,
        int exitCode)
    {
        stopwatch.Stop();
        return new(
            new(false, received, 0, received, 0, null, null, stopwatch.ElapsedMilliseconds, [error]),
            exitCode);
    }
}

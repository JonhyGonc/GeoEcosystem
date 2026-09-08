using GeoMap.Contract;

namespace GeoMap.Importer;

public static class ImportErrorCodes
{
    public const string FileInvalid = "GMI_FILE_INVALID";
    public const string CrsUnavailable = "GMI_CRS_UNAVAILABLE";
    public const string PersistenceFailed = "GMI_PERSISTENCE_FAILED";
}

public sealed record GeoMapImportResult(
    Guid ProjectId,
    Guid NetworkId,
    int Imported,
    int Replaced);

public sealed record ImportSummary(
    bool Success,
    int Received,
    int Imported,
    int Rejected,
    int Replaced,
    Guid? ProjectId,
    Guid? NetworkId,
    long ElapsedMilliseconds,
    IReadOnlyList<ContractValidationError> Errors);

public sealed class GeoMapImportException(
    string code,
    string path,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public string Path { get; } = path;
}

public interface IGeoMapImportStore
{
    Task<GeoMapImportResult> ImportAsync(
        string connectionString,
        GeoMapImportDocument document,
        CancellationToken cancellationToken = default);
}

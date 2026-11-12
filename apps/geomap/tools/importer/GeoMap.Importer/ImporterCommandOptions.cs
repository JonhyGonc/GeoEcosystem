using System.Globalization;

namespace GeoMap.Importer;

public sealed record ImporterCommandOptions(
    string FilePath,
    string ConnectionString,
    int BatchSize,
    bool IsStreaming)
{
    public const string ConnectionStringEnvironmentVariable = "GEOMAP_DB_CONNECTION_STRING";
    public const int DefaultBatchSize = 10_000;
    public const int MinimumBatchSize = 100;
    public const int MaximumBatchSize = 50_000;

    public static bool TryParse(
        IReadOnlyList<string> args,
        string? connectionString,
        out ImporterCommandOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        string? filePath = null;
        var batchSize = DefaultBatchSize;
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument == "--file" && index + 1 < args.Count && filePath is null)
            {
                filePath = args[++index];
            }
            else if (argument == "--batch-size" && index + 1 < args.Count &&
                     int.TryParse(args[++index], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBatchSize))
            {
                batchSize = parsedBatchSize;
            }
            else
            {
                error = "Usage: GeoMap.Importer --file <path> [--batch-size <100-50000>]";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            error = "The import file path cannot be blank.";
            return false;
        }

        if (batchSize is < MinimumBatchSize or > MaximumBatchSize)
        {
            error = $"Batch size must be between {MinimumBatchSize} and {MaximumBatchSize}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = $"Environment variable {ConnectionStringEnvironmentVariable} is required.";
            return false;
        }

        options = new(
            filePath,
            connectionString,
            batchSize,
            filePath.EndsWith(".geomap.ndjson", StringComparison.OrdinalIgnoreCase));
        return true;
    }
}

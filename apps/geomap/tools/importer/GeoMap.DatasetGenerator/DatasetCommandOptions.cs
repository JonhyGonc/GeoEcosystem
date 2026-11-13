using System.Globalization;

namespace GeoMap.DatasetGenerator;

public sealed record DatasetCommandOptions(
    DatasetProfile Profile,
    int Seed,
    int BatchSize,
    string ConnectionString)
{
    public const int DefaultSeed = 20_260_906;
    public const int DefaultBatchSize = 5_000;
    public const string ConnectionStringEnvironmentVariable = "GEOMAP_DB_CONNECTION_STRING";

    public static DatasetCommandOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                throw new DatasetOptionException($"Unexpected argument '{key}'.");
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new DatasetOptionException($"Option '{key}' requires a value.");
            }

            if (!values.TryAdd(key, args[++index]))
            {
                throw new DatasetOptionException($"Option '{key}' was provided more than once.");
            }
        }

        var allowedOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--size", "--seed", "--batch-size", "--connection-string"
        };

        var unknownOption = values.Keys.FirstOrDefault(key => !allowedOptions.Contains(key));
        if (unknownOption is not null)
        {
            throw new DatasetOptionException($"Unknown option '{unknownOption}'.");
        }

        if (!values.TryGetValue("--size", out var size))
        {
            throw new DatasetOptionException("Option '--size' is required.");
        }

        var seed = ParseInteger(values, "--seed", DefaultSeed);
        var batchSize = ParseInteger(values, "--batch-size", DefaultBatchSize);
        if (batchSize is < 1 or > 50_000)
        {
            throw new DatasetOptionException("Option '--batch-size' must be between 1 and 50000.");
        }

        values.TryGetValue("--connection-string", out var connectionString);
        connectionString ??= Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DatasetOptionException(
                "Provide '--connection-string' or set GEOMAP_DB_CONNECTION_STRING.");
        }

        return new DatasetCommandOptions(
            DatasetProfile.Parse(size),
            seed,
            batchSize,
            connectionString);
    }

    private static int ParseInteger(
        IReadOnlyDictionary<string, string> values,
        string option,
        int defaultValue)
    {
        if (!values.TryGetValue(option, out var rawValue))
        {
            return defaultValue;
        }

        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new DatasetOptionException($"Option '{option}' must be an integer.");
        }

        return value;
    }
}

public sealed class DatasetOptionException(string message) : Exception(message);

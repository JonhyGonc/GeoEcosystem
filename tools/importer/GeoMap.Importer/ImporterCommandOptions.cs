namespace GeoMap.Importer;

public sealed record ImporterCommandOptions(string FilePath, string ConnectionString)
{
    public const string ConnectionStringEnvironmentVariable = "GEOMAP_DB_CONNECTION_STRING";

    public static bool TryParse(
        IReadOnlyList<string> args,
        string? connectionString,
        out ImporterCommandOptions? options,
        out string? error)
    {
        options = null;
        error = null;

        if (args.Count != 2 || !string.Equals(args[0], "--file", StringComparison.Ordinal))
        {
            error = "Usage: GeoMap.Importer --file <path-to.geomap.json>";
            return false;
        }

        if (string.IsNullOrWhiteSpace(args[1]))
        {
            error = "The import file path cannot be blank.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            error = $"Environment variable {ConnectionStringEnvironmentVariable} is required.";
            return false;
        }

        options = new(args[1], connectionString);
        return true;
    }
}

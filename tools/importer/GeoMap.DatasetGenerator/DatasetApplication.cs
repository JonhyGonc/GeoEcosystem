using System.Globalization;

namespace GeoMap.DatasetGenerator;

public static class DatasetApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Any(argument =>
                argument.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                argument.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            PrintUsage(Console.Out);
            return 0;
        }

        try
        {
            var options = DatasetCommandOptions.Parse(args);
            var loader = new DatasetLoader(new SyntheticFeatureGenerator());
            var result = await loader.LoadAsync(options);

            Console.WriteLine(
                $"Loaded {options.Profile.Name.ToUpperInvariant()}: " +
                $"{result.FeatureCount.ToString("N0", CultureInfo.InvariantCulture)} features.");
            Console.WriteLine($"Seed: {options.Seed}");
            Console.WriteLine($"Batch size: {options.BatchSize}");
            Console.WriteLine($"Project ID: {result.ProjectId}");
            Console.WriteLine($"Network ID: {result.NetworkId}");
            Console.WriteLine(FormattableString.Invariant(
                $"Actual extent (EPSG:4326): [{result.ActualExtent.MinLongitude:F8}, {result.ActualExtent.MinLatitude:F8}, {result.ActualExtent.MaxLongitude:F8}, {result.ActualExtent.MaxLatitude:F8}]"));
            Console.WriteLine(
                FormattableString.Invariant($"Elapsed: {result.Elapsed.TotalSeconds:F3} s"));
            return 0;
        }
        catch (DatasetOptionException exception)
        {
            Console.Error.WriteLine($"Argument error: {exception.Message}");
            PrintUsage(Console.Error);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Dataset load failed: {exception.Message}");
            return 1;
        }
    }

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine("GeoMap synthetic spatial dataset generator");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  dotnet run --project tools/importer/GeoMap.DatasetGenerator -- \\");
        output.WriteLine("    --size small|medium [--seed 20260906] [--batch-size 5000] \\");
        output.WriteLine("    [--connection-string <postgres-connection-string>]");
        output.WriteLine();
        output.WriteLine(
            "The connection string may instead be set in GEOMAP_DB_CONNECTION_STRING.");
    }
}

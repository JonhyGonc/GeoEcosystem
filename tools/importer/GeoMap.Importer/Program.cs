namespace GeoMap.Importer;

public static class Program
{
    public static Task<int> Main(string[] args) =>
        new ImporterApplication(new PostgresGeoMapImportStore())
            .RunAsync(args, Console.Out, Console.Error);
}

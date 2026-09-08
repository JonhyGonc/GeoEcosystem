namespace GeoMap.Importer;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += handler;
        try
        {
            return await new ImporterApplication(
                    new PostgresGeoMapImportStore(),
                    new PostgresGeoMapStreamingImportStore())
                .RunAsync(args, Console.Out, Console.Error, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}

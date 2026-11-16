namespace GeoEcosystem.Contracts.Inspection.Tests;

// Acesso às fixtures de contratos/gadx-inspecao/fixtures (M3.2).
internal static class FixtureFiles
{
    public static readonly string Root = FindRoot();

    public static byte[] Payload(string name) => File.ReadAllBytes(Path.Combine(Root, "json", $"{name}.json"));

    public static string FramePath(string name) => Path.Combine(Root, "frames", $"{name}.bin");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "contratos", "gadx-inspecao", "fixtures");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Fixtures do GADX não encontradas: rode os testes a partir de um clone do monorepo GeoEcosystem.");
    }
}

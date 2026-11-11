using System.Security.Cryptography;

namespace GeoMap.Importer;

/// <summary>
/// SHA-256 dos bytes do arquivo importado, em hexadecimal minúsculo, gravado em
/// <c>network_imports.source_file_sha256</c> para ligar a rede ao arquivo de origem.
/// </summary>
public static class SourceFileHash
{
    private const int ReadBufferSize = 64 * 1024;

    public static string Compute(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));

    public static async Task<string> ComputeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }
}

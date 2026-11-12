using System.Buffers;
using System.Runtime.CompilerServices;
using GeoMap.Contract;

namespace GeoMap.Importer;

public sealed record NdjsonLine(int Number, ReadOnlyMemory<byte> Utf8Json);

public static class BoundedNdjsonReader
{
    private const int ReadBufferSize = 64 * 1024;

    public static async IAsyncEnumerable<NdjsonLine> ReadLinesAsync(
        string path,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            ReadBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var readBuffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            var lineBuffer = new ArrayBufferWriter<byte>();
            var lineNumber = 1;
            while (true)
            {
                var read = await stream.ReadAsync(
                    readBuffer.AsMemory(0, ReadBufferSize),
                    cancellationToken);
                if (read == 0) break;

                var offset = 0;
                while (offset < read)
                {
                    var newline = Array.IndexOf(readBuffer, (byte)'\n', offset, read - offset);
                    var end = newline >= 0 ? newline : read;
                    Append(lineBuffer, readBuffer.AsMemory(offset, end - offset), lineNumber);
                    if (newline < 0) break;

                    yield return CreateLine(lineBuffer.WrittenMemory, lineNumber);
                    lineBuffer = new();
                    lineNumber++;
                    offset = newline + 1;
                }
            }

            if (lineBuffer.WrittenCount > 0)
            {
                yield return CreateLine(lineBuffer.WrittenMemory, lineNumber);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private static void Append(
        ArrayBufferWriter<byte> writer,
        ReadOnlyMemory<byte> value,
        int lineNumber)
    {
        if (writer.WrittenCount + value.Length > GeoMapContractValidator.MaximumStreamingFeatureBytes)
        {
            var path = lineNumber == 1 ? "$" : $"$.features[{lineNumber - 2}]";
            throw new GeoMapImportException(
                ContractErrorCodes.LimitExceeded,
                path,
                "NDJSON record exceeds 16 MiB.");
        }

        value.Span.CopyTo(writer.GetSpan(value.Length));
        writer.Advance(value.Length);
    }

    private static NdjsonLine CreateLine(ReadOnlyMemory<byte> memory, int lineNumber)
    {
        var value = memory.Span;
        if (value.Length > 0 && value[^1] == (byte)'\r') value = value[..^1];
        if (lineNumber == 1 && value.Length >= 3 &&
            value[0] == 0xef && value[1] == 0xbb && value[2] == 0xbf)
        {
            value = value[3..];
        }
        return new(lineNumber, value.ToArray());
    }
}

using System.Text;
using Xunit;

namespace GeoMap.Importer.Tests;

public sealed class BoundedNdjsonReaderTests
{
    [Fact]
    public async Task ReadLinesAsync_handles_utf8_bom_crlf_and_final_line()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(
                path,
                [0xef, 0xbb, 0xbf, .. Encoding.UTF8.GetBytes("{\"line\":1}\r\n{\"line\":2}")]);

            var lines = new List<NdjsonLine>();
            await foreach (var line in BoundedNdjsonReader.ReadLinesAsync(path))
            {
                lines.Add(line);
            }

            Assert.Equal(2, lines.Count);
            Assert.Equal(1, lines[0].Number);
            Assert.Equal("{\"line\":1}", Encoding.UTF8.GetString(lines[0].Utf8Json.Span));
            Assert.Equal("{\"line\":2}", Encoding.UTF8.GetString(lines[1].Utf8Json.Span));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

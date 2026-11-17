using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GeoMap.Api.Orthophotos;

/// <summary>Pasta das ortofotos (DEC-035). Vazia: a camada não existe.</summary>
public sealed class OrthophotoOptions
{
    public const string SectionName = "Orthophotos";

    public string Directory { get; init; } = string.Empty;
}

/// <summary>Ortofoto encontrada para um projeto: o arquivo MBTiles e o que o mapa precisa saber dele.</summary>
public sealed record OrthophotoFile(
    string Path,
    string FileName,
    string Format,
    double[]? Bounds,
    int MinZoom,
    int MaxZoom);

/// <summary>Nome do projeto pelo id; separado para os testes não precisarem de banco.</summary>
public interface IProjectNameLookup
{
    Task<string?> FindNameAsync(Guid projectId, CancellationToken cancellationToken);
}

public sealed class PostgresProjectNameLookup(NpgsqlDataSource dataSource) : IProjectNameLookup
{
    public async Task<string?> FindNameAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT name FROM projects WHERE id = @id;", connection)
        {
            CommandTimeout = 5,
        };
        command.Parameters.AddWithValue("id", projectId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }
}

/// <summary>
/// Ortofotos em MBTiles (DEC-035): um arquivo por projeto, com o nome do projeto como aparece no site
/// (<c>&lt;nome do projeto&gt;.mbtiles</c>) ou com o id do projeto. Os tiles são lidos direto do SQLite, em
/// modo só leitura; o arquivo é gerado uma vez a partir do ECW/JP2 no QGIS do usuário.
/// </summary>
public sealed class OrthophotoStore(IOptions<OrthophotoOptions> options, IProjectNameLookup projects)
{
    public const string Extension = ".mbtiles";

    private static readonly TimeSpan LookupLifetime = TimeSpan.FromSeconds(30);
    private static readonly char[] InvalidFileNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private readonly ConcurrentDictionary<Guid, (OrthophotoLookup Lookup, DateTimeOffset CheckedAt)> lookups = new();

    /// <summary>Nome de arquivo esperado para o projeto: o nome do projeto, com caracteres proibidos trocados por "-".</summary>
    public static string FileNameFor(string projectName) =>
        new string(projectName.Trim()
            .Select(character => character < ' ' || InvalidFileNameCharacters.Contains(character) ? '-' : character)
            .ToArray()) + Extension;

    public async Task<OrthophotoLookup?> FindAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (lookups.TryGetValue(projectId, out var cached) && DateTimeOffset.UtcNow - cached.CheckedAt < LookupLifetime)
        {
            return cached.Lookup;
        }

        var name = await projects.FindNameAsync(projectId, cancellationToken);
        if (name is null)
        {
            return null;
        }

        var expected = FileNameFor(name);
        var lookup = new OrthophotoLookup(expected, Open(projectId, expected));
        lookups[projectId] = (lookup, DateTimeOffset.UtcNow);
        return lookup;
    }

    public static async Task<byte[]?> ReadTileAsync(OrthophotoFile file, int z, int x, int y, CancellationToken cancellationToken)
    {
        await using var connection = OpenConnection(file.Path);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        // MBTiles numera as linhas de baixo para cima (TMS); o mapa pede de cima para baixo (XYZ).
        command.CommandText =
            "SELECT tile_data FROM tiles WHERE zoom_level = $z AND tile_column = $x AND tile_row = $row LIMIT 1;";
        command.Parameters.AddWithValue("$z", z);
        command.Parameters.AddWithValue("$x", x);
        command.Parameters.AddWithValue("$row", (1 << z) - 1 - y);
        return await command.ExecuteScalarAsync(cancellationToken) as byte[];
    }

    public static string ContentType(OrthophotoFile file, byte[] tile)
    {
        if (tile.Length >= 4 && tile[0] == 0x89 && tile[1] == 0x50 && tile[2] == 0x4E && tile[3] == 0x47)
        {
            return "image/png";
        }

        if (tile.Length >= 3 && tile[0] == 0xFF && tile[1] == 0xD8 && tile[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (tile.Length >= 12 && tile[0] == (byte)'R' && tile[1] == (byte)'I' && tile[8] == (byte)'W' && tile[9] == (byte)'E')
        {
            return "image/webp";
        }

        return file.Format switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "webp" => "image/webp",
            _ => "image/png",
        };
    }

    private OrthophotoFile? Open(Guid projectId, string expected)
    {
        var directory = options.Value.Directory;
        if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
        {
            return null;
        }

        foreach (var fileName in new[] { expected, projectId.ToString("D") + Extension })
        {
            var path = System.IO.Path.Combine(directory, fileName);
            if (File.Exists(path))
            {
                return ReadMetadata(path, fileName);
            }
        }

        return null;
    }

    private static OrthophotoFile ReadMetadata(string path, string fileName)
    {
        using var connection = OpenConnection(path);
        connection.Open();
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name, value FROM metadata;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                {
                    metadata[reader.GetString(0)] = reader.GetString(1);
                }
            }
        }

        int minZoom;
        int maxZoom;
        if (TryInt(metadata, "minzoom", out minZoom) && TryInt(metadata, "maxzoom", out maxZoom))
        {
            return new OrthophotoFile(path, fileName, Format(metadata), Bounds(metadata), minZoom, maxZoom);
        }

        using var zooms = connection.CreateCommand();
        zooms.CommandText = "SELECT min(zoom_level), max(zoom_level) FROM tiles;";
        using var zoomReader = zooms.ExecuteReader();
        zoomReader.Read();
        minZoom = zoomReader.IsDBNull(0) ? 0 : zoomReader.GetInt32(0);
        maxZoom = zoomReader.IsDBNull(1) ? 0 : zoomReader.GetInt32(1);
        return new OrthophotoFile(path, fileName, Format(metadata), Bounds(metadata), minZoom, maxZoom);
    }

    private static SqliteConnection OpenConnection(string path) =>
        new(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());

    private static string Format(Dictionary<string, string> metadata) =>
        metadata.TryGetValue("format", out var format) ? format.Trim().ToLowerInvariant() : "png";

    private static bool TryInt(Dictionary<string, string> metadata, string key, out int value)
    {
        value = 0;
        return metadata.TryGetValue(key, out var text) &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    // bounds = "oeste,sul,leste,norte" em graus (EPSG:4326).
    private static double[]? Bounds(Dictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue("bounds", out var text))
        {
            return null;
        }

        var parts = text.Split(',');
        var values = new double[4];
        if (parts.Length != 4)
        {
            return null;
        }

        for (var index = 0; index < 4; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index]) ||
                !double.IsFinite(values[index]))
            {
                return null;
            }
        }

        return values;
    }
}

/// <summary>O nome de arquivo esperado e a ortofoto encontrada (ou <c>null</c>).</summary>
public sealed record OrthophotoLookup(string ExpectedFileName, OrthophotoFile? File);

using Npgsql;
using NpgsqlTypes;

namespace GeoMap.Api.VectorTiles;

public sealed class VectorTileQuery(NpgsqlDataSource dataSource)
{
    public const int Extent = 4_096;
    public const int Buffer = 64;
    public const int CommandTimeoutSeconds = 5;
    public const string LayerName = "network_features";

    private const string QuerySql = """
        WITH bounds AS (
            SELECT
                ST_TileEnvelope(@zoom, @tile_x, @tile_y) AS tile_envelope,
                ST_Transform(
                    ST_TileEnvelope(@zoom, @tile_x, @tile_y, 0.015625),
                    4326) AS query_envelope
        ),
        tile_rows AS (
            SELECT
                feature.id::text AS id,
                ST_AsMVTGeom(
                    ST_Transform(feature.geometry, 3857),
                    bounds.tile_envelope,
                    4096,
                    64,
                    true) AS geom
            FROM network_features feature
            JOIN networks network ON network.id = feature.network_id
            CROSS JOIN bounds
            WHERE network.project_id = @project_id
              AND feature.network_id = @network_id
              AND feature.geometry && bounds.query_envelope
              AND ST_Intersects(feature.geometry, bounds.query_envelope)
        )
        SELECT COALESCE(
            ST_AsMVT(tile_rows, 'network_features', 4096, 'geom'),
            '\x'::bytea)
        FROM tile_rows
        WHERE geom IS NOT NULL;
        """;

    public async Task<byte[]> ExecuteAsync(
        Guid projectId,
        Guid networkId,
        VectorTileCoordinates coordinates,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(QuerySql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.AddWithValue("project_id", NpgsqlDbType.Uuid, projectId);
        command.Parameters.AddWithValue("network_id", NpgsqlDbType.Uuid, networkId);
        command.Parameters.AddWithValue("zoom", NpgsqlDbType.Integer, coordinates.Zoom);
        command.Parameters.AddWithValue("tile_x", NpgsqlDbType.Integer, coordinates.X);
        command.Parameters.AddWithValue("tile_y", NpgsqlDbType.Integer, coordinates.Y);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as byte[] ?? [];
    }
}

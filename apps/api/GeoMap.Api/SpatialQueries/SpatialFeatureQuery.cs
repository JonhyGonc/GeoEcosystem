using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace GeoMap.Api.SpatialQueries;

public sealed class SpatialFeatureQuery(NpgsqlDataSource dataSource)
{
    public const int CommandTimeoutSeconds = 5;

    private const string QuerySql = """
        SELECT
            feature.id,
            feature.network_id,
            ST_AsGeoJSON(feature.geometry, 8)
        FROM network_features feature
        JOIN networks network ON network.id = feature.network_id
        WHERE network.project_id = @project_id
          AND feature.network_id = @network_id
          AND feature.geometry && ST_MakeEnvelope(
              @min_longitude,
              @min_latitude,
              @max_longitude,
              @max_latitude,
              4326)
          AND ST_Intersects(
              feature.geometry,
              ST_MakeEnvelope(
                  @min_longitude,
                  @min_latitude,
                  @max_longitude,
                  @max_latitude,
                  4326))
        LIMIT @result_limit;
        """;

    public async Task<SpatialFeatureQueryResponse> ExecuteAsync(
        Guid projectId,
        Guid networkId,
        SpatialQueryRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(QuerySql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.AddWithValue("project_id", NpgsqlDbType.Uuid, projectId);
        command.Parameters.AddWithValue("network_id", NpgsqlDbType.Uuid, networkId);
        command.Parameters.AddWithValue(
            "min_longitude", NpgsqlDbType.Double, request.Envelope.MinLongitude);
        command.Parameters.AddWithValue(
            "min_latitude", NpgsqlDbType.Double, request.Envelope.MinLatitude);
        command.Parameters.AddWithValue(
            "max_longitude", NpgsqlDbType.Double, request.Envelope.MaxLongitude);
        command.Parameters.AddWithValue(
            "max_latitude", NpgsqlDbType.Double, request.Envelope.MaxLatitude);
        command.Parameters.AddWithValue("result_limit", NpgsqlDbType.Integer, request.Limit + 1);

        var features = new List<SpatialFeatureResult>(request.Limit + 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            using var geometryDocument = JsonDocument.Parse(reader.GetString(2));
            features.Add(new SpatialFeatureResult(
                reader.GetGuid(0),
                reader.GetGuid(1),
                geometryDocument.RootElement.Clone()));
        }

        var hasMore = features.Count > request.Limit;
        if (hasMore)
        {
            features.RemoveAt(features.Count - 1);
        }

        return new SpatialFeatureQueryResponse(
            projectId,
            networkId,
            request.Envelope,
            request.Limit,
            features.Count,
            hasMore,
            features);
    }
}

public sealed record SpatialFeatureQueryResponse(
    Guid ProjectId,
    Guid NetworkId,
    SpatialEnvelope BoundingBox,
    int Limit,
    int Returned,
    bool HasMore,
    IReadOnlyList<SpatialFeatureResult> Features);

public sealed record SpatialFeatureResult(
    Guid Id,
    Guid NetworkId,
    JsonElement Geometry);

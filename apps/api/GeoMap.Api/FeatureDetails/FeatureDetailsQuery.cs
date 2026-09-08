using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace GeoMap.Api.FeatureDetails;

public sealed class FeatureDetailsQuery(NpgsqlDataSource dataSource)
{
    public const int CommandTimeoutSeconds = 5;

    private const string QuerySql = """
        SELECT
            feature.id,
            feature.network_id,
            network.name,
            network.type,
            feature.source_id,
            feature.source_layer,
            feature.source_type,
            feature.properties::text,
            feature.created_at
        FROM network_features feature
        JOIN networks network ON network.id = feature.network_id
        WHERE network.project_id = @project_id
          AND feature.network_id = @network_id
          AND feature.id = @feature_id;
        """;

    public async Task<FeatureDetailsResponse?> ExecuteAsync(
        Guid projectId,
        Guid networkId,
        Guid featureId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(QuerySql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.AddWithValue("project_id", NpgsqlDbType.Uuid, projectId);
        command.Parameters.AddWithValue("network_id", NpgsqlDbType.Uuid, networkId);
        command.Parameters.AddWithValue("feature_id", NpgsqlDbType.Uuid, featureId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        using var propertiesDocument = JsonDocument.Parse(reader.GetString(7));
        return new FeatureDetailsResponse(
            projectId,
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6),
            propertiesDocument.RootElement.Clone(),
            reader.GetDateTime(8));
    }
}

public sealed record FeatureDetailsResponse(
    Guid ProjectId,
    Guid Id,
    Guid NetworkId,
    string NetworkName,
    string NetworkType,
    string? SourceId,
    string? SourceLayer,
    string SourceType,
    JsonElement Properties,
    DateTime CreatedAt);

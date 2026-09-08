using Npgsql;
using NpgsqlTypes;

namespace GeoMap.Api.Networks;

public sealed class NetworkCatalogQuery(NpgsqlDataSource dataSource)
{
    public const int CommandTimeoutSeconds = 5;

    private const string QuerySql = """
        SELECT
            network.id,
            network.name,
            network.type
        FROM networks network
        WHERE network.project_id = @project_id
        ORDER BY network.name, network.id;
        """;

    public async Task<NetworkCatalogResponse> ExecuteAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(QuerySql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        command.Parameters.AddWithValue("project_id", NpgsqlDbType.Uuid, projectId);

        var networks = new List<NetworkCatalogItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            networks.Add(new NetworkCatalogItem(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return new NetworkCatalogResponse(projectId, networks);
    }
}

public sealed record NetworkCatalogResponse(
    Guid ProjectId,
    IReadOnlyList<NetworkCatalogItem> Networks);

public sealed record NetworkCatalogItem(
    Guid Id,
    string Name,
    string Type);

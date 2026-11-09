using Npgsql;

namespace GeoMap.Api.Projects;

public sealed class ProjectCatalogQuery(NpgsqlDataSource dataSource)
{
    public const int CommandTimeoutSeconds = 5;

    private const string QuerySql = """
        SELECT
            project.id,
            project.name
        FROM projects project
        ORDER BY project.name, project.id;
        """;

    public async Task<ProjectCatalogResponse> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(QuerySql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        var projects = new List<ProjectCatalogItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new ProjectCatalogItem(
                reader.GetGuid(0),
                reader.GetString(1)));
        }

        return new ProjectCatalogResponse(projects);
    }
}

public sealed record ProjectCatalogResponse(
    IReadOnlyList<ProjectCatalogItem> Projects);

public sealed record ProjectCatalogItem(
    Guid Id,
    string Name);

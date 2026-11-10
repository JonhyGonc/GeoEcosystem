using Npgsql;
using NpgsqlTypes;

namespace GeoMap.Api.Projects;

public sealed class ProjectExtentQuery(NpgsqlDataSource dataSource)
{
    public const int CommandTimeoutSeconds = 5;

    // ST_Extent lê apenas a caixa envolvente de cada geometria; as feições do
    // projeto são localizadas pelo índice ix_network_features_network_id.
    private const string ExtentSql = """
        SELECT
            ST_XMin(extent.box),
            ST_YMin(extent.box),
            ST_XMax(extent.box),
            ST_YMax(extent.box)
        FROM (
            SELECT ST_Extent(feature.geometry) AS box
            FROM networks network
            JOIN network_features feature ON feature.network_id = network.id
            WHERE network.project_id = @project_id
        ) extent;
        """;

    // O foco é o ponto da rede mais próximo do centro da extensão. Com o centro
    // como parâmetro constante, o operador <-> usa o índice GiST (KNN). Assim o
    // visualizador pode abrir onde há feições quando a extensão inteira exigiria
    // um zoom abaixo do mínimo das redes.
    private const string FocusSql = """
        SELECT ST_X(focus.point), ST_Y(focus.point)
        FROM (
            SELECT ST_ClosestPoint(
                feature.geometry,
                ST_SetSRID(ST_MakePoint(@center_longitude, @center_latitude), 4326)) AS point
            FROM network_features feature
            JOIN networks network ON network.id = feature.network_id
            WHERE network.project_id = @project_id
            ORDER BY feature.geometry <-> ST_SetSRID(ST_MakePoint(@center_longitude, @center_latitude), 4326)
            LIMIT 1
        ) focus;
        """;

    public async Task<ProjectExtentResponse> ExecuteAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        ProjectExtent extent;
        await using (var command = new NpgsqlCommand(ExtentSql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        })
        {
            command.Parameters.AddWithValue("project_id", NpgsqlDbType.Uuid, projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
            {
                return new ProjectExtentResponse(projectId, null, null);
            }

            extent = new ProjectExtent(
                reader.GetDouble(0),
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.GetDouble(3));
        }

        await using var focusCommand = new NpgsqlCommand(FocusSql, connection)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        focusCommand.Parameters.AddWithValue("project_id", NpgsqlDbType.Uuid, projectId);
        focusCommand.Parameters.AddWithValue(
            "center_longitude",
            NpgsqlDbType.Double,
            (extent.MinLongitude + extent.MaxLongitude) / 2);
        focusCommand.Parameters.AddWithValue(
            "center_latitude",
            NpgsqlDbType.Double,
            (extent.MinLatitude + extent.MaxLatitude) / 2);

        await using var focusReader = await focusCommand.ExecuteReaderAsync(cancellationToken);
        var focus = await focusReader.ReadAsync(cancellationToken)
            ? new ProjectFocus(focusReader.GetDouble(0), focusReader.GetDouble(1))
            : null;

        return new ProjectExtentResponse(projectId, extent, focus);
    }
}

public sealed record ProjectExtentResponse(
    Guid ProjectId,
    ProjectExtent? Extent,
    ProjectFocus? Focus);

public sealed record ProjectExtent(
    double MinLongitude,
    double MinLatitude,
    double MaxLongitude,
    double MaxLatitude);

public sealed record ProjectFocus(
    double Longitude,
    double Latitude);

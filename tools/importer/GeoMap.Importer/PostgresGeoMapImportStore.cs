using Npgsql;
using NpgsqlTypes;

namespace GeoMap.Importer;

public sealed class PostgresGeoMapImportStore : IGeoMapImportStore
{
    /// <summary>
    /// Limite explícito para os comandos que contam, removem e inserem o snapshot
    /// completo da rede. O padrão de 30 s do Npgsql é insuficiente para o cenário
    /// LARGE; o valor permanece finito para que um bloqueio não prenda a importação
    /// indefinidamente.
    /// </summary>
    public const int SnapshotCommandTimeoutSeconds = 600;

    public async Task<GeoMapImportResult> ImportAsync(
        string connectionString,
        GeoMapImportDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(document);

        var projectId = DeterministicGeoMapIds.Project(document);
        var networkId = DeterministicGeoMapIds.Network(projectId, document);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await VerifyDatabaseAsync(
                connection, transaction, document.SourceSrid, cancellationToken);
            await AcquireNetworkLockAsync(
                connection, transaction, networkId, cancellationToken);
            await UpsertContainersAsync(
                connection, transaction, projectId, networkId, document.Metadata, cancellationToken);
            await CreateStagingTableAsync(connection, transaction, cancellationToken);
            await CopyFeaturesAsync(connection, networkId, document.Features, cancellationToken);

            var replaced = await CountExistingFeaturesAsync(
                connection, transaction, networkId, cancellationToken);
            await DeleteExistingFeaturesAsync(
                connection, transaction, networkId, cancellationToken);
            var imported = await InsertStagedFeaturesAsync(
                connection, transaction, networkId, document.SourceSrid, cancellationToken);

            if (imported != document.Features.Count)
            {
                throw new InvalidOperationException(
                    $"Imported {imported} features; expected {document.Features.Count}.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new(projectId, networkId, imported, replaced);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    internal static async Task VerifyDatabaseAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int sourceSrid,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                to_regclass('public.projects') IS NOT NULL
                AND to_regclass('public.networks') IS NOT NULL
                AND to_regclass('public.network_features') IS NOT NULL,
                EXISTS (SELECT 1 FROM spatial_ref_sys WHERE srid = @source_srid);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("source_srid", sourceSrid);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        if (!reader.GetBoolean(0))
        {
            throw new GeoMapImportException(
                ImportErrorCodes.PersistenceFailed,
                "$",
                "The GeoMap spatial model is missing. Apply migration 001 first.");
        }
        if (!reader.GetBoolean(1))
        {
            throw new GeoMapImportException(
                ImportErrorCodes.CrsUnavailable,
                "$.sourceCrs",
                $"EPSG:{sourceSrid} is not available in this PostGIS installation.");
        }
    }

    private static async Task AcquireNetworkLockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid networkId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@network_id, 0));",
            connection,
            transaction);
        command.Parameters.AddWithValue("network_id", networkId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task UpsertContainersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid networkId,
        GeoMapImportMetadata metadata,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO projects (id, name)
            VALUES (@project_id, @project_name)
            ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;

            INSERT INTO networks (id, project_id, name, type)
            VALUES (@network_id, @project_id, @network_name, @network_type)
            ON CONFLICT (id) DO UPDATE SET
                project_id = EXCLUDED.project_id,
                name = EXCLUDED.name,
                type = EXCLUDED.type;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("project_name", metadata.Project.Name);
        command.Parameters.AddWithValue("network_id", networkId);
        command.Parameters.AddWithValue("network_name", metadata.Network.Name);
        command.Parameters.AddWithValue("network_type", metadata.Network.Type);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateStagingTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TEMP TABLE geomap_import_stage (
                sequence integer NOT NULL,
                id uuid NOT NULL,
                source_id text,
                source_layer text,
                source_type text NOT NULL,
                geometry_json text NOT NULL,
                properties_json text NOT NULL
            ) ON COMMIT DROP;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task CopyFeaturesAsync(
        NpgsqlConnection connection,
        Guid networkId,
        IReadOnlyList<GeoMapImportFeature> features,
        CancellationToken cancellationToken)
    {
        const string copySql = """
            COPY geomap_import_stage (
                sequence, id, source_id, source_layer, source_type,
                geometry_json, properties_json)
            FROM STDIN (FORMAT BINARY)
            """;

        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);
        foreach (var feature in features)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(feature.Sequence, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(
                DeterministicGeoMapIds.Feature(networkId, feature),
                NpgsqlDbType.Uuid,
                cancellationToken);
            await WriteNullableTextAsync(writer, feature.SourceId, cancellationToken);
            await WriteNullableTextAsync(writer, feature.SourceLayer, cancellationToken);
            await writer.WriteAsync(feature.SourceType, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(feature.GeometryJson, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(feature.PropertiesJson, NpgsqlDbType.Text, cancellationToken);
        }
        await writer.CompleteAsync(cancellationToken);
    }

    private static Task WriteNullableTextAsync(
        NpgsqlBinaryImporter writer,
        string? value,
        CancellationToken cancellationToken) =>
        value is null
            ? writer.WriteNullAsync(cancellationToken)
            : writer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);

    internal static async Task<int> CountExistingFeaturesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid networkId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateSnapshotCommand(
            "SELECT count(*)::integer FROM network_features WHERE network_id = @network_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("network_id", networkId);
        return (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    internal static async Task DeleteExistingFeaturesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid networkId,
        CancellationToken cancellationToken)
    {
        await using var command = CreateSnapshotCommand(
            "DELETE FROM network_features WHERE network_id = @network_id;",
            connection,
            transaction);
        command.Parameters.AddWithValue("network_id", networkId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task<int> InsertStagedFeaturesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid networkId,
        int sourceSrid,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO network_features (
                id, network_id, geometry, source_id, source_layer,
                source_type, properties)
            SELECT
                id,
                @network_id,
                ST_Transform(
                    ST_SetSRID(ST_GeomFromGeoJSON(geometry_json), @source_srid),
                    4326),
                source_id,
                source_layer,
                source_type,
                properties_json::jsonb
            FROM geomap_import_stage
            ORDER BY sequence;
            """;

        await using var command = CreateSnapshotCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("network_id", networkId);
        command.Parameters.AddWithValue("source_srid", sourceSrid);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static NpgsqlCommand CreateSnapshotCommand(
        string sql,
        NpgsqlConnection? connection,
        NpgsqlTransaction? transaction) =>
        new(sql, connection, transaction)
        {
            CommandTimeout = SnapshotCommandTimeoutSeconds
        };
}

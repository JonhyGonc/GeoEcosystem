using System.Diagnostics;
using Npgsql;
using NpgsqlTypes;

namespace GeoMap.DatasetGenerator;

public sealed class DatasetLoader(SyntheticFeatureGenerator generator)
{
    public async Task<DatasetLoadResult> LoadAsync(
        DatasetCommandOptions options,
        Action<DatasetLoadProgress>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        var projectId = SyntheticFeatureGenerator.CreateDatasetEntityId(
            options.Profile, options.Seed, "project");
        var networkId = SyntheticFeatureGenerator.CreateDatasetEntityId(
            options.Profile, options.Seed, "network");

        await using var dataSource = NpgsqlDataSource.Create(options.ConnectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await VerifySpatialModelAsync(connection, transaction, cancellationToken);
        await EnsureDatasetDoesNotExistAsync(
            connection, transaction, projectId, networkId, options, cancellationToken);
        await InsertDatasetContainersAsync(
            connection, transaction, projectId, networkId, options, cancellationToken);
        await CreateStagingTableAsync(connection, transaction, cancellationToken);

        for (var offset = 0; offset < options.Profile.FeatureCount; offset += options.BatchSize)
        {
            var count = Math.Min(options.BatchSize, options.Profile.FeatureCount - offset);
            await CopyBatchToStagingAsync(
                connection,
                options.Profile,
                options.Seed,
                offset,
                count,
                cancellationToken);
            await InsertStagedBatchAsync(
                connection,
                transaction,
                networkId,
                options,
                cancellationToken);
            await TruncateStagingTableAsync(connection, transaction, cancellationToken);

            reportProgress?.Invoke(new DatasetLoadProgress(
                offset + count,
                options.Profile.FeatureCount,
                stopwatch.Elapsed));
        }

        var statistics = await ReadStatisticsAsync(
            connection, transaction, networkId, cancellationToken);
        if (statistics.FeatureCount != options.Profile.FeatureCount)
        {
            throw new InvalidOperationException(
                $"Loaded {statistics.FeatureCount} features; expected {options.Profile.FeatureCount}.");
        }

        await transaction.CommitAsync(cancellationToken);
        stopwatch.Stop();

        return new DatasetLoadResult(
            projectId,
            networkId,
            statistics.FeatureCount,
            statistics.Extent,
            stopwatch.Elapsed);
    }

    private static async Task VerifySpatialModelAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT to_regclass('public.projects') IS NOT NULL
               AND to_regclass('public.networks') IS NOT NULL
               AND to_regclass('public.network_features') IS NOT NULL;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var isReady = (bool?)await command.ExecuteScalarAsync(cancellationToken) ?? false;
        if (!isReady)
        {
            throw new InvalidOperationException(
                "The spatial model is missing. Apply database migration 001 before loading a dataset.");
        }
    }

    private static async Task EnsureDatasetDoesNotExistAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid networkId,
        DatasetCommandOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM projects WHERE id = @project_id
                UNION ALL
                SELECT 1 FROM networks WHERE id = @network_id
            );
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("network_id", networkId);
        var exists = (bool?)await command.ExecuteScalarAsync(cancellationToken) ?? false;
        if (exists)
        {
            throw new InvalidOperationException(
                $"Dataset {options.Profile.Name.ToUpperInvariant()} with seed {options.Seed} already exists. " +
                "Use another seed or delete its synthetic project explicitly before loading it again.");
        }
    }

    private static async Task InsertDatasetContainersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid projectId,
        Guid networkId,
        DatasetCommandOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO projects (id, name)
            VALUES (@project_id, @project_name);

            INSERT INTO networks (id, project_id, name, type)
            VALUES (@network_id, @project_id, @network_name, 'synthetic-drainage');
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("project_id", projectId);
        command.Parameters.AddWithValue("network_id", networkId);
        command.Parameters.AddWithValue(
            "project_name",
            $"GeoMap scale datasets - seed {options.Seed}");
        command.Parameters.AddWithValue(
            "network_name",
            $"GeoMap Synthetic {options.Profile.Name.ToUpperInvariant()} - seed {options.Seed}");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreateStagingTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TEMP TABLE synthetic_feature_stage (
                id uuid NOT NULL,
                sequence integer NOT NULL,
                source_id text NOT NULL,
                start_longitude double precision NOT NULL,
                start_latitude double precision NOT NULL,
                end_longitude double precision NOT NULL,
                end_latitude double precision NOT NULL,
                diameter_mm integer NOT NULL,
                material text NOT NULL
            ) ON COMMIT DROP;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task CopyBatchToStagingAsync(
        NpgsqlConnection connection,
        DatasetProfile profile,
        int seed,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        const string copySql = """
            COPY synthetic_feature_stage (
                id,
                sequence,
                source_id,
                start_longitude,
                start_latitude,
                end_longitude,
                end_latitude,
                diameter_mm,
                material
            ) FROM STDIN (FORMAT BINARY)
            """;

        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);
        foreach (var feature in generator.Generate(profile, seed, offset, count))
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(feature.Id, NpgsqlDbType.Uuid, cancellationToken);
            await writer.WriteAsync(feature.Sequence, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(feature.SourceId, NpgsqlDbType.Text, cancellationToken);
            await writer.WriteAsync(feature.StartLongitude, NpgsqlDbType.Double, cancellationToken);
            await writer.WriteAsync(feature.StartLatitude, NpgsqlDbType.Double, cancellationToken);
            await writer.WriteAsync(feature.EndLongitude, NpgsqlDbType.Double, cancellationToken);
            await writer.WriteAsync(feature.EndLatitude, NpgsqlDbType.Double, cancellationToken);
            await writer.WriteAsync(feature.DiameterMillimeters, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(feature.Material, NpgsqlDbType.Text, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task InsertStagedBatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid networkId,
        DatasetCommandOptions options,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO network_features (
                id,
                network_id,
                geometry,
                source_id,
                source_layer,
                source_type,
                properties)
            SELECT
                id,
                @network_id,
                ST_SetSRID(
                    ST_MakeLine(
                        ST_MakePoint(start_longitude, start_latitude),
                        ST_MakePoint(end_longitude, end_latitude)),
                    4326),
                source_id,
                'SYNTHETIC-' || upper(@dataset),
                'SyntheticLineString',
                jsonb_build_object(
                    'dataset', @dataset,
                    'seed', @seed,
                    'sequence', sequence,
                    'diameter_mm', diameter_mm,
                    'material', material)
            FROM synthetic_feature_stage
            ORDER BY sequence;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("network_id", networkId);
        command.Parameters.AddWithValue("dataset", options.Profile.Name);
        command.Parameters.AddWithValue("seed", options.Seed);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TruncateStagingTableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "TRUNCATE TABLE synthetic_feature_stage;", connection, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DatasetStatistics> ReadStatisticsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid networkId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                count(*)::integer,
                ST_XMin(Box3D(ST_Extent(geometry))),
                ST_YMin(Box3D(ST_Extent(geometry))),
                ST_XMax(Box3D(ST_Extent(geometry))),
                ST_YMax(Box3D(ST_Extent(geometry)))
            FROM network_features
            WHERE network_id = @network_id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("network_id", networkId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new DatasetStatistics(
            reader.GetInt32(0),
            new DatasetExtent(
                reader.GetDouble(1),
                reader.GetDouble(2),
                reader.GetDouble(3),
                reader.GetDouble(4)));
    }

    private sealed record DatasetStatistics(int FeatureCount, DatasetExtent Extent);
}

public sealed record DatasetLoadResult(
    Guid ProjectId,
    Guid NetworkId,
    int FeatureCount,
    DatasetExtent ActualExtent,
    TimeSpan Elapsed);

public sealed record DatasetLoadProgress(
    int LoadedFeatureCount,
    int TotalFeatureCount,
    TimeSpan Elapsed)
{
    public double Percentage => LoadedFeatureCount * 100d / TotalFeatureCount;
}

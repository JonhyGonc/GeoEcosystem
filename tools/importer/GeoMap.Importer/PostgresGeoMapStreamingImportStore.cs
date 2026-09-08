using System.Diagnostics;
using GeoMap.Contract;
using Npgsql;

namespace GeoMap.Importer;

public sealed class PostgresGeoMapStreamingImportStore : IGeoMapStreamingImportStore
{
    public async Task<GeoMapImportResult> ImportAsync(
        string connectionString,
        GeoMapImportMetadata metadata,
        IAsyncEnumerable<GeoMapImportFeature> features,
        int batchSize,
        Action<ImportProgress>? reportProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(features);
        if (batchSize is < ImporterCommandOptions.MinimumBatchSize or > ImporterCommandOptions.MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        var stopwatch = Stopwatch.StartNew();
        var projectId = DeterministicGeoMapIds.Project(metadata);
        var networkId = DeterministicGeoMapIds.Network(projectId, metadata);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await PostgresGeoMapImportStore.VerifyDatabaseAsync(
            connection, null, metadata.SourceSrid, cancellationToken);
        await AcquireSessionLockAsync(connection, networkId, cancellationToken);
        try
        {
            await CreateStreamingStagingTableAsync(connection, cancellationToken);
            var batch = new List<GeoMapImportFeature>(batchSize);
            var processed = 0;
            var batchNumber = 0;

            await foreach (var feature in features.WithCancellation(cancellationToken))
            {
                if (processed >= GeoMapContractValidator.MaximumStreamingFeatures)
                {
                    throw new GeoMapImportException(
                        ContractErrorCodes.LimitExceeded,
                        "$.features",
                        "Streaming import exceeds 5,000,000 features.");
                }

                batch.Add(feature);
                processed++;
                if (batch.Count < batchSize) continue;

                batchNumber++;
                await AppendBatchAsync(
                    connection, networkId, batch, cancellationToken);
                reportProgress?.Invoke(new(
                    "progress", processed, batchNumber, batch.Count, stopwatch.ElapsedMilliseconds));
                batch.Clear();
            }

            if (batch.Count > 0)
            {
                batchNumber++;
                await AppendBatchAsync(
                    connection, networkId, batch, cancellationToken);
                reportProgress?.Invoke(new(
                    "progress", processed, batchNumber, batch.Count, stopwatch.ElapsedMilliseconds));
            }

            if (processed == 0)
            {
                throw new GeoMapImportException(
                    ContractErrorCodes.FieldInvalid,
                    "$.features",
                    "Streaming import requires at least one feature record.");
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await PostgresGeoMapImportStore.UpsertContainersAsync(
                    connection, transaction, projectId, networkId, metadata, cancellationToken);
                var replaced = await PostgresGeoMapImportStore.CountExistingFeaturesAsync(
                    connection, transaction, networkId, cancellationToken);
                await PostgresGeoMapImportStore.DeleteExistingFeaturesAsync(
                    connection, transaction, networkId, cancellationToken);
                var imported = await PostgresGeoMapImportStore.InsertStagedFeaturesAsync(
                    connection, transaction, networkId, metadata.SourceSrid, cancellationToken);
                if (imported != processed)
                {
                    throw new InvalidOperationException(
                        $"Imported {imported} features; expected {processed}.");
                }

                await transaction.CommitAsync(cancellationToken);
                return new(projectId, networkId, imported, replaced, batchNumber);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            await ReleaseSessionLockAsync(connection, networkId);
        }
    }

    private static async Task CreateStreamingStagingTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TEMP TABLE geomap_import_stage (
                sequence integer NOT NULL,
                id uuid PRIMARY KEY,
                source_id text,
                source_layer text,
                source_type text NOT NULL,
                geometry_json text NOT NULL,
                properties_json text NOT NULL
            ) ON COMMIT PRESERVE ROWS;

            CREATE UNIQUE INDEX ux_geomap_import_stage_source
                ON geomap_import_stage (source_type, source_id)
                WHERE source_id IS NOT NULL;
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AppendBatchAsync(
        NpgsqlConnection connection,
        Guid networkId,
        IReadOnlyList<GeoMapImportFeature> batch,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await PostgresGeoMapImportStore.CopyFeaturesAsync(
                connection, networkId, batch, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new GeoMapImportException(
                ContractErrorCodes.SourceIdDuplicate,
                "$.features",
                "sourceId must be unique for each sourceType in the streaming import.",
                exception);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task AcquireSessionLockAsync(
        NpgsqlConnection connection,
        Guid networkId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_lock(hashtextextended(@network_id, 0));",
            connection);
        command.Parameters.AddWithValue("network_id", networkId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReleaseSessionLockAsync(
        NpgsqlConnection connection,
        Guid networkId)
    {
        if (connection.State != System.Data.ConnectionState.Open) return;
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_unlock(hashtextextended(@network_id, 0));",
            connection);
        command.Parameters.AddWithValue("network_id", networkId.ToString("D"));
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}

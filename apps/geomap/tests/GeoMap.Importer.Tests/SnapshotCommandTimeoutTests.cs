using Npgsql;
using Xunit;

namespace GeoMap.Importer.Tests;

public sealed class SnapshotCommandTimeoutTests
{
    private const int NpgsqlDefaultCommandTimeoutSeconds = 30;

    [Fact]
    public void Snapshot_timeout_exceeds_npgsql_default_and_remains_finite()
    {
        Assert.True(
            PostgresGeoMapImportStore.SnapshotCommandTimeoutSeconds
                > NpgsqlDefaultCommandTimeoutSeconds);
        Assert.True(PostgresGeoMapImportStore.SnapshotCommandTimeoutSeconds > 0);
    }

    [Fact]
    public void CreateSnapshotCommand_applies_explicit_timeout()
    {
        using var defaultCommand = new NpgsqlCommand("SELECT 1;");
        using var snapshotCommand = PostgresGeoMapImportStore.CreateSnapshotCommand(
            "SELECT 1;", null, null);

        Assert.Equal(NpgsqlDefaultCommandTimeoutSeconds, defaultCommand.CommandTimeout);
        Assert.Equal(
            PostgresGeoMapImportStore.SnapshotCommandTimeoutSeconds,
            snapshotCommand.CommandTimeout);
        Assert.Equal("SELECT 1;", snapshotCommand.CommandText);
    }
}

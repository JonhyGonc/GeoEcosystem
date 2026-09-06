using GeoMap.DatasetGenerator;
using Xunit;

namespace GeoMap.DatasetGenerator.Tests;

public sealed class DatasetCommandOptionsTests
{
    [Fact]
    public void Parses_size_seed_batch_and_connection_string()
    {
        var options = DatasetCommandOptions.Parse(
        [
            "--size", "medium",
            "--seed", "123",
            "--batch-size", "2500",
            "--connection-string", "Host=localhost;Database=geomap"
        ]);

        Assert.Equal(DatasetProfile.Medium, options.Profile);
        Assert.Equal(123, options.Seed);
        Assert.Equal(2_500, options.BatchSize);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("50001")]
    public void Rejects_batch_size_outside_the_controlled_range(string batchSize)
    {
        var action = () => DatasetCommandOptions.Parse(
        [
            "--size", "small",
            "--batch-size", batchSize,
            "--connection-string", "Host=localhost;Database=geomap"
        ]);

        Assert.Throws<DatasetOptionException>(action);
    }
}

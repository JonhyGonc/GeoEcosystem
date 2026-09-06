using GeoMap.DatasetGenerator;
using Xunit;

namespace GeoMap.DatasetGenerator.Tests;

public sealed class SyntheticFeatureGeneratorTests
{
    private readonly SyntheticFeatureGenerator _generator = new();

    [Fact]
    public void Same_seed_produces_the_same_features()
    {
        var first = _generator.Generate(DatasetProfile.Small, 42).Take(100).ToArray();
        var second = _generator.Generate(DatasetProfile.Small, 42).Take(100).ToArray();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_seed_changes_ids_geometry_and_attributes()
    {
        var first = _generator.Generate(DatasetProfile.Small, 42).First();
        var second = _generator.Generate(DatasetProfile.Small, 43).First();

        Assert.NotEqual(first, second);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.StartLatitude, second.StartLatitude);
    }

    [Theory]
    [InlineData("small", 1_000)]
    [InlineData("medium", 100_000)]
    public void Profile_generates_the_exact_expected_count(string size, int expectedCount)
    {
        var profile = DatasetProfile.Parse(size);

        Assert.Equal(expectedCount, _generator.Generate(profile, 20260906).Count());
    }

    [Theory]
    [InlineData("small")]
    [InlineData("medium")]
    public void Every_coordinate_is_finite_and_inside_the_declared_extent(string size)
    {
        var profile = DatasetProfile.Parse(size);

        foreach (var feature in _generator.Generate(profile, 20260906))
        {
            Assert.True(double.IsFinite(feature.StartLongitude));
            Assert.True(double.IsFinite(feature.StartLatitude));
            Assert.True(double.IsFinite(feature.EndLongitude));
            Assert.True(double.IsFinite(feature.EndLatitude));
            Assert.True(profile.Extent.Contains(feature.StartLongitude, feature.StartLatitude));
            Assert.True(profile.Extent.Contains(feature.EndLongitude, feature.EndLatitude));
            Assert.NotEqual(feature.StartLongitude, feature.EndLongitude);
        }
    }

    [Fact]
    public void A_requested_slice_matches_the_same_slice_of_the_full_sequence()
    {
        var expected = _generator.Generate(DatasetProfile.Small, 77)
            .Skip(250)
            .Take(125)
            .ToArray();

        var actual = _generator.Generate(DatasetProfile.Small, 77, 250, 125).ToArray();

        Assert.Equal(expected, actual);
    }
}

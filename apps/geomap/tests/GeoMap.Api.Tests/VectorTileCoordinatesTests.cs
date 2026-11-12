using GeoMap.Api.VectorTiles;
using Xunit;

namespace GeoMap.Api.Tests;

public sealed class VectorTileCoordinatesTests
{
    [Theory]
    [InlineData(14, 5_969, 9_433)]
    [InlineData(16, 23_876, 37_732)]
    [InlineData(22, 1_527_984, 2_414_928)]
    public void Valid_coordinates_are_accepted(int zoom, int x, int y)
    {
        var result = VectorTileCoordinates.Validate(zoom, x, y);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(new VectorTileCoordinates(zoom, x, y), result.Coordinates);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(23)]
    [InlineData(-1)]
    public void Zoom_outside_the_supported_range_is_rejected(int zoom)
    {
        var result = VectorTileCoordinates.Validate(zoom, 0, 0);

        Assert.False(result.IsValid);
        Assert.Contains("z", result.Errors.Keys);
    }

    [Theory]
    [InlineData(-1, 0, "x")]
    [InlineData(16_384, 0, "x")]
    [InlineData(0, -1, "y")]
    [InlineData(0, 16_384, "y")]
    public void Coordinate_outside_the_zoom_grid_is_rejected(
        int x,
        int y,
        string expectedError)
    {
        var result = VectorTileCoordinates.Validate(14, x, y);

        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.Errors.Keys);
    }
}

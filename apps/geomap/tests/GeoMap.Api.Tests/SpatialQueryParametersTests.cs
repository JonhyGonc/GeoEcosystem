using GeoMap.Api.SpatialQueries;
using Xunit;

namespace GeoMap.Api.Tests;

public sealed class SpatialQueryParametersTests
{
    [Fact]
    public void Valid_bounding_box_uses_the_default_limit()
    {
        var parameters = CreateValidParameters();

        var result = parameters.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(SpatialQueryParameters.DefaultLimit, result.Request!.Limit);
    }

    [Fact]
    public void Valid_custom_limit_is_preserved()
    {
        var parameters = CreateValidParameters(SpatialQueryParameters.MaximumLimit);

        var result = parameters.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(SpatialQueryParameters.MaximumLimit, result.Request!.Limit);
    }

    [Fact]
    public void Missing_coordinates_are_all_reported()
    {
        var result = new SpatialQueryParameters().Validate();

        Assert.False(result.IsValid);
        Assert.Equal(4, result.Errors.Count);
        Assert.Contains("minLongitude", result.Errors.Keys);
        Assert.Contains("minLatitude", result.Errors.Keys);
        Assert.Contains("maxLongitude", result.Errors.Keys);
        Assert.Contains("maxLatitude", result.Errors.Keys);
    }

    [Theory]
    [InlineData(double.NaN, -26.4, -48.7, -26.2, "minLongitude")]
    [InlineData(double.NegativeInfinity, -26.4, -48.7, -26.2, "minLongitude")]
    [InlineData(-181, -26.4, -48.7, -26.2, "minLongitude")]
    [InlineData(-48.9, -91, -48.7, -26.2, "minLatitude")]
    [InlineData(-48.9, -26.4, 181, -26.2, "maxLongitude")]
    public void Non_finite_or_out_of_range_coordinate_is_rejected(
        double minLongitude,
        double minLatitude,
        double maxLongitude,
        double maxLatitude,
        string expectedError)
    {
        var parameters = new SpatialQueryParameters
        {
            MinLongitude = minLongitude,
            MinLatitude = minLatitude,
            MaxLongitude = maxLongitude,
            MaxLatitude = maxLatitude
        };

        var result = parameters.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.Errors.Keys);
    }

    [Theory]
    [InlineData(-48.7, -48.9, "longitude")]
    [InlineData(-48.9, -48.9, "longitude")]
    public void Reversed_or_empty_longitude_range_is_rejected(
        double minLongitude,
        double maxLongitude,
        string expectedError)
    {
        var parameters = new SpatialQueryParameters
        {
            MinLongitude = minLongitude,
            MinLatitude = -26.4,
            MaxLongitude = maxLongitude,
            MaxLatitude = -26.2
        };

        var result = parameters.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(expectedError, result.Errors.Keys);
    }

    [Theory]
    [InlineData(-26.2, -26.4)]
    [InlineData(-26.3, -26.3)]
    public void Reversed_or_empty_latitude_range_is_rejected(
        double minLatitude,
        double maxLatitude)
    {
        var parameters = new SpatialQueryParameters
        {
            MinLongitude = -48.9,
            MinLatitude = minLatitude,
            MaxLongitude = -48.7,
            MaxLatitude = maxLatitude
        };

        var result = parameters.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("latitude", result.Errors.Keys);
    }

    [Fact]
    public void Excessive_longitude_span_is_rejected()
    {
        var parameters = new SpatialQueryParameters
        {
            MinLongitude = -49.5,
            MinLatitude = -26.4,
            MaxLongitude = -48.0,
            MaxLatitude = -26.2
        };

        var result = parameters.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("longitude", result.Errors.Keys);
    }

    [Fact]
    public void Excessive_latitude_span_is_rejected()
    {
        var parameters = new SpatialQueryParameters
        {
            MinLongitude = -48.9,
            MinLatitude = -27.0,
            MaxLongitude = -48.7,
            MaxLatitude = -25.5
        };

        var result = parameters.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("latitude", result.Errors.Keys);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2001)]
    [InlineData(int.MaxValue)]
    public void Limit_outside_the_allowed_range_is_rejected(int limit)
    {
        var result = CreateValidParameters(limit).Validate();

        Assert.False(result.IsValid);
        Assert.Contains("limit", result.Errors.Keys);
    }

    private static SpatialQueryParameters CreateValidParameters(int? limit = null) =>
        new()
        {
            MinLongitude = -48.90,
            MinLatitude = -26.35,
            MaxLongitude = -48.85,
            MaxLatitude = -26.30,
            Limit = limit
        };
}

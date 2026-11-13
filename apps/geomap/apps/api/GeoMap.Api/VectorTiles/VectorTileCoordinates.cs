namespace GeoMap.Api.VectorTiles;

public sealed record VectorTileCoordinates(int Zoom, int X, int Y)
{
    public const int MinimumZoom = 14;
    public const int MaximumZoom = 22;

    public static VectorTileValidationResult Validate(int zoom, int x, int y)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (zoom is < MinimumZoom or > MaximumZoom)
        {
            errors["z"] =
            [
                $"z must be between {MinimumZoom} and {MaximumZoom}."
            ];
        }
        else
        {
            var tileCount = 1 << zoom;
            if (x < 0 || x >= tileCount)
            {
                errors["x"] = [$"x must be between 0 and {tileCount - 1} for zoom {zoom}."];
            }

            if (y < 0 || y >= tileCount)
            {
                errors["y"] = [$"y must be between 0 and {tileCount - 1} for zoom {zoom}."];
            }
        }

        return errors.Count == 0
            ? new VectorTileValidationResult(new VectorTileCoordinates(zoom, x, y), errors)
            : new VectorTileValidationResult(null, errors);
    }
}

public sealed record VectorTileValidationResult(
    VectorTileCoordinates? Coordinates,
    IDictionary<string, string[]> Errors)
{
    public bool IsValid => Coordinates is not null;
}

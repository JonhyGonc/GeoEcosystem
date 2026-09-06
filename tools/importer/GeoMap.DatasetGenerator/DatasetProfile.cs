namespace GeoMap.DatasetGenerator;

public sealed record DatasetProfile(
    string Name,
    int FeatureCount,
    DatasetExtent Extent)
{
    public static DatasetProfile Small { get; } = new(
        "small",
        1_000,
        DatasetExtent.JoinvilleTestArea);

    public static DatasetProfile Medium { get; } = new(
        "medium",
        100_000,
        DatasetExtent.JoinvilleTestArea);

    public static DatasetProfile Parse(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "small" => Small,
            "medium" => Medium,
            _ => throw new DatasetOptionException(
                $"Unknown dataset size '{value}'. Use 'small' or 'medium'.")
        };
}

public readonly record struct DatasetExtent(
    double MinLongitude,
    double MinLatitude,
    double MaxLongitude,
    double MaxLatitude)
{
    public static DatasetExtent JoinvilleTestArea { get; } =
        new(-48.95, -26.40, -48.75, -26.20);

    public bool Contains(double longitude, double latitude) =>
        longitude >= MinLongitude && longitude <= MaxLongitude &&
        latitude >= MinLatitude && latitude <= MaxLatitude;
}

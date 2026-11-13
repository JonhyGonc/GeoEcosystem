namespace GeoMap.Api.SpatialQueries;

public sealed class SpatialQueryParameters
{
    public const int DefaultLimit = 500;
    public const int MaximumLimit = 2_000;
    public const double MaximumLongitudeSpan = 1.0;
    public const double MaximumLatitudeSpan = 1.0;

    public double? MinLongitude { get; set; }

    public double? MinLatitude { get; set; }

    public double? MaxLongitude { get; set; }

    public double? MaxLatitude { get; set; }

    public int? Limit { get; set; }

    public SpatialQueryValidationResult Validate()
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        ValidateCoordinate(errors, "minLongitude", MinLongitude, -180, 180);
        ValidateCoordinate(errors, "maxLongitude", MaxLongitude, -180, 180);
        ValidateCoordinate(errors, "minLatitude", MinLatitude, -90, 90);
        ValidateCoordinate(errors, "maxLatitude", MaxLatitude, -90, 90);

        var resultLimit = Limit ?? DefaultLimit;
        if (resultLimit is < 1 or > MaximumLimit)
        {
            errors["limit"] =
            [
                $"limit must be between 1 and {MaximumLimit}."
            ];
        }

        if (errors.Count == 0)
        {
            if (MinLongitude!.Value >= MaxLongitude!.Value)
            {
                errors["longitude"] =
                [
                    "minLongitude must be smaller than maxLongitude."
                ];
            }

            if (MinLatitude!.Value >= MaxLatitude!.Value)
            {
                errors["latitude"] =
                [
                    "minLatitude must be smaller than maxLatitude."
                ];
            }
        }

        if (errors.Count == 0 && MaxLongitude!.Value - MinLongitude!.Value > MaximumLongitudeSpan)
        {
            errors["longitude"] =
            [
                $"Bounding box longitude span cannot exceed {MaximumLongitudeSpan} degree."
            ];
        }

        if (errors.Count == 0 && MaxLatitude!.Value - MinLatitude!.Value > MaximumLatitudeSpan)
        {
            errors["latitude"] =
            [
                $"Bounding box latitude span cannot exceed {MaximumLatitudeSpan} degree."
            ];
        }

        if (errors.Count > 0)
        {
            return new SpatialQueryValidationResult(null, errors);
        }

        return new SpatialQueryValidationResult(
            new SpatialQueryRequest(
                new SpatialEnvelope(
                    MinLongitude!.Value,
                    MinLatitude!.Value,
                    MaxLongitude!.Value,
                    MaxLatitude!.Value),
                resultLimit),
            errors);
    }

    private static void ValidateCoordinate(
        IDictionary<string, string[]> errors,
        string name,
        double? value,
        double minimum,
        double maximum)
    {
        if (value is null)
        {
            errors[name] = [$"{name} is required."];
            return;
        }

        if (!double.IsFinite(value.Value) || value.Value < minimum || value.Value > maximum)
        {
            errors[name] = [$"{name} must be a finite number between {minimum} and {maximum}."];
        }
    }
}

public sealed record SpatialQueryValidationResult(
    SpatialQueryRequest? Request,
    IDictionary<string, string[]> Errors)
{
    public bool IsValid => Request is not null;
}

public sealed record SpatialQueryRequest(SpatialEnvelope Envelope, int Limit);

public sealed record SpatialEnvelope(
    double MinLongitude,
    double MinLatitude,
    double MaxLongitude,
    double MaxLatitude);

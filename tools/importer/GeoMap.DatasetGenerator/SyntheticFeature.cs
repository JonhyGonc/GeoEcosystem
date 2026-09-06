namespace GeoMap.DatasetGenerator;

public sealed record SyntheticFeature(
    Guid Id,
    int Sequence,
    string SourceId,
    double StartLongitude,
    double StartLatitude,
    double EndLongitude,
    double EndLatitude,
    int DiameterMillimeters,
    string Material);

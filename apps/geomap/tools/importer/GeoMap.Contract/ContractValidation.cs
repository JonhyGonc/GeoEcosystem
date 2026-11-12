namespace GeoMap.Contract;

public static class ContractErrorCodes
{
    public const string DocumentInvalid = "GMC_DOCUMENT_INVALID";
    public const string VersionUnsupported = "GMC_VERSION_UNSUPPORTED";
    public const string RequiredField = "GMC_REQUIRED_FIELD";
    public const string FieldInvalid = "GMC_FIELD_INVALID";
    public const string LimitExceeded = "GMC_LIMIT_EXCEEDED";
    public const string CrsInvalid = "GMC_CRS_INVALID";
    public const string UnitsUnsupported = "GMC_UNITS_UNSUPPORTED";
    public const string GeometryTypeUnsupported = "GMC_GEOMETRY_TYPE_UNSUPPORTED";
    public const string GeometryEmpty = "GMC_GEOMETRY_EMPTY";
    public const string GeometryCoordinateInvalid = "GMC_GEOMETRY_COORDINATE_INVALID";
    public const string GeometryDimensionUnsupported = "GMC_GEOMETRY_DIMENSION_UNSUPPORTED";
    public const string SourceIdDuplicate = "GMC_SOURCE_ID_DUPLICATE";
    public const string PropertiesInvalid = "GMC_PROPERTIES_INVALID";
}

public sealed record ContractValidationError(
    string Code,
    string Path,
    string Message);

public sealed record ContractValidationResult(
    IReadOnlyList<ContractValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

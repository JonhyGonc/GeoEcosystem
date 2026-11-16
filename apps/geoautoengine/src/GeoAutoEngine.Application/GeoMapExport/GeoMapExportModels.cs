namespace GeoAutoEngine.Application.GeoMapExport;

public readonly record struct GeoMapCoordinate(double X, double Y);

/// <param name="SourceId">Código próprio da feição (GUID em XData, DEC-ECO-0003); vira o <c>sourceId</c> no GeoMap.</param>
/// <param name="CadHandle">Handle do AutoCAD, enviado como atributo <c>cadHandle</c> para rastreio.</param>
public sealed record GeoMapExportFeature(
    string SourceId,
    string CadHandle,
    string SourceLayer,
    IReadOnlyList<GeoMapCoordinate> Vertices,
    bool IsClosed,
    double Elevation,
    bool ProjectedTo2D);

public sealed record GeoMapExportRequest(
    string SourceCrs,
    string Units,
    string ProjectExternalId,
    string ProjectName,
    string NetworkExternalId,
    string NetworkName,
    string NetworkType,
    DateTimeOffset ExportedAt,
    IReadOnlyList<GeoMapExportFeature> Features);

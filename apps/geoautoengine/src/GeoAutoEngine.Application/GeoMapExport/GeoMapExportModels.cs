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
    bool ProjectedTo2D,
    GeoMapBlockInfo? Block = null);

/// <summary>
/// Dados de um bloco (BlockReference) exportado como ponto, no contrato GeoMap 0.3 (M4.3c; DEC-029 do GeoMap).
/// Uma feição com bloco tem exatamente uma posição em <see cref="GeoMapExportFeature.Vertices"/>.
/// </summary>
/// <param name="Name">Nome do bloco (nome efetivo, no caso de bloco dinâmico); vai em <c>properties.bloco</c>.</param>
/// <param name="PositionFromSymbol">Verdadeiro quando a posição é o centro do símbolo, e não o ponto de inserção.</param>
public sealed record GeoMapBlockInfo(
    string Name,
    double RotationDegrees,
    double ScaleX,
    double ScaleY,
    bool PositionFromSymbol);

public sealed record GeoMapExportRequest(
    string SourceCrs,
    string Units,
    string ProjectExternalId,
    string ProjectName,
    string NetworkExternalId,
    string NetworkName,
    string NetworkType,
    DateTimeOffset ExportedAt,
    IReadOnlyList<GeoMapExportFeature> Features,
    string? NetworkColor = null);

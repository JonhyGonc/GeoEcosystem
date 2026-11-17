namespace GeoAutoEngine.Application.GeoMapExport;

public readonly record struct GeoMapCoordinate(double X, double Y);

/// <param name="SourceId">Código próprio da feição (GUID em XData, DEC-ECO-0003); vira o <c>sourceId</c> no GeoMap.</param>
/// <param name="CadHandle">Handle do AutoCAD, enviado como atributo <c>cadHandle</c> para rastreio.</param>
/// <param name="Bulges">
/// Arcos da linha (M4.3d), um bulge por vértice, na convenção do AutoCAD; <c>null</c> quando todos os segmentos são retos.
/// Os arcos são trocados por trechos retos na serialização (<see cref="GeoMapArcTessellation"/>).
/// </param>
/// <param name="EntityType">Tipo da entidade no CAD: <c>Polyline</c>, <c>Line</c> ou <c>Arc</c> (M4.3d).</param>
/// <param name="EndElevation">Elevação do ponto final de uma LINE; a do ponto inicial fica em <paramref name="Elevation"/>.</param>
/// <param name="Depths">
/// Profundidades dos pontos <c>_TG</c> associadas à linha (<see cref="PipeDepthExport"/>); <c>null</c> quando a
/// associação não foi feita na exportação.
/// </param>
public sealed record GeoMapExportFeature(
    string SourceId,
    string CadHandle,
    string SourceLayer,
    IReadOnlyList<GeoMapCoordinate> Vertices,
    bool IsClosed,
    double Elevation,
    bool ProjectedTo2D,
    GeoMapBlockInfo? Block = null,
    IReadOnlyList<double>? Bulges = null,
    string EntityType = GeoMapEntityTypes.Polyline,
    double? EndElevation = null,
    GeoMapPipeDepths? Depths = null);

/// <summary>Profundidades de uma linha para o GeoMap, em metros, como foram medidas (sem média nem interpolação).</summary>
/// <param name="StartMetres">Profundidade no primeiro vértice, quando medida.</param>
/// <param name="EndMetres">Profundidade no último vértice, quando medida.</param>
public sealed record GeoMapPipeDepths(
    double? StartMetres,
    double? EndMetres,
    IReadOnlyList<GeoMapDepthMeasurement> Measurements);

/// <param name="Point">Nome do ponto do levantamento (atributo PONTO).</param>
/// <param name="Vertex">Índice do vértice (a partir de 0) quando o ponto está num vértice.</param>
/// <param name="StationMetres">Distância ao longo da linha, desde o primeiro vértice.</param>
/// <param name="Code">Descrição original do ponto (atributo DESC).</param>
/// <param name="Method"><see cref="PipeDepthMethods"/>.</param>
/// <param name="Shared">O ponto está na ponta de duas linhas que se continuam e vale para as duas.</param>
public sealed record GeoMapDepthMeasurement(
    string Point,
    double DepthMetres,
    int? Vertex,
    double StationMetres,
    string Code,
    string Method,
    bool Shared);

/// <summary>Tipos de linha exportados; o valor vai em <c>sourceType</c> e <c>cadEntityType</c>.</summary>
public static class GeoMapEntityTypes
{
    public const string Polyline = "Polyline";
    public const string Line = "Line";
    public const string Arc = "Arc";
}

/// <summary>
/// Dados de um bloco (BlockReference) exportado como ponto, no contrato GeoMap 0.3 (M4.3c; DEC-029 do GeoMap).
/// Uma feição com bloco tem exatamente uma posição em <see cref="GeoMapExportFeature.Vertices"/>.
/// </summary>
/// <param name="Name">Nome do bloco (nome efetivo, no caso de bloco dinâmico); vai em <c>properties.bloco</c>.</param>
/// <param name="PositionFromSymbol">Verdadeiro quando a posição é o centro do símbolo, e não o ponto de inserção.</param>
/// <param name="Attributes">Atributos do bloco (ATTRIB), tag e valor, na ordem do desenho (M4.3e).</param>
public sealed record GeoMapBlockInfo(
    string Name,
    double RotationDegrees,
    double ScaleX,
    double ScaleY,
    bool PositionFromSymbol,
    IReadOnlyList<KeyValuePair<string, string>>? Attributes = null);

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

using System.Globalization;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Ponto base e extensão do desenho de uma definição de bloco, nas coordenadas do próprio bloco.</summary>
public readonly record struct GeoMapBlockDefinitionExtents(
    double BaseX,
    double BaseY,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY);

/// <summary>Onde o ponto do bloco fica no mapa e se ele veio do centro do símbolo.</summary>
public readonly record struct GeoMapBlockPosition(GeoMapCoordinate Position, bool FromSymbol);

/// <summary>
/// Regras dos blocos exportados como pontos (M4.3c). São as mesmas da ponte DXF, registradas na DEC-029 do GeoMap.
/// </summary>
public static class GeoMapBlockRules
{
    /// <summary>Um símbolo a mais de 50 vezes o próprio tamanho do ponto base é tratado como desenhado fora dele.</summary>
    public const double FarSymbolFactor = 50.0;
    public const string SurveyLayerPrefix = "_TG";
    public const string SurveyPointBlockName = "SIMB_PONTO";
    public const string BlockNetworkIdSuffix = ":blocos";
    public const string BlockNetworkNameSuffix = " (blocos)";
    public const string BlockFileSuffix = "-blocos";
    public const string FileExtension = ".geomap.json";

    private const double MinimumSymbolSize = 1e-6;

    /// <summary>
    /// Motivo para deixar um bloco de fora, ou <c>null</c> quando ele entra. O levantamento topográfico
    /// nunca vai para o GeoMap: layers <c>_TG*</c> e o símbolo do ponto levantado. Blocos sem nome
    /// legível (anônimos, <c>*U…</c>) também ficam de fora.
    /// </summary>
    public static string? SkipReason(string layer, string blockName)
    {
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentNullException.ThrowIfNull(blockName);

        if (layer.Trim().StartsWith(SurveyLayerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "layer de levantamento (_TG*)";
        }

        var name = blockName.Trim();
        if (string.Equals(name, SurveyPointBlockName, StringComparison.OrdinalIgnoreCase))
        {
            return "ponto levantado (SIMB_PONTO)";
        }

        return name.Length == 0 || name.StartsWith("*", StringComparison.Ordinal) ? "bloco anônimo" : null;
    }

    /// <summary>
    /// Alguns blocos têm o desenho já nas coordenadas reais e o ponto base em 0,0: o AutoCAD mostra o
    /// símbolo no lugar certo, mas o ponto de inserção fica longe da obra. Nesses casos o ponto vai para o
    /// centro do símbolo como o AutoCAD o desenha (inserção + escala × rotação × centro do desenho).
    /// Os demais blocos mantêm o ponto de inserção.
    /// </summary>
    public static GeoMapBlockPosition Resolve(
        GeoMapCoordinate insertion,
        GeoMapBlockDefinitionExtents? definition,
        double rotationDegrees,
        double scaleX,
        double scaleY)
    {
        if (definition is not { } extents || !SymbolFarFromBase(extents))
        {
            return new GeoMapBlockPosition(insertion, false);
        }

        var centerX = (extents.MinX + extents.MaxX) / 2;
        var centerY = (extents.MinY + extents.MaxY) / 2;
        var localX = (centerX - extents.BaseX) * scaleX;
        var localY = (centerY - extents.BaseY) * scaleY;
        var angle = rotationDegrees * Math.PI / 180;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        return new GeoMapBlockPosition(
            new GeoMapCoordinate(
                insertion.X + (localX * cos) - (localY * sin),
                insertion.Y + (localX * sin) + (localY * cos)),
            true);
    }

    public static bool SymbolFarFromBase(GeoMapBlockDefinitionExtents extents)
    {
        var centerX = (extents.MinX + extents.MaxX) / 2;
        var centerY = (extents.MinY + extents.MaxY) / 2;
        var size = Hypot(extents.MaxX - extents.MinX, extents.MaxY - extents.MinY);
        var offset = Hypot(centerX - extents.BaseX, centerY - extents.BaseY);
        return offset > FarSymbolFactor * Math.Max(size, MinimumSymbolSize);
    }

    /// <summary>Rotação em graus, arredondada para não levar ruído de ponto flutuante (89,9999999…).</summary>
    public static double RotationDegrees(double radians) =>
        Math.Round(radians * 180 / Math.PI, 6, MidpointRounding.AwayFromZero);

    /// <summary>Os blocos vão numa rede separada das linhas (DEC-029): mesmo identificador com sufixo.</summary>
    public static string BlockNetworkExternalId(string lineNetworkExternalId) =>
        lineNetworkExternalId + BlockNetworkIdSuffix;

    public static string BlockNetworkName(string lineNetworkName) => lineNetworkName + BlockNetworkNameSuffix;

    /// <summary>Arquivo dos blocos, na mesma pasta do arquivo das linhas: <c>rede.geomap.json</c> → <c>rede-blocos.geomap.json</c>.</summary>
    public static string BlockFilePath(string lineFilePath)
    {
        ArgumentNullException.ThrowIfNull(lineFilePath);

        var stem = lineFilePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)
            ? lineFilePath.Substring(0, lineFilePath.Length - FileExtension.Length)
            : lineFilePath;
        return string.Concat(stem, BlockFileSuffix, FileExtension);
    }

    public static string Describe(IReadOnlyDictionary<string, int> skipped) =>
        string.Join(
            "; ",
            skipped
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => item.Key + "=" + item.Value.ToString(CultureInfo.InvariantCulture)));

    private static double Hypot(double x, double y) => Math.Sqrt((x * x) + (y * y));
}

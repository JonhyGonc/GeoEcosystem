using System.Globalization;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Diâmetro e material lidos do nome da layer (<c>RD_&lt;diâmetro&gt;[_&lt;material&gt;]</c>).</summary>
public readonly record struct GeoMapLayerAttributes(double? DiameterMm, string? Material)
{
    public static readonly GeoMapLayerAttributes None = new(null, null);
}

/// <summary>Uma rede do arquivo exportado: as linhas ou os blocos de uma layer.</summary>
public sealed record GeoMapLayerNetwork(string Layer, bool IsBlocks, IReadOnlyList<GeoMapExportFeature> Features);

/// <summary>
/// Uma rede por layer (M4.3e, decisão do usuário de 16/11/2026), como a ponte DXF: cada layer vira uma rede
/// com a própria cor; se a layer também tem blocos, eles vão numa rede separada (DEC-029 do GeoMap).
/// </summary>
public static class GeoMapLayerNetworks
{
    public const string LayerPrefix = "RD_";

    // Caracteres que o Windows não aceita em nome de arquivo.
    private static readonly char[] InvalidFileNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// <c>RD_150_PVC</c> → 150 mm e PVC; <c>RD_200</c> → 200 mm, sem material. Fora desse padrão, nada
    /// (sem erro). É a mesma convenção usada na validação com dados reais do GeoMap.
    /// </summary>
    public static GeoMapLayerAttributes Attributes(string layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        var name = layer.Trim();
        if (!name.StartsWith(LayerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return GeoMapLayerAttributes.None;
        }

        var rest = name.Substring(LayerPrefix.Length);
        var separator = rest.IndexOf('_');
        var number = separator < 0 ? rest : rest.Substring(0, separator);
        if (number.Length == 0 ||
            !number.All(character => character is >= '0' and <= '9' or '.' or ',') ||
            !double.TryParse(
                number.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var diameter) ||
            diameter <= 0)
        {
            return GeoMapLayerAttributes.None;
        }

        var material = separator < 0 ? null : rest.Substring(separator + 1).Trim();
        return new GeoMapLayerAttributes(diameter, string.IsNullOrEmpty(material) ? null : material);
    }

    /// <summary>Redes em ordem alfabética de layer; em cada layer, as linhas antes dos blocos.</summary>
    public static IReadOnlyList<GeoMapLayerNetwork> Split(IReadOnlyList<GeoMapExportFeature> features)
    {
        ArgumentNullException.ThrowIfNull(features);

        return features
            .GroupBy(feature => (feature.SourceLayer, IsBlocks: feature.Block is not null))
            .OrderBy(group => group.Key.SourceLayer, StringComparer.Ordinal)
            .ThenBy(group => group.Key.IsBlocks)
            .Select(group => new GeoMapLayerNetwork(group.Key.SourceLayer, group.Key.IsBlocks, group.ToArray()))
            .ToArray();
    }

    public static string NetworkExternalId(string projectExternalId, GeoMapLayerNetwork network) =>
        $"{projectExternalId}:{network.Layer}" + (network.IsBlocks ? GeoMapBlockRules.BlockNetworkIdSuffix : string.Empty);

    public static string NetworkName(GeoMapLayerNetwork network) =>
        network.IsBlocks ? GeoMapBlockRules.BlockNetworkName(network.Layer) : network.Layer;

    /// <summary>
    /// Arquivo da rede, na pasta do nome escolhido: <c>obra.geomap.json</c> → <c>obra-RD_150_PVC.geomap.json</c>
    /// e <c>obra-TÊ-blocos.geomap.json</c>.
    /// </summary>
    public static string FilePath(string chosenPath, GeoMapLayerNetwork network)
    {
        ArgumentNullException.ThrowIfNull(chosenPath);

        var stem = chosenPath.EndsWith(GeoMapBlockRules.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? chosenPath.Substring(0, chosenPath.Length - GeoMapBlockRules.FileExtension.Length)
            : chosenPath;
        var layer = new string(network.Layer
            .Select(character => character < ' ' || InvalidFileNameCharacters.Contains(character) ? '-' : character)
            .ToArray());
        return string.Concat(
            stem,
            "-",
            layer,
            network.IsBlocks ? GeoMapBlockRules.BlockFileSuffix : string.Empty,
            GeoMapBlockRules.FileExtension);
    }
}

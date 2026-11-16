using System.Globalization;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>A cor escolhida para a rede e a layer de onde ela veio.</summary>
public sealed record GeoMapNetworkColorChoice(string? Color, string? Layer);

/// <summary>
/// Cor da rede no contrato GeoMap 0.2 (<c>network.color</c>, <c>#RRGGBB</c>), a partir da cor das layers
/// das Polylines exportadas (M4.3b). Segue a mesma regra da ponte DXF: o ACI 7 (branco ou preto,
/// conforme o fundo) vira preto, porque o mapa base é claro.
/// </summary>
public static class GeoMapLayerColor
{
    public const short AciForegroundIndex = 7;
    public const string ForegroundColor = "#000000";

    public static string ToHex(byte red, byte green, byte blue) =>
        "#" +
        red.ToString("X2", CultureInfo.InvariantCulture) +
        green.ToString("X2", CultureInfo.InvariantCulture) +
        blue.ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Formato do contrato 0.2 como o GAE grava: <c>#RRGGBB</c> com dígitos em maiúsculas.</summary>
    public static bool IsValid(string? color) =>
        color is { Length: 7 } &&
        color[0] == '#' &&
        color.Skip(1).All(digit => digit is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>
    /// Uma rede tem uma só cor no mapa: vale a cor da layer com mais Polylines na seleção
    /// (empate: a primeira layer em ordem alfabética). Se essa layer não tem cor, a rede fica sem cor
    /// e o arquivo sai na versão 0.1.
    /// </summary>
    public static GeoMapNetworkColorChoice ChooseNetworkColor(
        IReadOnlyList<GeoMapExportFeature> features,
        IReadOnlyDictionary<string, string?> layerColors)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(layerColors);

        var layer = features
            .GroupBy(feature => feature.SourceLayer, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (layer is null || !layerColors.TryGetValue(layer, out var color) || !IsValid(color))
        {
            return new GeoMapNetworkColorChoice(null, layer);
        }

        return new GeoMapNetworkColorChoice(color, layer);
    }
}

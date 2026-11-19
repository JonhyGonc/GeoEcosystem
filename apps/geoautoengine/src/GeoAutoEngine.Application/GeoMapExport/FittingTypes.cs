using System.Globalization;
using System.Text;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>
/// Tipo da peça (tê, registro, cruzeta, ventosa…) a partir do nome do bloco (pedido do usuário, 19/11/2026).
/// Lista inicial com os nomes usuais; o usuário corrige. O nome vale inteiro ou seguido de número ou separador
/// (<c>TE</c>, <c>TE_100</c>, <c>RG-AF</c>), nunca como começo de outra palavra (<c>TERMINAL</c> não é tê).
/// </summary>
public static class FittingTypes
{
    // Mais longos primeiro, para CRUZETA ganhar de CRUZ.
    private static readonly (string Name, string Type)[] Names =
    [
        ("CRUZETA", "Cruzeta"), ("CRUZ", "Cruzeta"), ("CZ", "Cruzeta"),
        ("TEE", "Tê"), ("TE", "Tê"),
        ("REGISTRO", "Registro"), ("REG", "Registro"), ("RG", "Registro"),
        ("VENTOSA", "Ventosa"), ("VENT", "Ventosa"), ("VT", "Ventosa"),
        ("VALVULA", "Válvula"), ("VALV", "Válvula"),
        ("CURVA", "Curva"), ("CV", "Curva"),
        ("REDUCAO", "Redução"), ("RED", "Redução"),
        ("TAMPAO", "Cap"), ("PLUG", "Cap"), ("CAP", "Cap"),
        ("LUVA", "Luva"),
        ("HIDRANTE", "Hidrante"), ("HID", "Hidrante"),
        ("DESCARGA", "Descarga"),
        ("JUNCAO", "Junção"),
    ];

    /// <summary>Os nomes reconhecidos e o tipo de cada um, para mostrar e documentar.</summary>
    public static IReadOnlyList<(string Name, string Type)> KnownNames => Names;

    /// <summary>O tipo da peça; <c>null</c> quando o nome do bloco não está na lista.</summary>
    public static string? Resolve(string? blockName)
    {
        var name = Normalize(blockName);
        foreach (var (known, type) in Names.OrderByDescending(item => item.Name.Length))
        {
            if (name.StartsWith(known, StringComparison.Ordinal) &&
                (name.Length == known.Length || !char.IsLetter(name[known.Length])))
            {
                return type;
            }
        }

        return null;
    }

    // Maiúsculo e sem acento: TÊ → TE, REDUÇÃO → REDUCAO.
    private static string Normalize(string? text)
    {
        var decomposed = (text ?? string.Empty).Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}

/// <summary>O que está gravado numa peça: o tipo e a origem visíveis e o tipo que o plugin gravou por último.</summary>
/// <param name="Type">Campo TIPO da tabela (nulo quando a peça não tem registro).</param>
/// <param name="WrittenType">Cópia escondida do tipo que o plugin gravou (nula quando ele nunca gravou).</param>
public sealed record FittingStoredState(string? Type, string? Origin, string? WrittenType);

public enum FittingWriteAction
{
    /// <summary>Peça sem tipo gravado e com nome reconhecido: grava.</summary>
    Create,

    /// <summary>Tipo automático que mudou (ou RECALCULAR): regrava.</summary>
    Update,

    /// <summary>Nada mudou.</summary>
    Unchanged,

    /// <summary>Corrigida à mão e já marcada MANUAL: não mexe.</summary>
    KeepManual,

    /// <summary>Corrigida à mão sem marcar: só troca a origem para MANUAL.</summary>
    MarkManual,

    /// <summary>Nome de bloco fora da lista: nada é gravado; aparece no resumo para o usuário dizer o tipo.</summary>
    UnknownName,
}

/// <param name="Type">Tipo a gravar (ou o já gravado, nos casos sem gravação).</param>
public sealed record FittingWriteItem(string Handle, string BlockName, FittingWriteAction Action, string? Type);

public sealed record FittingWritePlan(IReadOnlyList<FittingWriteItem> Items)
{
    public int Count(FittingWriteAction action) => Items.Count(item => item.Action == action);

    public IReadOnlyList<FittingWriteItem> Writes => Items
        .Where(item => item.Action is FittingWriteAction.Create or FittingWriteAction.Update or FittingWriteAction.MarkManual)
        .ToArray();

    public IReadOnlyList<string> SummaryLines()
    {
        var brazil = CultureInfo.GetCultureInfo("pt-BR");
        var lines = new List<string>
        {
            "GEOAUTOENGINE — TIPO DAS PEÇAS",
            Line("Peças analisadas", Items.Count, brazil),
        };
        foreach (var group in Items
            .Where(item => item.Type is not null)
            .GroupBy(item => item.Type!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal))
        {
            lines.Add(Line("  " + group.Key, group.Count(), brazil));
        }

        var unknown = Items
            .Where(item => item.Action == FittingWriteAction.UnknownName)
            .GroupBy(item => item.BlockName, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
        {
            lines.Add(Line("Blocos com nome fora da lista (diga o tipo)", unknown.Sum(group => group.Count()), brazil));
            lines.AddRange(unknown.Take(20).Select(group => Line("  " + group.Key, group.Count(), brazil)));
            if (unknown.Length > 20)
            {
                lines.Add($"  … e mais {unknown.Length - 20} nome(s)");
            }
        }

        lines.Add("Gravação no desenho (tabela Object Data GEO_PECA):");
        lines.Add(Line("  novas", Count(FittingWriteAction.Create), brazil));
        lines.Add(Line("  atualizadas", Count(FittingWriteAction.Update), brazil));
        lines.Add(Line("  sem mudança", Count(FittingWriteAction.Unchanged), brazil));
        lines.Add(Line("  manuais mantidas", Count(FittingWriteAction.KeepManual) + Count(FittingWriteAction.MarkManual), brazil));
        return lines;
    }

    private static string Line(string label, int value, CultureInfo culture) =>
        $"{label + ":",-48}{value.ToString("N0", culture),10}";
}

/// <summary>
/// Decide o que gravar em cada peça sem perder correção feita à mão (mesma regra das profundidades):
/// ORIGEM = MANUAL, ou TIPO diferente do que o plugin gravou, ou TIPO que o plugin não gravou → manual;
/// ORIGEM = RECALCULAR → volta a ser automático.
/// </summary>
public static class FittingWritePlanner
{
    public static FittingWritePlan Plan(
        IEnumerable<(string Handle, string BlockName)> blocks,
        IReadOnlyDictionary<string, FittingStoredState> stored)
    {
        if (blocks is null)
        {
            throw new ArgumentNullException(nameof(blocks));
        }

        if (stored is null)
        {
            throw new ArgumentNullException(nameof(stored));
        }

        return new FittingWritePlan(blocks.Select(block =>
        {
            stored.TryGetValue(block.Handle, out var state);
            return Decide(block.Handle, block.BlockName, state);
        }).ToArray());
    }

    private static FittingWriteItem Decide(string handle, string blockName, FittingStoredState? state)
    {
        var origin = state?.Origin?.Trim().ToUpperInvariant() ?? string.Empty;
        if (state?.Type is { } storedType && IsManual(storedType, origin, state.WrittenType))
        {
            return new FittingWriteItem(
                handle,
                blockName,
                origin == PipeDepthOrigins.Manual ? FittingWriteAction.KeepManual : FittingWriteAction.MarkManual,
                storedType.Trim());
        }

        var computed = FittingTypes.Resolve(blockName);
        if (computed is null)
        {
            return new FittingWriteItem(handle, blockName, FittingWriteAction.UnknownName, null);
        }

        if (state?.Type is null)
        {
            return new FittingWriteItem(handle, blockName, FittingWriteAction.Create, computed);
        }

        return state.Type.Trim() == computed && origin == PipeDepthOrigins.Automatic && state.WrittenType == computed
            ? new FittingWriteItem(handle, blockName, FittingWriteAction.Unchanged, computed)
            : new FittingWriteItem(handle, blockName, FittingWriteAction.Update, computed);
    }

    private static bool IsManual(string storedType, string origin, string? writtenType) =>
        origin == PipeDepthOrigins.Manual ||
        (origin != PipeDepthOrigins.Recalculate &&
            (writtenType is null || !string.Equals(storedType.Trim(), writtenType, StringComparison.Ordinal)));
}

/// <summary>Coloca o tipo da peça nos blocos exportados para o GeoMap (<c>properties.tipoPeca</c>).</summary>
public static class FittingExport
{
    /// <summary>
    /// O tipo gravado no desenho (inclusive o corrigido à mão) vale primeiro; sem ele, o tipo pela lista de nomes.
    /// Bloco com nome fora da lista fica sem tipo.
    /// </summary>
    public static IReadOnlyList<GeoMapExportFeature> Apply(
        IReadOnlyList<GeoMapExportFeature> features,
        IReadOnlyDictionary<string, string> storedTypes)
    {
        if (features is null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        if (storedTypes is null)
        {
            throw new ArgumentNullException(nameof(storedTypes));
        }

        return features
            .Select(feature => feature.Block is { } block &&
                (storedTypes.TryGetValue(feature.CadHandle, out var stored) && !string.IsNullOrWhiteSpace(stored)
                    ? stored.Trim()
                    : FittingTypes.Resolve(block.Name)) is { } type
                ? feature with { Block = block with { FittingType = type } }
                : feature)
            .ToArray();
    }
}

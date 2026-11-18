namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Coloca nas linhas exportadas o resumo das profundidades.</summary>
public static class PipeDepthExport
{
    /// <summary>
    /// Associação feita na hora da exportação (provisória, nada gravado no desenho): cada linha com medição recebe
    /// o resumo. Blocos e linhas sem medição ficam como estão.
    /// </summary>
    public static IReadOnlyList<GeoMapExportFeature> Attach(
        IReadOnlyList<GeoMapExportFeature> features,
        PipeDepthAnalysis analysis)
    {
        if (features is null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        if (analysis is null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        return features
            .Select(feature => feature.Block is null &&
                analysis.ByHandle.TryGetValue(feature.CadHandle, out var result) &&
                PipeDepthSummary.From(result) is { } summary
                    ? feature with { Depths = ToDepths(summary, null) }
                    : feature)
            .ToArray();
    }

    /// <summary>
    /// Profundidades gravadas no desenho (tabela Object Data), inclusive as corrigidas à mão. Linhas sem medição
    /// (SEM_MEDICAO) ou com valores ilegíveis ficam sem profundidade.
    /// </summary>
    public static IReadOnlyList<GeoMapExportFeature> FromStored(
        IReadOnlyList<GeoMapExportFeature> features,
        IReadOnlyDictionary<string, PipeDepthFields> stored)
    {
        if (features is null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        if (stored is null)
        {
            throw new ArgumentNullException(nameof(stored));
        }

        return features
            .Select(feature => feature.Block is null &&
                stored.TryGetValue(feature.CadHandle, out var fields) &&
                fields.ToSummary() is { } summary
                    ? feature with { Depths = ToDepths(summary, OriginForMap(fields)) }
                    : feature)
            .ToArray();
    }

    private static string OriginForMap(PipeDepthFields fields) =>
        fields.NormalizedOrigin == PipeDepthOrigins.Manual ? PipeDepthOrigins.Manual : PipeDepthOrigins.Automatic;

    private static GeoMapPipeDepths ToDepths(PipeDepthSummary summary, string? origin) => new(
        summary.AverageMetres,
        summary.StartMetres,
        summary.EndMetres,
        summary.PointCount,
        origin);
}

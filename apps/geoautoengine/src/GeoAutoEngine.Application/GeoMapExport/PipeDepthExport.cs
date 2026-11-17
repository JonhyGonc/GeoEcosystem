namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Coloca nas linhas exportadas as profundidades associadas por <see cref="PipeDepthAssociator"/>.</summary>
public static class PipeDepthExport
{
    /// <summary>
    /// Cada linha analisada recebe as suas profundidades, inclusive nenhuma (para o mapa mostrar que a linha foi
    /// analisada e não tem medição). Blocos e linhas fora da análise ficam como estão.
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

        var unitsPerMetre = analysis.Options.DrawingUnitsPerMetre;
        return features
            .Select(feature => feature.Block is null && analysis.ByHandle.TryGetValue(feature.CadHandle, out var result)
                ? feature with { Depths = ToDepths(result, unitsPerMetre) }
                : feature)
            .ToArray();
    }

    private static GeoMapPipeDepths ToDepths(PipeDepthPipeResult result, double unitsPerMetre) => new(
        result.StartDepthMetres,
        result.EndDepthMetres,
        result.Measurements
            .Select(measurement => new GeoMapDepthMeasurement(
                measurement.PointName,
                measurement.DepthMetres,
                measurement.VertexIndex,
                Math.Round(measurement.Station / unitsPerMetre, 3),
                measurement.OriginalCode,
                measurement.Method,
                measurement.Shared))
            .ToArray());
}

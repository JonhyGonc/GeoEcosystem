using System.Globalization;
using System.Text;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Resumo para a linha de comando e diagnóstico em CSV (para conferir e corrigir em lote).</summary>
public static class PipeDepthReport
{
    private static readonly CultureInfo Brazil = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly string[] Header =
    [
        "tipo", "estado", "ponto", "handlePonto", "x", "y", "desc", "cota", "profundidadeM", "redePonto",
        "diametroPontoMm", "handleTubulacao", "layer", "distanciaM", "vertice", "observacao",
    ];

    public static IReadOnlyList<string> SummaryLines(PipeDepthAnalysis analysis)
    {
        if (analysis is null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        var conflicts = analysis.CountPoints(PipeDepthPointStatus.NetworkConflict) +
            analysis.CountPoints(PipeDepthPointStatus.DiameterConflict) +
            analysis.CountPoints(PipeDepthPointStatus.PointConflict);
        return
        [
            "GEOAUTOENGINE — ANÁLISE DE PROFUNDIDADES",
            Line("Tubulações analisadas", analysis.Pipes.Count),
            Line("Pontos _TG lidos", analysis.Points.Count),
            Line("Pontos com profundidade de tubulação", analysis.PipeDepthPoints),
            Line("  associados a uma linha", analysis.CountPoints(PipeDepthPointStatus.Associated)),
            Line("  na ponta de duas linhas (vale para as duas)", analysis.CountPoints(PipeDepthPointStatus.Shared)),
            Line("  repetidos (valem uma vez)", analysis.CountPoints(PipeDepthPointStatus.Duplicate)),
            Line("  ambíguos (várias linhas)", analysis.CountPoints(PipeDepthPointStatus.Ambiguous)),
            Line("  em conflito (rede, diâmetro ou ponto)", conflicts),
            Line("  perto de uma linha, fora da tolerância", analysis.CountPoints(PipeDepthPointStatus.NearNotAssociated)),
            Line("  sem linha", analysis.CountPoints(PipeDepthPointStatus.NoPipe)),
            Line("Tubulações com medições", analysis.PipesWithMeasurements),
            Line("Tubulações sem medições", analysis.Pipes.Count - analysis.PipesWithMeasurements),
            Line("Códigos desconhecidos", analysis.CountPoints(PipeDepthPointStatus.UnknownCode)),
            Line("Profundidades inválidas", analysis.CountPoints(PipeDepthPointStatus.InvalidDepth)),
            Line("Profundidades de caixa/PV (não usadas)", analysis.CountPoints(PipeDepthPointStatus.ObjectDepth)),
        ];
    }

    /// <summary>CSV com separador ";" e vírgula decimal, para abrir direto no Excel em português.</summary>
    public static string DiagnosticCsv(PipeDepthAnalysis analysis)
    {
        if (analysis is null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        var unitsPerMetre = analysis.Options.DrawingUnitsPerMetre;
        var builder = new StringBuilder();
        builder.Append(string.Join(";", Header)).Append("\r\n");
        foreach (var result in analysis.Points)
        {
            var point = result.Point;
            Row(
                builder,
                "PONTO",
                result.Status,
                point.Name,
                point.Handle,
                Number(point.Node.X),
                Number(point.Node.Y),
                point.Description,
                point.Elevation,
                Number(result.Reading.DepthMetres),
                result.Reading.Network ?? string.Empty,
                Number(result.Reading.DiameterMm),
                string.Join(" ", result.PipeHandles),
                string.Join(" ", result.PipeHandles
                    .Select(handle => analysis.ByHandle.TryGetValue(handle, out var pipe) ? pipe.Pipe.SourceLayer : string.Empty)
                    .Distinct(StringComparer.Ordinal)),
                Number(result.Distance / unitsPerMetre),
                result.VertexIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                result.Observation);
        }

        foreach (var pipe in analysis.Pipes)
        {
            var hasMeasurements = pipe.Measurements.Count > 0;
            var observation = hasMeasurements
                ? $"{pipe.Measurements.Count} medição(ões): " + string.Join(", ", pipe.Measurements.Select(measurement =>
                    $"{measurement.PointName}={Number(measurement.DepthMetres)}"))
                : "Nenhum ponto com profundidade sobre a linha.";
            Row(
                builder,
                "TUBULACAO",
                hasMeasurements ? "COM_MEDICAO" : "SEM_MEDICAO",
                string.Empty,
                string.Empty,
                Number(pipe.Pipe.Vertices[0].X),
                Number(pipe.Pipe.Vertices[0].Y),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                pipe.Pipe.CadHandle,
                pipe.Pipe.SourceLayer,
                string.Empty,
                string.Empty,
                observation);
        }

        return builder.ToString();
    }

    private static string Line(string label, int value) =>
        $"{label + ":",-48}{value.ToString("N0", Brazil),10}";

    private static string Number(double? value) =>
        value is { } number ? number.ToString("0.###", Brazil) : string.Empty;

    private static void Row(StringBuilder builder, params string[] values)
    {
        builder.Append(string.Join(";", values.Select(Escape))).Append("\r\n");
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([';', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}

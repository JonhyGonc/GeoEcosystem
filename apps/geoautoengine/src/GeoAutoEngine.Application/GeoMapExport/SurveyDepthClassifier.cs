using System.Text.RegularExpressions;
using GeoAutoEngine.Domain;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>
/// Lê a descrição (DESC) de um ponto do levantamento com o normalizador e o interpretador de códigos que o GAE já
/// usa nos CSVs (<see cref="SurveyCodeNormalizer"/>, <see cref="SurveyCodeInterpreter"/>), sem outro parser.
/// Só a "informação da rede" com profundidade (ex.: <c>AP1.20D300</c>, <c>E1.22D150</c>, <c>NI0.83</c>) vale como
/// profundidade da tubulação. A profundidade de caixas e PVs (ex.: <c>CXAP1.50</c>) é de outro objeto.
/// A cota (atributo COTA) nunca é lida como profundidade.
/// </summary>
public sealed class SurveyDepthClassifier
{
    private const string NetworkInformation = "Informação da rede";
    private const string UnknownRule = "DESCONHECIDO";

    private readonly SurveyCodeNormalizer _normalizer = new();
    private readonly SurveyCodeInterpreter _interpreter = new();

    public SurveyDepthReading Classify(string? description, double maximumDepthMetres)
    {
        var original = description?.Trim() ?? string.Empty;
        var normalized = _normalizer.Normalize(original);
        if (normalized.Length == 0)
        {
            return new SurveyDepthReading(SurveyDepthKind.NoCode, null, null, null, null, "VAZIO", normalized);
        }

        SurveyInterpretation interpretation;
        try
        {
            interpretation = _interpreter.Interpret(original, normalized);
        }
        catch (RegexMatchTimeoutException)
        {
            return new SurveyDepthReading(SurveyDepthKind.UnknownCode, null, null, null, null, UnknownRule, normalized);
        }

        var network = interpretation.Network;
        if (interpretation.Rule == UnknownRule || !interpretation.Recognized)
        {
            return new SurveyDepthReading(SurveyDepthKind.UnknownCode, null, null, null, null, interpretation.Rule, normalized);
        }

        if (interpretation.Depth is not { } depth)
        {
            return new SurveyDepthReading(
                SurveyDepthKind.NoDepth, null, network, interpretation.Diameter, interpretation.Material,
                interpretation.Rule, normalized);
        }

        if (interpretation.Nature != NetworkInformation)
        {
            return new SurveyDepthReading(
                SurveyDepthKind.ObjectDepth, depth, network, interpretation.Diameter, interpretation.Material,
                interpretation.Rule, normalized);
        }

        var kind = depth > 0 && depth <= maximumDepthMetres ? SurveyDepthKind.PipeDepth : SurveyDepthKind.InvalidDepth;
        return new SurveyDepthReading(
            kind, depth, network, interpretation.Diameter, interpretation.Material, interpretation.Rule, normalized);
    }
}

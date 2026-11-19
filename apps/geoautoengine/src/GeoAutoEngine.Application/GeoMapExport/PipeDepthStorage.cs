using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Resumo das profundidades de uma linha: média simples dos pontos, pontas e quantidade de pontos.</summary>
public sealed record PipeDepthSummary(double AverageMetres, double? StartMetres, double? EndMetres, int PointCount)
{
    private const double SameDepthMetres = 0.005;

    /// <summary>Resumo de uma linha analisada; <c>null</c> quando nenhum ponto foi associado a ela.</summary>
    public static PipeDepthSummary? From(PipeDepthPipeResult result)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        return result.Measurements.Count == 0
            ? null
            : new PipeDepthSummary(
                Math.Round(result.Measurements.Average(measurement => measurement.DepthMetres), 2, MidpointRounding.AwayFromZero),
                result.StartDepthMetres,
                result.EndDepthMetres,
                result.Measurements.Count);
    }

    public bool SameAs(PipeDepthSummary? other) =>
        other is not null &&
        Math.Abs(AverageMetres - other.AverageMetres) <= SameDepthMetres &&
        Same(StartMetres, other.StartMetres) &&
        Same(EndMetres, other.EndMetres) &&
        PointCount == other.PointCount;

    private static bool Same(double? left, double? right) =>
        left is null ? right is null : right is not null && Math.Abs(left.Value - right.Value) <= SameDepthMetres;
}

/// <summary>Valores do campo ORIGEM na tabela de profundidades do desenho.</summary>
public static class PipeDepthOrigins
{
    /// <summary>Calculada pelo plugin a partir dos pontos _TG.</summary>
    public const string Automatic = "AUTOMATICA";

    /// <summary>Corrigida à mão; o plugin não sobrescreve.</summary>
    public const string Manual = "MANUAL";

    /// <summary>A linha tinha profundidade automática e deixou de ter pontos associados.</summary>
    public const string WithoutMeasurement = "SEM_MEDICAO";

    /// <summary>Escrito pelo usuário para o plugin voltar a calcular uma linha que estava manual.</summary>
    public const string Recalculate = "RECALCULAR";
}

/// <summary>
/// Os campos de profundidade como estão no desenho (tabela Object Data), onde o usuário pode editar.
/// Início, fim e pontos são texto: o usuário pode deixar em branco ou digitar com vírgula.
/// </summary>
public sealed record PipeDepthFields(double AverageMetres, string Start, string End, string Points, string Origin)
{
    public static PipeDepthFields Automatic(PipeDepthSummary summary)
    {
        if (summary is null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        return new PipeDepthFields(
            summary.AverageMetres,
            Format(summary.StartMetres),
            Format(summary.EndMetres),
            summary.PointCount.ToString(CultureInfo.InvariantCulture),
            PipeDepthOrigins.Automatic);
    }

    public static PipeDepthFields WithoutMeasurement() =>
        new(0, string.Empty, string.Empty, "0", PipeDepthOrigins.WithoutMeasurement);

    public string NormalizedOrigin => Origin.Trim().ToUpperInvariant();

    /// <summary>O resumo que esses campos representam; <c>null</c> sem medição ou com valores ilegíveis.</summary>
    public PipeDepthSummary? ToSummary()
    {
        if (NormalizedOrigin == PipeDepthOrigins.WithoutMeasurement ||
            !int.TryParse(Points.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var points) ||
            points <= 0 ||
            !(AverageMetres > 0) ||
            !TryParse(Start, out var start) ||
            !TryParse(End, out var end))
        {
            return null;
        }

        return new PipeDepthSummary(AverageMetres, start, end, points);
    }

    /// <summary>Os valores (sem a origem) são os mesmos de outro conjunto de campos.</summary>
    public bool SameValues(PipeDepthFields other)
    {
        if (other is null)
        {
            return false;
        }

        return Math.Abs(AverageMetres - other.AverageMetres) <= 0.005 &&
            TryParse(Start, out var start) && TryParse(other.Start, out var otherStart) && Same(start, otherStart) &&
            TryParse(End, out var end) && TryParse(other.End, out var otherEnd) && Same(end, otherEnd) &&
            string.Equals(Points.Trim(), other.Points.Trim(), StringComparison.Ordinal);
    }

    public static string Format(double? value) =>
        value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>Lê um número digitado com ponto ou vírgula; texto vazio é "sem valor".</summary>
    public static bool TryParse(string? text, out double? value)
    {
        value = null;
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (double.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
            !double.IsNaN(number) && !double.IsInfinity(number))
        {
            value = number;
            return true;
        }

        return false;
    }

    private static bool Same(double? left, double? right) =>
        left is null ? right is null : right is not null && Math.Abs(left.Value - right.Value) <= 0.005;
}

/// <summary>
/// Cópia escondida, na própria linha, do que o plugin gravou por último: o resumo e as medições de cada ponto.
/// Serve para saber se o usuário mudou os campos à mão e para guardar o detalhe de cada ponto no DWG.
/// </summary>
public sealed record PipeDepthSnapshot(PipeDepthSummary? Summary, IReadOnlyList<PipeDepthSnapshotMeasurement> Measurements)
{
    public static PipeDepthSnapshot From(PipeDepthPipeResult result, double drawingUnitsPerMetre)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        return new PipeDepthSnapshot(
            PipeDepthSummary.From(result),
            result.Measurements
                .Select(measurement => new PipeDepthSnapshotMeasurement(
                    measurement.PointHandle,
                    measurement.PointName,
                    measurement.DepthMetres,
                    measurement.VertexIndex,
                    Math.Round(measurement.Station / drawingUnitsPerMetre, 3),
                    measurement.OriginalCode,
                    measurement.Method,
                    measurement.Shared))
                .ToArray());
    }

    /// <summary>Os campos que o plugin escreveu junto com esta cópia.</summary>
    public PipeDepthFields ExpectedFields() =>
        Summary is null ? PipeDepthFields.WithoutMeasurement() : PipeDepthFields.Automatic(Summary);

    public bool SameAs(PipeDepthSnapshot? other) =>
        other is not null &&
        (Summary is null ? other.Summary is null : Summary.SameAs(other.Summary)) &&
        Measurements.SequenceEqual(other.Measurements);
}

public sealed record PipeDepthSnapshotMeasurement(
    [property: JsonPropertyName("handle")] string PointHandle,
    [property: JsonPropertyName("ponto")] string PointName,
    [property: JsonPropertyName("valorM")] double DepthMetres,
    [property: JsonPropertyName("vertice")] int? VertexIndex,
    [property: JsonPropertyName("distanciaInicioM")] double StationMetres,
    [property: JsonPropertyName("codigo")] string Code,
    [property: JsonPropertyName("metodo")] string Method,
    [property: JsonPropertyName("compartilhada")] bool Shared);

/// <summary>A cópia escondida em texto (JSON só com ASCII), cortada em pedaços que cabem num XRecord.</summary>
public static class PipeDepthSnapshotCodec
{
    public const int ChunkLength = 1800;
    private const int Version = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IReadOnlyList<string> Encode(PipeDepthSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var dto = new SnapshotDto(
            Version,
            snapshot.Summary?.AverageMetres,
            snapshot.Summary?.StartMetres,
            snapshot.Summary?.EndMetres,
            snapshot.Summary?.PointCount ?? 0,
            snapshot.Measurements);
        var text = JsonSerializer.Serialize(dto, Options);
        var chunks = new List<string>();
        for (var start = 0; start < text.Length; start += ChunkLength)
        {
            chunks.Add(text.Substring(start, Math.Min(ChunkLength, text.Length - start)));
        }

        return chunks;
    }

    /// <summary><c>null</c> quando o texto não é uma cópia válida (de outra versão ou corrompida).</summary>
    public static PipeDepthSnapshot? Decode(IEnumerable<string> chunks)
    {
        if (chunks is null)
        {
            return null;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<SnapshotDto>(string.Concat(chunks), Options);
            if (dto is null || dto.Version != Version || dto.Measurements is null)
            {
                return null;
            }

            var summary = dto.Average is { } average && dto.Points > 0
                ? new PipeDepthSummary(average, dto.Start, dto.End, dto.Points)
                : null;
            return new PipeDepthSnapshot(summary, dto.Measurements);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record SnapshotDto(
        [property: JsonPropertyName("versao")] int Version,
        [property: JsonPropertyName("mediaM")] double? Average,
        [property: JsonPropertyName("inicialM")] double? Start,
        [property: JsonPropertyName("finalM")] double? End,
        [property: JsonPropertyName("pontos")] int Points,
        [property: JsonPropertyName("medicoes")] IReadOnlyList<PipeDepthSnapshotMeasurement>? Measurements);
}

/// <summary>O que está gravado numa linha: os campos visíveis e a cópia escondida (cada um pode faltar).</summary>
public sealed record PipeDepthStoredState(PipeDepthFields? Fields, PipeDepthSnapshot? Snapshot);

public enum PipeDepthWriteAction
{
    /// <summary>Linha sem profundidade gravada e com medição agora: grava.</summary>
    Create,

    /// <summary>Profundidade automática que mudou: regrava.</summary>
    Update,

    /// <summary>Nada mudou.</summary>
    Unchanged,

    /// <summary>Corrigida à mão e já marcada MANUAL: não mexe.</summary>
    KeepManual,

    /// <summary>Corrigida à mão sem marcar: só troca a origem para MANUAL, sem mexer nos valores.</summary>
    MarkManual,

    /// <summary>Era automática e perdeu as medições: zera e marca SEM_MEDICAO, em vez de apagar.</summary>
    MarkWithoutMeasurement,

    /// <summary>Sem profundidade gravada e sem medição: nada a gravar.</summary>
    Nothing,
}

/// <param name="Fields">Campos a gravar (nulo quando não há gravação).</param>
/// <param name="Snapshot">Cópia escondida a gravar (nula quando ela não muda).</param>
public sealed record PipeDepthWriteItem(
    string Handle,
    PipeDepthWriteAction Action,
    PipeDepthFields? Fields,
    PipeDepthSnapshot? Snapshot);

public sealed record PipeDepthWritePlan(IReadOnlyList<PipeDepthWriteItem> Items)
{
    public int Count(PipeDepthWriteAction action) => Items.Count(item => item.Action == action);

    public IReadOnlyList<PipeDepthWriteItem> Writes => Items
        .Where(item => item.Action is PipeDepthWriteAction.Create or PipeDepthWriteAction.Update
            or PipeDepthWriteAction.MarkManual or PipeDepthWriteAction.MarkWithoutMeasurement)
        .ToArray();

    public IReadOnlyList<string> SummaryLines() =>
    [
        "Gravação no desenho (tabela Object Data GEO_PROFUNDIDADE):",
        Line("  novas", Count(PipeDepthWriteAction.Create)),
        Line("  atualizadas", Count(PipeDepthWriteAction.Update)),
        Line("  sem mudança", Count(PipeDepthWriteAction.Unchanged)),
        Line("  manuais mantidas", Count(PipeDepthWriteAction.KeepManual) + Count(PipeDepthWriteAction.MarkManual)),
        Line("  perderam as medições (ficam SEM_MEDICAO)", Count(PipeDepthWriteAction.MarkWithoutMeasurement)),
    ];

    private static string Line(string label, int value) =>
        $"{label + ":",-48}{value.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")),10}";
}

/// <summary>
/// Decide o que gravar em cada linha analisada sem nunca perder uma correção feita à mão:
/// <list type="bullet">
/// <item>ORIGEM = MANUAL, ou valores diferentes do que o plugin gravou, ou campos que o plugin não gravou → manual, não mexe;</item>
/// <item>ORIGEM = RECALCULAR → volta a ser automática;</item>
/// <item>linha automática que perdeu as medições → zerada e marcada SEM_MEDICAO (não é apagada).</item>
/// </list>
/// </summary>
public static class PipeDepthWritePlanner
{
    public static PipeDepthWritePlan Plan(
        PipeDepthAnalysis analysis,
        IReadOnlyDictionary<string, PipeDepthStoredState> stored)
    {
        if (analysis is null)
        {
            throw new ArgumentNullException(nameof(analysis));
        }

        if (stored is null)
        {
            throw new ArgumentNullException(nameof(stored));
        }

        var items = new List<PipeDepthWriteItem>(analysis.Pipes.Count);
        foreach (var pipe in analysis.Pipes)
        {
            var handle = pipe.Pipe.CadHandle;
            stored.TryGetValue(handle, out var state);
            items.Add(Decide(handle, PipeDepthSnapshot.From(pipe, analysis.Options.DrawingUnitsPerMetre), state));
        }

        return new PipeDepthWritePlan(items);
    }

    private static PipeDepthWriteItem Decide(string handle, PipeDepthSnapshot computed, PipeDepthStoredState? state)
    {
        var fields = state?.Fields;
        if (fields is not null && IsManual(fields, state!.Snapshot))
        {
            return fields.NormalizedOrigin == PipeDepthOrigins.Manual
                ? new PipeDepthWriteItem(handle, PipeDepthWriteAction.KeepManual, null, null)
                : new PipeDepthWriteItem(handle, PipeDepthWriteAction.MarkManual, fields with { Origin = PipeDepthOrigins.Manual }, null);
        }

        if (computed.Summary is null)
        {
            if (fields is null)
            {
                return new PipeDepthWriteItem(handle, PipeDepthWriteAction.Nothing, null, null);
            }

            return fields.NormalizedOrigin == PipeDepthOrigins.WithoutMeasurement && computed.SameAs(state!.Snapshot)
                ? new PipeDepthWriteItem(handle, PipeDepthWriteAction.Unchanged, null, null)
                : new PipeDepthWriteItem(handle, PipeDepthWriteAction.MarkWithoutMeasurement, PipeDepthFields.WithoutMeasurement(), computed);
        }

        var expected = PipeDepthFields.Automatic(computed.Summary);
        if (fields is null)
        {
            return new PipeDepthWriteItem(handle, PipeDepthWriteAction.Create, expected, computed);
        }

        return fields.NormalizedOrigin == PipeDepthOrigins.Automatic && computed.SameAs(state!.Snapshot)
            ? new PipeDepthWriteItem(handle, PipeDepthWriteAction.Unchanged, null, null)
            : new PipeDepthWriteItem(handle, PipeDepthWriteAction.Update, expected, computed);
    }

    private static bool IsManual(PipeDepthFields fields, PipeDepthSnapshot? snapshot)
    {
        var origin = fields.NormalizedOrigin;
        if (origin == PipeDepthOrigins.Manual)
        {
            return true;
        }

        if (origin == PipeDepthOrigins.Recalculate)
        {
            return false;
        }

        // Campos que o plugin não gravou (sem cópia escondida) ou diferentes do que ele gravou: foram digitados.
        return snapshot is null || !fields.SameValues(snapshot.ExpectedFields());
    }
}

using System.Text.Json.Serialization;

namespace GeoEcosystem.Contracts.Inspection;

/// <summary>Payload do <c>CorrectionPush</c> (tipo 6, GAE → GAD). SPEC §9.1.</summary>
public sealed class InspectionCorrectionPush
{
    /// <summary>Limite do pacote aceito pelo consumidor (SPEC §9.1).</summary>
    public const int MaxCorrections = 1000;

    /// <summary>Tamanho máximo de <see cref="InspectionOccurrenceCorrection.Description"/> (SPEC §9.1).</summary>
    public const int MaxDescriptionLength = 4000;

    public Guid ReportId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public IReadOnlyList<InspectionOccurrenceCorrection> Corrections { get; set; } = [];
}

/// <summary>
/// Um item de <c>corrections[]</c>: o <b>estado completo</b> de uma ocorrência editada (SPEC §9.2).
/// O produtor escreve todos os campos, inclusive nulos e listas vazias. Para o consumidor, as
/// propriedades <c>Has*</c> dizem se o campo veio no JSON. Campo ausente = "não alterar" (tolerância).
/// </summary>
public sealed class InspectionOccurrenceCorrection
{
    private decimal? _meterage;
    private decimal? _endMeterage;
    private IReadOnlyList<decimal> _meteragePoints = [];
    private string? _description;

    public Guid SectionId { get; set; }
    public Guid OccurrenceId { get; set; }
    public string LocationMode { get; set; } = string.Empty;

    public decimal? Meterage
    {
        get => _meterage;
        set
        {
            _meterage = value;
            HasMeterage = true;
        }
    }

    public decimal? EndMeterage
    {
        get => _endMeterage;
        set
        {
            _endMeterage = value;
            HasEndMeterage = true;
        }
    }

    public IReadOnlyList<decimal> MeteragePoints
    {
        get => _meteragePoints;
        set
        {
            _meteragePoints = value ?? [];
            HasMeteragePoints = true;
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            _description = value;
            HasDescription = true;
        }
    }

    [JsonIgnore]
    public bool HasMeterage { get; private set; }

    [JsonIgnore]
    public bool HasEndMeterage { get; private set; }

    [JsonIgnore]
    public bool HasMeteragePoints { get; private set; }

    [JsonIgnore]
    public bool HasDescription { get; private set; }
}

/// <summary>Payload do <c>CorrectionAck</c> (tipo 7, GAD → GAE). SPEC §9.4.</summary>
public sealed class InspectionCorrectionAcknowledgement
{
    private int _appliedCount;

    public bool Accepted { get; set; }

    /// <summary><c>Success</c>, <c>Partial</c> ou <c>Failure</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
    public int RequestedCount { get; set; }

    /// <summary>O maior valor entre o informado e os itens confirmados (<c>Applied</c> ou <c>Unchanged</c>).</summary>
    public int AppliedCount
    {
        get
        {
            var confirmedByResults = Results.Count(InspectionCorrectionItemStatus.IsConfirmed);
            return Math.Max(_appliedCount, confirmedByResults);
        }
        set => _appliedCount = value;
    }

    /// <summary>Calculado: <c>requestedCount − appliedCount</c>, nunca negativo (SPEC §9.4).</summary>
    public int RejectedCount => Math.Max(0, RequestedCount - AppliedCount);

    public IReadOnlyList<InspectionCorrectionItemAcknowledgement> Results { get; set; } = [];
}

public sealed class InspectionCorrectionItemAcknowledgement
{
    public Guid SectionId { get; set; }
    public Guid OccurrenceId { get; set; }
    public bool Applied { get; set; }

    /// <summary>Um dos valores de <see cref="InspectionCorrectionItemStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

/// <summary>Valores de <c>results[].status</c> (SPEC §9.4).</summary>
public static class InspectionCorrectionItemStatus
{
    public const string Applied = "Applied";
    public const string Unchanged = "Unchanged";
    public const string NotFound = "NotFound";
    public const string Invalid = "Invalid";
    public const string Cancelled = "Cancelled";
    public const string SaveFailed = "SaveFailed";
    public const string NotApplied = "NotApplied";
    public const string Failure = "Failure";

    /// <summary>Confirmado = aplicado ou já atualizado (SPEC §9.4).</summary>
    public static bool IsConfirmed(InspectionCorrectionItemAcknowledgement result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Applied ||
               string.Equals(result.Status, Applied, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(result.Status, Unchanged, StringComparison.OrdinalIgnoreCase);
    }
}

using System.Text.Json.Serialization;

namespace GeoAutoDocs.Core;

// Os payloads das mensagens 6/7 (InspectionCorrectionPush, InspectionOccurrenceCorrection,
// InspectionCorrectionAcknowledgement) ficam na biblioteca compartilhada do contrato
// libs/GeoEcosystem.Contracts.Inspection (DEC-ECO-0002, M3.3b).

public interface IInspectionCorrectionChannel
{
    void SetCorrectionHandler(
        Func<InspectionCorrectionPush, CancellationToken, Task<InspectionCorrectionAcknowledgement>> handler);

    Task StopCorrectionSessionAsync();
}

public enum InspectionCorrectionResolutionStatus
{
    Ready,
    Unchanged,
    Invalid,
    NotFound
}

public sealed class InspectionCorrectionResolution
{
    public required InspectionOccurrenceCorrection Correction { get; init; }
    public InspectionCorrectionResolutionStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string SectionName { get; init; } = string.Empty;
    public OccurrenceLocationMode LocationMode { get; init; }
    public bool HasPositionChange { get; init; }
    public decimal? CurrentMeterage { get; init; }
    public decimal? CurrentEndMeterage { get; init; }
    public IReadOnlyList<decimal> CurrentMeteragePoints { get; init; } = [];
    public string CurrentDescription { get; init; } = string.Empty;
    public decimal? ProposedMeterage { get; init; }
    public decimal? ProposedEndMeterage { get; init; }
    public IReadOnlyList<decimal> ProposedMeteragePoints { get; init; } = [];
    public string ProposedDescription { get; init; } = string.Empty;
    public string ProposedGeneratedDescription { get; init; } = string.Empty;
    public string? ProposedManualDescriptionOverride { get; init; }

    [JsonIgnore]
    public Occurrence? Target { get; init; }

    [JsonIgnore]
    public bool CanApply => Status == InspectionCorrectionResolutionStatus.Ready && Target is not null;
}

public sealed class InspectionCorrectionPlan
{
    public Guid ReportId { get; init; }
    public Report? Report { get; init; }
    public List<InspectionCorrectionResolution> Items { get; init; } = [];
    public int ApplicableCount => Items.Count(x => x.CanApply);
    public int InvalidCount => Items.Count(x =>
        x.Status is InspectionCorrectionResolutionStatus.Invalid or InspectionCorrectionResolutionStatus.NotFound);
}

public sealed record InspectionCorrectionSnapshot(
    Occurrence Occurrence,
    decimal? Meterage,
    decimal? EndMeterage,
    IReadOnlyList<decimal> MeteragePoints,
    string GeneratedDescription,
    string? ManualDescriptionOverride);

public sealed class InspectionCorrectionService
{
    public const int MaxCorrectionsPerPackage = 1000;
    public const int MaxDescriptionLength = 4000;

    public InspectionCorrectionPlan Prepare(Project project, InspectionCorrectionPush package)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(package);

        var corrections = package.Corrections ?? [];
        if (corrections.Count > MaxCorrectionsPerPackage)
            throw new InvalidDataException(
                $"O pacote possui {corrections.Count} correções; o limite é {MaxCorrectionsPerPackage}.");

        var report = project.Reports.SingleOrDefault(x => x.Id == package.ReportId);
        var duplicateKeys = corrections
            .GroupBy(x => (x.SectionId, x.OccurrenceId))
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet();

        var items = corrections.Select(correction =>
            Resolve(project, report, correction, duplicateKeys.Contains((correction.SectionId, correction.OccurrenceId))))
            .ToList();

        return new InspectionCorrectionPlan
        {
            ReportId = package.ReportId,
            Report = report,
            Items = items
        };
    }

    public IReadOnlyList<InspectionCorrectionSnapshot> Apply(InspectionCorrectionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var snapshots = new List<InspectionCorrectionSnapshot>();

        foreach (var item in plan.Items.Where(x => x.CanApply))
        {
            var occurrence = item.Target!;
            var orderedPoints = OrderedPoints(occurrence);
            snapshots.Add(new InspectionCorrectionSnapshot(
                occurrence,
                occurrence.Meterage,
                occurrence.EndMeterage,
                orderedPoints.Select(point => point.Meterage).ToArray(),
                occurrence.GeneratedDescription,
                occurrence.ManualDescriptionOverride));

            switch (occurrence.LocationMode)
            {
                case OccurrenceLocationMode.Point:
                case OccurrenceLocationMode.From:
                    if (item.Correction.HasMeterage)
                        occurrence.Meterage = item.ProposedMeterage;
                    break;
                case OccurrenceLocationMode.Range:
                    if (item.Correction.HasMeterage)
                        occurrence.Meterage = item.ProposedMeterage;
                    if (item.Correction.HasEndMeterage)
                        occurrence.EndMeterage = item.ProposedEndMeterage;
                    break;
                case OccurrenceLocationMode.MultiPoint:
                    if (item.Correction.HasMeteragePoints)
                    {
                        for (var index = 0; index < orderedPoints.Count; index++)
                            orderedPoints[index].Meterage = item.ProposedMeteragePoints[index];
                    }
                    break;
            }

            occurrence.GeneratedDescription = item.ProposedGeneratedDescription;
            occurrence.ManualDescriptionOverride = item.ProposedManualDescriptionOverride;
        }

        return snapshots;
    }

    public void Rollback(IEnumerable<InspectionCorrectionSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        foreach (var snapshot in snapshots)
        {
            snapshot.Occurrence.Meterage = snapshot.Meterage;
            snapshot.Occurrence.EndMeterage = snapshot.EndMeterage;
            var orderedPoints = OrderedPoints(snapshot.Occurrence);
            for (var index = 0; index < orderedPoints.Count && index < snapshot.MeteragePoints.Count; index++)
                orderedPoints[index].Meterage = snapshot.MeteragePoints[index];
            snapshot.Occurrence.GeneratedDescription = snapshot.GeneratedDescription;
            snapshot.Occurrence.ManualDescriptionOverride = snapshot.ManualDescriptionOverride;
        }
    }

    private static InspectionCorrectionResolution Resolve(Project project, Report? report,
        InspectionOccurrenceCorrection correction, bool duplicated)
    {
        if (correction.SectionId == Guid.Empty || correction.OccurrenceId == Guid.Empty)
            return Failure(correction, InspectionCorrectionResolutionStatus.Invalid,
                "SectionId e OccurrenceId são obrigatórios.");
        if (report is null)
            return Failure(correction, InspectionCorrectionResolutionStatus.NotFound,
                "O relatório informado não pertence ao projeto aberto.");
        if (duplicated)
            return Failure(correction, InspectionCorrectionResolutionStatus.Invalid,
                "A ocorrência aparece mais de uma vez no mesmo pacote.");
        if (correction.HasDescription && correction.Description?.Length > MaxDescriptionLength)
            return Failure(correction, InspectionCorrectionResolutionStatus.Invalid,
                $"O texto excede {MaxDescriptionLength} caracteres.");

        var sectionEntry = project.Locations
            .SelectMany(location => location.Sections.Select(section => new { Location = location, Section = section }))
            .SingleOrDefault(x => x.Section.Id == correction.SectionId);
        if (sectionEntry is null)
            return Failure(correction, InspectionCorrectionResolutionStatus.NotFound,
                "O trecho não foi encontrado no projeto aberto.");
        if (!report.SectionLinks.Any(x => x.SectionId == correction.SectionId))
            return Failure(correction, InspectionCorrectionResolutionStatus.NotFound,
                "O trecho não pertence ao relatório informado.",
                sectionEntry.Location.Name, sectionEntry.Section.Name);

        var occurrence = sectionEntry.Section.Occurrences
            .SingleOrDefault(x => x.Id == correction.OccurrenceId);
        if (occurrence is null)
            return Failure(correction, InspectionCorrectionResolutionStatus.NotFound,
                "A ocorrência não foi encontrada no trecho informado.",
                sectionEntry.Location.Name, sectionEntry.Section.Name);

        if (!TryResolveLocationMode(correction, occurrence, out var locationMode, out var modeMessage))
            return Failure(correction, InspectionCorrectionResolutionStatus.Invalid, modeMessage,
                sectionEntry.Location.Name, sectionEntry.Section.Name, occurrence.LocationMode);

        var hasPositionChange = HasPositionPayload(correction, locationMode);
        if (!hasPositionChange && !correction.HasDescription)
            return Failure(correction, InspectionCorrectionResolutionStatus.Invalid,
                "Nenhum campo corrigível foi enviado.", sectionEntry.Location.Name,
                sectionEntry.Section.Name, locationMode);

        if (!ValidatePositionPayload(correction, occurrence, locationMode, out var validationMessage))
            return Failure(correction, InspectionCorrectionResolutionStatus.Invalid, validationMessage,
                sectionEntry.Location.Name, sectionEntry.Section.Name, locationMode);

        var currentPoints = OrderedPoints(occurrence).Select(point => point.Meterage).ToArray();
        var proposedMeterage = occurrence.Meterage;
        var proposedEndMeterage = occurrence.EndMeterage;
        IReadOnlyList<decimal> proposedPoints = currentPoints;

        switch (locationMode)
        {
            case OccurrenceLocationMode.Point:
            case OccurrenceLocationMode.From:
                if (correction.HasMeterage)
                    proposedMeterage = correction.Meterage;
                break;
            case OccurrenceLocationMode.Range:
                if (correction.HasMeterage)
                    proposedMeterage = correction.Meterage;
                if (correction.HasEndMeterage)
                    proposedEndMeterage = correction.EndMeterage;
                break;
            case OccurrenceLocationMode.MultiPoint:
                if (correction.HasMeteragePoints)
                    proposedPoints = correction.MeteragePoints.ToArray();
                break;
        }

        var positionChanged = occurrence.Meterage != proposedMeterage ||
                              occurrence.EndMeterage != proposedEndMeterage ||
                              !currentPoints.SequenceEqual(proposedPoints);
        var proposedGenerated = positionChanged && occurrence.Anomalies.Count > 0
            ? GenerateDescriptionAt(occurrence, proposedMeterage, proposedEndMeterage, proposedPoints)
            : occurrence.GeneratedDescription;

        // O Engine envia a descrição atual mesmo quando só a metragem foi editada.
        // Nesse caso, não transformá-la em texto manual que congelaria a metragem antiga.
        var editedText = correction.HasDescription &&
            !string.Equals(correction.Description?.Trim(), occurrence.EffectiveDescription.Trim(),
                StringComparison.Ordinal);
        var proposedOverride = editedText
            ? NormalizeDescription(correction.Description, proposedGenerated)
            : NormalizeDescription(occurrence.ManualDescriptionOverride, occurrence.GeneratedDescription);
        var proposedDescription = string.IsNullOrWhiteSpace(proposedOverride)
            ? proposedGenerated : proposedOverride;
        var changed = positionChanged ||
                      !string.Equals(occurrence.GeneratedDescription, proposedGenerated, StringComparison.Ordinal) ||
                      !string.Equals(occurrence.ManualDescriptionOverride, proposedOverride, StringComparison.Ordinal);

        return new InspectionCorrectionResolution
        {
            Correction = correction,
            Status = changed ? InspectionCorrectionResolutionStatus.Ready :
                InspectionCorrectionResolutionStatus.Unchanged,
            Message = changed ? "Pronta para aplicar." : "Os valores já estão atualizados.",
            LocationName = sectionEntry.Location.Name,
            SectionName = BuildSectionName(sectionEntry.Section),
            LocationMode = locationMode,
            HasPositionChange = hasPositionChange,
            CurrentMeterage = occurrence.Meterage,
            CurrentEndMeterage = occurrence.EndMeterage,
            CurrentMeteragePoints = currentPoints,
            CurrentDescription = occurrence.EffectiveDescription,
            ProposedMeterage = proposedMeterage,
            ProposedEndMeterage = proposedEndMeterage,
            ProposedMeteragePoints = proposedPoints,
            ProposedDescription = proposedDescription,
            ProposedGeneratedDescription = proposedGenerated,
            ProposedManualDescriptionOverride = proposedOverride,
            Target = occurrence
        };
    }

    private static bool TryResolveLocationMode(InspectionOccurrenceCorrection correction, Occurrence occurrence,
        out OccurrenceLocationMode locationMode, out string message)
    {
        locationMode = occurrence.LocationMode;
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(correction.LocationMode)) return true;
        if (!Enum.TryParse<OccurrenceLocationMode>(correction.LocationMode, true, out var receivedMode))
        {
            message = $"LocationMode desconhecido: '{correction.LocationMode}'.";
            return false;
        }

        if (receivedMode != occurrence.LocationMode)
        {
            message = $"LocationMode recebido ({receivedMode}) não corresponde ao cadastro ({occurrence.LocationMode}).";
            return false;
        }

        locationMode = receivedMode;
        return true;
    }

    private static bool HasPositionPayload(InspectionOccurrenceCorrection correction, OccurrenceLocationMode mode) => mode switch
    {
        OccurrenceLocationMode.Point or OccurrenceLocationMode.From => correction.HasMeterage,
        OccurrenceLocationMode.Range => correction.HasMeterage || correction.HasEndMeterage,
        OccurrenceLocationMode.MultiPoint => correction.HasMeteragePoints,
        _ => false
    };

    private static bool ValidatePositionPayload(InspectionOccurrenceCorrection correction, Occurrence occurrence,
        OccurrenceLocationMode mode, out string message)
    {
        message = string.Empty;
        switch (mode)
        {
            case OccurrenceLocationMode.Point:
            case OccurrenceLocationMode.From:
                if (correction.HasMeterage && !ValidRequiredMeterage(correction.Meterage, out message))
                    return false;
                if (correction.HasEndMeterage && correction.EndMeterage.HasValue)
                {
                    message = "Metragem final não é válida para este tipo de ocorrência.";
                    return false;
                }
                if (correction.HasMeteragePoints && correction.MeteragePoints.Count > 0)
                {
                    message = "Pontos múltiplos não são válidos para este tipo de ocorrência.";
                    return false;
                }
                return true;

            case OccurrenceLocationMode.Range:
                if (correction.HasMeterage || correction.HasEndMeterage)
                {
                    if (!correction.HasMeterage || !correction.HasEndMeterage ||
                        !ValidRequiredMeterage(correction.Meterage, out message) ||
                        !ValidRequiredMeterage(correction.EndMeterage, out message))
                    {
                        if (string.IsNullOrWhiteSpace(message))
                            message = "Intervalos devem informar metragem inicial e final.";
                        return false;
                    }
                    if (correction.EndMeterage < correction.Meterage)
                    {
                        message = "A metragem final não pode ser menor que a metragem inicial.";
                        return false;
                    }
                }
                if (correction.HasMeteragePoints && correction.MeteragePoints.Count > 0)
                {
                    message = "Pontos múltiplos não são válidos para uma ocorrência de intervalo.";
                    return false;
                }
                return true;

            case OccurrenceLocationMode.MultiPoint:
                if (correction.HasMeteragePoints)
                {
                    var currentCount = occurrence.MeteragePoints.Count;
                    if (correction.MeteragePoints.Count != currentCount)
                    {
                        message = $"A ocorrência multiponto deve manter {currentCount} ponto(s).";
                        return false;
                    }
                    if (correction.MeteragePoints.Any(value => value < 0))
                    {
                        message = "As metragens dos pontos não podem ser negativas.";
                        return false;
                    }
                }
                if (correction.HasMeterage && correction.Meterage.HasValue ||
                    correction.HasEndMeterage && correction.EndMeterage.HasValue)
                {
                    message = "Metragem simples ou final não é válida para uma ocorrência multiponto.";
                    return false;
                }
                return true;

            case OccurrenceLocationMode.EntireSection:
            case OccurrenceLocationMode.NotApplicable:
                if (correction.HasMeterage && correction.Meterage.HasValue ||
                    correction.HasEndMeterage && correction.EndMeterage.HasValue ||
                    correction.HasMeteragePoints && correction.MeteragePoints.Count > 0)
                {
                    message = "Este tipo de ocorrência não aceita alteração de metragem.";
                    return false;
                }
                return true;

            default:
                message = "Tipo de posicionamento não suportado para correção.";
                return false;
        }
    }

    private static bool ValidRequiredMeterage(decimal? value, out string message)
    {
        if (!value.HasValue)
        {
            message = "A metragem obrigatória não foi informada.";
            return false;
        }
        if (value.Value < 0)
        {
            message = "A metragem não pode ser negativa.";
            return false;
        }
        message = string.Empty;
        return true;
    }

    private static List<OccurrenceMeteragePoint> OrderedPoints(Occurrence occurrence) =>
        occurrence.MeteragePoints.OrderBy(point => point.Order).ThenBy(point => point.Id).ToList();

    private static InspectionCorrectionResolution Failure(InspectionOccurrenceCorrection correction,
        InspectionCorrectionResolutionStatus status, string message,
        string locationName = "", string sectionName = "",
        OccurrenceLocationMode locationMode = OccurrenceLocationMode.Point) => new()
    {
        Correction = correction,
        Status = status,
        Message = message,
        LocationName = locationName,
        SectionName = sectionName,
        LocationMode = locationMode,
        HasPositionChange = correction.HasMeterage || correction.HasEndMeterage ||
                            correction.HasMeteragePoints && correction.MeteragePoints.Count > 0,
        ProposedMeterage = correction.Meterage,
        ProposedEndMeterage = correction.EndMeterage,
        ProposedMeteragePoints = correction.MeteragePoints.ToArray(),
        ProposedDescription = correction.Description?.Trim() ?? string.Empty
    };

    private static string BuildSectionName(InspectionSection section)
    {
        if (!string.IsNullOrWhiteSpace(section.StartPoint) || !string.IsNullOrWhiteSpace(section.EndPoint))
            return $"{section.StartPoint ?? "?"} - {section.EndPoint ?? "?"}";
        return section.Name;
    }

    private static string? NormalizeDescription(string? description, string generatedDescription)
    {
        var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return string.Equals(normalized, generatedDescription?.Trim(), StringComparison.Ordinal)
            ? null : normalized;
    }

    private static string GenerateDescriptionAt(Occurrence occurrence, decimal? meterage,
        decimal? endMeterage, IReadOnlyList<decimal> meteragePoints)
    {
        var positioned = new Occurrence
        {
            LocationMode = occurrence.LocationMode,
            Meterage = meterage,
            EndMeterage = endMeterage,
            MeteragePoints = meteragePoints.Select((value, index) => new OccurrenceMeteragePoint
            {
                Meterage = value,
                Order = index
            }).ToList(),
            Anomalies = occurrence.Anomalies,
            ReferencePoint = occurrence.ReferencePoint,
            InterruptedRecording = occurrence.InterruptedRecording,
            IsSevere = occurrence.IsSevere
        };
        return OccurrenceRules.GenerateDescription(positioned);
    }
}

namespace GeoAutoEngine.Application.VideoInspection;

public sealed record InspectionOccurrenceEdit(
    Guid SectionId,
    Guid OccurrenceId,
    decimal? Meterage,
    decimal? EndMeterage,
    IReadOnlyList<decimal> MeteragePoints,
    string Description);

// Os payloads das mensagens 6/7 (InspectionCorrectionPush, InspectionOccurrenceCorrection,
// InspectionCorrectionAcknowledgement) ficam na biblioteca compartilhada do contrato
// libs/GeoEcosystem.Contracts.Inspection (DEC-ECO-0002, M3.3b).

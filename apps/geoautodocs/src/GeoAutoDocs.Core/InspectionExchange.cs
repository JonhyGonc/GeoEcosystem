namespace GeoAutoDocs.Core;

// O documento GeoAutoDocs.Inspection v1 e o protocolo GADX ficam na biblioteca compartilhada
// libs/GeoEcosystem.Contracts.Inspection (DEC-ECO-0002, M3.3).

public enum InspectionTransportStatus
{
    Success,
    DestinationUnavailable,
    Timeout,
    IncompatibleVersion,
    InvalidPayload,
    CommunicationError,
    Cancelled
}

public sealed record InspectionTransportResult(
    InspectionTransportStatus Status,
    string Message,
    string? RemoteApplication = null,
    string? RemoteVersion = null,
    int? ProtocolVersion = null,
    int? InspectionSchemaVersion = null,
    string? Destination = null)
{
    public bool Success => Status == InspectionTransportStatus.Success;
}

public interface IInspectionTransport
{
    Task<InspectionTransportResult> SendAsync(
        InspectionExchangeDocument document,
        CancellationToken cancellationToken = default);
}

public interface IInspectionAvailabilityProbe
{
    Task<InspectionTransportResult> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default);
}

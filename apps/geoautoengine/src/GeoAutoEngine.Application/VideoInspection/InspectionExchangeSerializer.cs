using System.Diagnostics;
using System.Text.Json;

namespace GeoAutoEngine.Application.VideoInspection;

public sealed class InspectionExchangeSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public InspectionExchangeDocument Deserialize(string json) =>
        Deserialize(json, out _);

    public InspectionExchangeDocument Deserialize(
        string json,
        out InspectionExchangeDeserializeMetrics metrics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        InspectionExchangeDocument document;
        var deserialize = Stopwatch.StartNew();
        try
        {
            document = JsonSerializer.Deserialize<InspectionExchangeDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("O documento de intercâmbio está vazio.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("O arquivo de inspeção é inválido.", exception);
        }
        finally
        {
            deserialize.Stop();
        }

        var validation = Stopwatch.StartNew();
        try
        {
            Validate(document);
            PreserveSectionExportOrder(document);
        }
        finally
        {
            validation.Stop();
        }

        metrics = new InspectionExchangeDeserializeMetrics(
            deserialize.Elapsed.TotalMilliseconds,
            validation.Elapsed.TotalMilliseconds);
        return document;
    }

    public InspectionExchangeDocument Deserialize(byte[] utf8Payload)
    {
        ArgumentNullException.ThrowIfNull(utf8Payload);
        return Deserialize(System.Text.Encoding.UTF8.GetString(utf8Payload));
    }

    // As regras ficam na biblioteca compartilhada do contrato (DEC-ECO-0002, M3.3).
    public void Validate(InspectionExchangeDocument document) =>
        InspectionExchangeValidator.Validate(document);

    private static void PreserveSectionExportOrder(InspectionExchangeDocument document)
    {
        // The order of the sections in the exported JSON is authoritative. Older
        // Order values may contain gaps or stale values after sections were edited.
        for (var index = 0; index < document.Sections.Count; index++)
        {
            document.Sections[index].Order = index;
        }
    }
}

public readonly record struct InspectionExchangeDeserializeMetrics(
    double DeserializeMilliseconds,
    double ValidationMilliseconds);

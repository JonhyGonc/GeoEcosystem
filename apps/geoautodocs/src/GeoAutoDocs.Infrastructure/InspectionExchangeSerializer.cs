using System.Text.Json;
using GeoAutoDocs.Core;

namespace GeoAutoDocs.Infrastructure;

public sealed class InspectionExchangeSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Serialize(InspectionExchangeDocument document)
    {
        Validate(document);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public InspectionExchangeDocument Deserialize(string json)
    {
        InspectionExchangeDocument document;
        try
        {
            document = JsonSerializer.Deserialize<InspectionExchangeDocument>(json, JsonOptions)
                ?? throw new InvalidDataException("O documento de intercâmbio está vazio.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("O JSON de intercâmbio é inválido.", ex);
        }
        Validate(document);
        return document;
    }

    // As regras ficam na biblioteca compartilhada do contrato (DEC-ECO-0002, M3.3).
    public void Validate(InspectionExchangeDocument document) =>
        InspectionExchangeValidator.Validate(document);
}

namespace GeoAutoDocs.Core;

public static class OccurrenceLocationModeWire
{
    // SPEC GADX §8: o produtor escreve exatamente um destes seis nomes, e o GeoAutoEngine compara
    // o texto exato. O enum tem aliases (SpecificMeterage = Point, Interval = Range) e ToString()
    // não garante qual dos nomes sai, por isso o nome é escolhido aqui, explicitamente.
    public static string ToWireName(this OccurrenceLocationMode mode) => mode switch
    {
        OccurrenceLocationMode.Point => "Point",
        OccurrenceLocationMode.Range => "Range",
        OccurrenceLocationMode.EntireSection => "EntireSection",
        OccurrenceLocationMode.NotApplicable => "NotApplicable",
        OccurrenceLocationMode.MultiPoint => "MultiPoint",
        OccurrenceLocationMode.From => "From",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Tipo de localização desconhecido.")
    };
}

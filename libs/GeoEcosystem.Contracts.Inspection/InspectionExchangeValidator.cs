namespace GeoEcosystem.Contracts.Inspection;

/// <summary>
/// Validação obrigatória do documento <c>GeoAutoDocs.Inspection</c> v1 (SPEC §8).
/// Reúne as regras que antes estavam copiadas no GeoAutoEngine e no GeoAutoDocs.
/// </summary>
public static class InspectionExchangeValidator
{
    /// <exception cref="InvalidDataException">Documento inválido.</exception>
    /// <exception cref="NotSupportedException">Versão do documento não suportada.</exception>
    public static void Validate(InspectionExchangeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!string.Equals(document.Schema, InspectionExchangeSchema.Name, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Schema de intercâmbio não suportado: '{document.Schema}'.");
        }

        if (document.SchemaVersion != InspectionExchangeSchema.CurrentVersion)
        {
            throw new NotSupportedException(
                $"Schema {InspectionExchangeSchema.Name} v{document.SchemaVersion} não é suportado por esta versão.");
        }

        if (document.Project is null || document.Report is null ||
            document.Locations is null || document.Sections is null)
        {
            throw new InvalidDataException("O documento de intercâmbio está incompleto.");
        }

        if (document.Project.ProjectId == Guid.Empty)
        {
            throw new InvalidDataException("O identificador do projeto é obrigatório.");
        }

        if (document.Report.ReportId == Guid.Empty)
        {
            throw new InvalidDataException("O identificador do relatório é obrigatório.");
        }

        var locationIds = new HashSet<Guid>(document.Locations.Select(location => location.LocationId));
        if (locationIds.Contains(Guid.Empty) || locationIds.Count != document.Locations.Count)
        {
            throw new InvalidDataException("O documento contém locais sem identidade estável ou duplicados.");
        }

        if (document.Sections.Any(section =>
                section.SectionId == Guid.Empty || !locationIds.Contains(section.LocationId)))
        {
            throw new InvalidDataException("O documento contém trecho sem local válido.");
        }

        if (document.Sections.Select(section => section.SectionId).Distinct().Count() != document.Sections.Count)
        {
            throw new InvalidDataException("O documento contém trechos duplicados.");
        }

        if (document.Sections.Any(section => section.Occurrences is null || section.VideoReferences is null))
        {
            throw new InvalidDataException("O documento contém trecho incompleto.");
        }

        var occurrences = document.Sections.SelectMany(section => section.Occurrences).ToArray();
        if (occurrences.Any(occurrence => occurrence.OccurrenceId == Guid.Empty) ||
            occurrences.Select(occurrence => occurrence.OccurrenceId).Distinct().Count() != occurrences.Length)
        {
            throw new InvalidDataException("O documento contém ocorrências sem identidade estável ou duplicadas.");
        }

        if (occurrences.Any(occurrence => occurrence.MeteragePoints is null ||
                                          occurrence.Anomalies is null || occurrence.Evidence is null))
        {
            throw new InvalidDataException("O documento contém ocorrência incompleta.");
        }
    }
}

using System.Globalization;

namespace GeoAutoEngine.Application.GeoMapExport;

/// <summary>Uma entidade selecionada para exportação, com o código que ela já tem (se tiver).</summary>
public sealed record GeoMapFeatureIdCandidate(string CadHandle, Guid? ExistingFeatureId);

public enum GeoMapFeatureIdStatus
{
    /// <summary>A entidade já tinha o código e o mantém.</summary>
    Kept,

    /// <summary>A entidade não tinha código: recebeu um novo (primeira exportação).</summary>
    Assigned,

    /// <summary>O código estava repetido (cópia ou BREAK): esta entidade recebeu um novo.</summary>
    Renewed,
}

public sealed record GeoMapFeatureIdAssignment(string CadHandle, Guid FeatureId, GeoMapFeatureIdStatus Status)
{
    /// <summary>Indica se o código precisa ser gravado na entidade.</summary>
    public bool MustBeWritten => Status != GeoMapFeatureIdStatus.Kept;
}

/// <summary>
/// Código próprio de cada feição exportada para o GeoMap (DEC-ECO-0003, D-04): um GUID gravado pelo
/// GeoAutoEngine em XData, na RegApp <see cref="RegisteredApplicationName"/>. Ele é o <c>sourceId</c>
/// no contrato GeoMap e sobrevive a mover e editar a entidade.
/// </summary>
public static class GeoMapFeatureIdentity
{
    public const string RegisteredApplicationName = "GEOAUTOENGINE_FEATURE";
    public const int SchemaVersion = 1;
    public const string SchemaVersionKey = "SCHEMA_VERSION";
    public const string FeatureIdKey = "FEATURE_ID";

    /// <summary>Valores <c>CHAVE=VALOR</c> gravados na XData da entidade.</summary>
    public static IReadOnlyList<string> XDataValues(Guid featureId)
    {
        if (featureId == Guid.Empty)
        {
            throw new ArgumentException("O código da feição não pode ser vazio.", nameof(featureId));
        }

        return
        [
            $"{SchemaVersionKey}={SchemaVersion.ToString(CultureInfo.InvariantCulture)}",
            $"{FeatureIdKey}={featureId:D}",
        ];
    }

    /// <summary>Lê o código dos valores <c>CHAVE=VALOR</c> da XData. Valor ausente ou inválido = sem código.</summary>
    public static Guid? ParseXData(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var prefix = FeatureIdKey + "=";
        foreach (var value in values)
        {
            if (value is not null &&
                value.StartsWith(prefix, StringComparison.Ordinal) &&
                Guid.TryParse(value.Substring(prefix.Length), out var featureId) &&
                featureId != Guid.Empty)
            {
                return featureId;
            }
        }

        return null;
    }

    /// <summary>
    /// Decide o código de cada entidade (DEC-ECO-0003, itens 2 e 3):
    /// sem código → código novo; código único → mantém; código repetido na seleção (cópia ou BREAK)
    /// → a entidade de handle mais antigo (menor número) mantém e as outras recebem código novo.
    /// O resultado sai na mesma ordem da entrada.
    /// </summary>
    public static IReadOnlyList<GeoMapFeatureIdAssignment> Resolve(
        IReadOnlyList<GeoMapFeatureIdCandidate> candidates,
        Func<Guid> newFeatureId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(newFeatureId);

        var owners = candidates
            .Where(candidate => candidate.ExistingFeatureId is not null)
            .GroupBy(candidate => candidate.ExistingFeatureId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(candidate => HandleOrder(candidate.CadHandle))
                    .ThenBy(candidate => candidate.CadHandle, StringComparer.OrdinalIgnoreCase)
                    .First().CadHandle);

        var used = new HashSet<Guid>(owners.Keys);
        var result = new List<GeoMapFeatureIdAssignment>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.ExistingFeatureId is { } existing &&
                string.Equals(owners[existing], candidate.CadHandle, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new GeoMapFeatureIdAssignment(candidate.CadHandle, existing, GeoMapFeatureIdStatus.Kept));
                continue;
            }

            Guid fresh;
            do
            {
                fresh = newFeatureId();
            }
            while (fresh == Guid.Empty || !used.Add(fresh));

            result.Add(new GeoMapFeatureIdAssignment(
                candidate.CadHandle,
                fresh,
                candidate.ExistingFeatureId is null ? GeoMapFeatureIdStatus.Assigned : GeoMapFeatureIdStatus.Renewed));
        }

        return result;
    }

    // Handles do AutoCAD são hexadecimais crescentes: o menor é o mais antigo.
    private static ulong HandleOrder(string handle) =>
        ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? value
            : ulong.MaxValue;
}

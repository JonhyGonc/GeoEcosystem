using System.Reflection;

namespace GeoAutoDocs.Infrastructure;

public static class InspectionClientVersion
{
    // Versão do produto enviada no Hello (SPEC GADX §5.1). Antes ia a versão do assembly
    // GeoAutoDocs.Infrastructure (1.0.0.0), que nunca muda.
    public static string Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var metadata = informational.IndexOf('+');
            return metadata >= 0 ? informational[..metadata] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}

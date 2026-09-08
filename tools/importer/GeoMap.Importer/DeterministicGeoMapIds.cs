using System.Security.Cryptography;
using System.Text;

namespace GeoMap.Importer;

public static class DeterministicGeoMapIds
{
    private static readonly Guid NamespaceId = new("6a81bc1e-c60a-4d14-8fb8-f96e56b69a75");

    public static Guid Project(GeoMapImportDocument document) =>
        Project(document.Metadata);

    public static Guid Project(GeoMapImportMetadata metadata) =>
        Create($"project\u001f{metadata.SourceSystem}\u001f{metadata.Project.ExternalId}");

    public static Guid Network(Guid projectId, GeoMapImportDocument document) =>
        Network(projectId, document.Metadata);

    public static Guid Network(Guid projectId, GeoMapImportMetadata metadata) =>
        Create($"network\u001f{projectId:D}\u001f{metadata.Network.ExternalId}");

    public static Guid Feature(Guid networkId, GeoMapImportFeature feature) =>
        feature.SourceId is null
            ? Create($"anonymous-feature\u001f{networkId:D}\u001f{feature.Sequence}")
            : Create($"feature\u001f{networkId:D}\u001f{feature.SourceType}\u001f{feature.SourceId}");

    public static Guid Create(string name)
    {
        var namespaceBytes = NamespaceId.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);

        var hash = SHA1.HashData(input);
        var uuidBytes = hash[..16];
        uuidBytes[6] = (byte)((uuidBytes[6] & 0x0f) | 0x50);
        uuidBytes[8] = (byte)((uuidBytes[8] & 0x3f) | 0x80);
        SwapGuidByteOrder(uuidBytes);
        return new Guid(uuidBytes);
    }

    private static void SwapGuidByteOrder(byte[] bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }
}

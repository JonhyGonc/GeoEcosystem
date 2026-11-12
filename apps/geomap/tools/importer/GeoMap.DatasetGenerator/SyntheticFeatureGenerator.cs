using System.Buffers.Binary;

namespace GeoMap.DatasetGenerator;

public sealed class SyntheticFeatureGenerator
{
    private const ulong GoldenRatio = 0x9E3779B97F4A7C15UL;

    private static readonly int[] Diameters = [100, 150, 200, 300, 400, 600];
    private static readonly string[] Materials = ["PVC", "PEAD", "concrete", "ductile_iron"];

    public IEnumerable<SyntheticFeature> Generate(
        DatasetProfile profile,
        int seed,
        int startIndex = 0,
        int? count = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var requestedCount = count ?? profile.FeatureCount - startIndex;
        if (startIndex < 0 || requestedCount < 0 || startIndex + requestedCount > profile.FeatureCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startIndex),
                "The requested range must be inside the selected dataset profile.");
        }

        var columns = (int)Math.Ceiling(Math.Sqrt(profile.FeatureCount));
        var rows = (int)Math.Ceiling(profile.FeatureCount / (double)columns);
        var cellWidth = (profile.Extent.MaxLongitude - profile.Extent.MinLongitude) / columns;
        var cellHeight = (profile.Extent.MaxLatitude - profile.Extent.MinLatitude) / rows;
        var seedBits = unchecked((ulong)(long)seed);

        for (var index = startIndex; index < startIndex + requestedCount; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var randomA = Mix(seedBits ^ ((ulong)(index + 1) * GoldenRatio));
            var randomB = Mix(randomA);
            var randomC = Mix(randomB);

            var startLongitude = profile.Extent.MinLongitude +
                (column + 0.15 + 0.15 * ToUnitInterval(randomA)) * cellWidth;
            var endLongitude = profile.Extent.MinLongitude +
                (column + 0.70 + 0.15 * ToUnitInterval(randomB)) * cellWidth;

            var centerLatitude = profile.Extent.MinLatitude + (row + 0.50) * cellHeight;
            var verticalOffset = (ToUnitInterval(randomB) - 0.50) * 0.40 * cellHeight;
            var slope = (ToUnitInterval(randomC) - 0.50) * 0.30 * cellHeight;
            var startLatitude = centerLatitude + verticalOffset - slope / 2;
            var endLatitude = centerLatitude + verticalOffset + slope / 2;

            var diameter = Diameters[(int)(randomA % (ulong)Diameters.Length)];
            var material = Materials[(int)(randomC % (ulong)Materials.Length)];

            yield return new SyntheticFeature(
                CreateDeterministicGuid(profile.Name, seed, index),
                index,
                FormattableString.Invariant($"synthetic-{profile.Name}-{seed}-{index:D6}"),
                startLongitude,
                startLatitude,
                endLongitude,
                endLatitude,
                diameter,
                material);
        }
    }

    public static Guid CreateDatasetEntityId(
        DatasetProfile profile,
        int seed,
        string entityType) =>
        CreateDeterministicGuid($"{profile.Name}:{entityType}", seed, -1);

    private static Guid CreateDeterministicGuid(string scope, int seed, int index)
    {
        var scopeHash = 14695981039346656037UL;
        foreach (var character in scope)
        {
            scopeHash ^= character;
            scopeHash *= 1099511628211UL;
        }

        var first = Mix(scopeHash ^ unchecked((ulong)(long)seed));
        var second = Mix(first ^ unchecked((ulong)(long)index) ^ GoldenRatio);
        var bytes = new byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(0, 8), first);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8, 8), second);

        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static ulong Mix(ulong value)
    {
        value += GoldenRatio;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static double ToUnitInterval(ulong value) =>
        (value >> 11) * (1.0 / (1UL << 53));
}

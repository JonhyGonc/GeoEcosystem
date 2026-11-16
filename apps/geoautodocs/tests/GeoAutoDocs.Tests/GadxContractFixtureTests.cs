using System.Text;
using System.Text.Json;
using GeoAutoDocs.Core;
using GeoAutoDocs.Infrastructure;
using Xunit;

namespace GeoAutoDocs.Tests;

// Fixtures compartilhadas do contrato GADX 1.1 (contratos/gadx-inspecao/fixtures, M3.2).
// O GeoAutoEngine tem um teste-espelho que lê os mesmos arquivos; se um lado deixar de
// entender uma mensagem, a CI dele falha.
public sealed class GadxContractFixtureTests
{
    private static readonly string FixturesRoot = FindFixturesRoot();
    private readonly InspectionExchangeSerializer _serializer = new();

    public static TheoryData<string, int, int> Fixtures()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixturesRoot, "manifest.json")));
        var data = new TheoryData<string, int, int>();
        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            data.Add(
                fixture.GetProperty("name").GetString()!,
                fixture.GetProperty("messageType").GetInt32(),
                fixture.GetProperty("requestId").GetInt32());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Frame_ReaderReturnsHeaderAndExactPayload(string name, int messageType, int requestId)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(FramePath(name)));

        var frame = await InspectionPipeProtocol.ReadFrameAsync(stream);

        Assert.Equal(InspectionPipeProtocol.CurrentVersion, frame.ProtocolVersion);
        Assert.Equal((InspectionPipeMessageType)(ushort)messageType, frame.MessageType);
        Assert.Equal(requestId, frame.RequestId);
        Assert.Equal(File.ReadAllBytes(JsonPath(name)), frame.Payload);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Frame_WriterProducesTheFixtureBytes(string name, int messageType, int requestId)
    {
        using var stream = new MemoryStream();

        await InspectionPipeProtocol.WriteFrameAsync(
            stream,
            (InspectionPipeMessageType)(ushort)messageType,
            requestId,
            File.ReadAllBytes(JsonPath(name)));

        Assert.Equal(File.ReadAllBytes(FramePath(name)), stream.ToArray());
    }

    [Fact]
    public void Hello_LegacyFixtureIsExactlyWhatTheClientProducesToday()
    {
        var hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(Payload("hello-1.0"));

        Assert.Equal("GeoAutoDocs", hello.Application);
        Assert.Equal(InspectionExchangeSchema.CurrentVersion, hello.InspectionSchemaVersion);
        AssertJsonEquivalent(Payload("hello-1.0"), InspectionPipeProtocol.SerializePayload(hello));
    }

    [Theory]
    [InlineData("hello-ack-1.0")]
    [InlineData("hello-ack-1.1")]
    public void HelloAck_EveryVariantIsAcceptedByTheCurrentClientTypes(string name)
    {
        // A 1.1 só acrescenta campos opcionais: o cliente atual tem de ler as duas variantes.
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionHelloAck>(Payload(name));

        Assert.Equal("GeoAutoEngine", ack.Application);
        Assert.Equal(InspectionPipeProtocol.CurrentVersion, ack.ProtocolVersion);
        Assert.Equal(InspectionExchangeSchema.CurrentVersion, ack.InspectionSchemaVersion);
        Assert.Equal("2026", ack.CadVersion);
    }

    [Fact]
    public void InspectionPush_DocumentPassesValidationAndIsWhatTheClientProduces()
    {
        var json = Encoding.UTF8.GetString(Payload("inspection-push"));

        var document = _serializer.Deserialize(json);

        Assert.Equal(Guid.Parse("0e0c0000-0000-4000-8000-000000000002"), document.Report.ReportId);
        var section = Assert.Single(document.Sections);
        Assert.Equal(["Point", "Range", "MultiPoint", "EntireSection"],
            section.Occurrences.Select(occurrence => occurrence.LocationMode));
        AssertJsonEquivalent(Payload("inspection-push"), Encoding.UTF8.GetBytes(_serializer.Serialize(document)));
    }

    [Fact]
    public void InspectionAckAndError_AreReadByTheClient()
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionAcknowledgement>(Payload("inspection-ack-success"));
        var error = InspectionPipeProtocol.DeserializePayload<InspectionPipeError>(Payload("error-incompatible-version"));

        Assert.True(ack.Accepted);
        Assert.Equal("Success", ack.Status);
        Assert.Equal("IncompatibleVersion", error.Status);
    }

    [Fact]
    public void CorrectionPush_EveryFieldArrivesAsPresent()
    {
        // Estado completo (SPEC §9.2): o servidor manda todos os campos, então o cliente
        // tem de vê-los todos como presentes, inclusive os nulos e as listas vazias.
        var package = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionPush>(Payload("correction-push"));

        Assert.Equal(Guid.Parse("0e0c0000-0000-4000-8000-000000000002"), package.ReportId);
        Assert.Equal(3, package.Corrections.Count);
        Assert.All(package.Corrections, correction =>
        {
            Assert.True(correction.HasMeterage);
            Assert.True(correction.HasEndMeterage);
            Assert.True(correction.HasMeteragePoints);
            Assert.True(correction.HasDescription);
        });
        Assert.Equal(["Point", "Range", "MultiPoint"], package.Corrections.Select(correction => correction.LocationMode));
        Assert.Equal(13m, package.Corrections[0].Meterage);
        Assert.Null(package.Corrections[0].EndMeterage);
        Assert.Equal(26m, package.Corrections[1].EndMeterage);
        Assert.Equal([3m, 6m], package.Corrections[2].MeteragePoints);
    }

    [Fact]
    public void CorrectionAck_PartialFixtureIsExactlyWhatTheClientProduces()
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionAcknowledgement>(
            Payload("correction-ack-partial"));

        Assert.True(ack.Accepted);
        Assert.Equal("Partial", ack.Status);
        Assert.Equal(2, ack.AppliedCount);
        Assert.Equal(1, ack.RejectedCount);
        AssertJsonEquivalent(Payload("correction-ack-partial"), InspectionPipeProtocol.SerializePayload(ack));
    }

    private static byte[] Payload(string name) => File.ReadAllBytes(JsonPath(name));

    private static string JsonPath(string name) => Path.Combine(FixturesRoot, "json", $"{name}.json");

    private static string FramePath(string name) => Path.Combine(FixturesRoot, "frames", $"{name}.bin");

    private static string FindFixturesRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "contratos", "gadx-inspecao", "fixtures");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
                return candidate;
            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Fixtures do GADX não encontradas: rode os testes a partir de um clone do monorepo GeoEcosystem.");
    }

    private static void AssertJsonEquivalent(byte[] expected, byte[] actual)
    {
        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        AssertJsonEquivalent(expectedDocument.RootElement, actualDocument.RootElement, "$");
    }

    // Compara a estrutura: mesmos nomes de propriedade (em qualquer ordem), mesmos valores;
    // números pelo valor decimal (12.5 == 12.50).
    private static void AssertJsonEquivalent(JsonElement expected, JsonElement actual, string path)
    {
        Assert.True(expected.ValueKind == actual.ValueKind,
            $"{path}: esperado {expected.ValueKind}, obtido {actual.ValueKind}.");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(property => property.Name)
                    .OrderBy(property => property, StringComparer.Ordinal).ToArray();
                var actualNames = actual.EnumerateObject().Select(property => property.Name)
                    .OrderBy(property => property, StringComparer.Ordinal).ToArray();
                Assert.True(expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal),
                    $"{path}: propriedades esperadas [{string.Join(", ", expectedNames)}], " +
                    $"obtidas [{string.Join(", ", actualNames)}].");
                foreach (var property in expected.EnumerateObject())
                    AssertJsonEquivalent(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                break;
            case JsonValueKind.Array:
                Assert.True(expected.GetArrayLength() == actual.GetArrayLength(),
                    $"{path}: tamanho esperado {expected.GetArrayLength()}, obtido {actual.GetArrayLength()}.");
                var index = 0;
                foreach (var (expectedItem, actualItem) in expected.EnumerateArray().Zip(actual.EnumerateArray()))
                    AssertJsonEquivalent(expectedItem, actualItem, $"{path}[{index++}]");
                break;
            case JsonValueKind.Number:
                Assert.True(expected.GetDecimal() == actual.GetDecimal(),
                    $"{path}: esperado {expected.GetRawText()}, obtido {actual.GetRawText()}.");
                break;
            case JsonValueKind.String:
                Assert.True(string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal),
                    $"{path}: esperado \"{expected.GetString()}\", obtido \"{actual.GetString()}\".");
                break;
        }
    }
}

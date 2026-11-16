using System.Text.Json;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

// Fixtures compartilhadas do contrato GADX 1.1 (contratos/gadx-inspecao/fixtures, M3.2).
// O GeoAutoDocs tem um teste-espelho que lê os mesmos arquivos; se um lado deixar de
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
    [Trait("Category", "FAST")]
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
    [Trait("Category", "FAST")]
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

    [Theory]
    [InlineData("hello-1.0")]
    [InlineData("hello-1.1")]
    [InlineData("hello-1.1-sem-correcoes")]
    [Trait("Category", "FAST")]
    public void Hello_EveryVariantIsAcceptedByTheCurrentServerTypes(string name)
    {
        // A 1.1 só acrescenta campos opcionais: o servidor atual tem de ler todas as variantes.
        var hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(Payload(name));

        Assert.Equal("GeoAutoDocs", hello.Application);
        Assert.Equal(InspectionPipeProtocol.CurrentVersion, hello.ProtocolVersion);
        Assert.Equal(InspectionExchangeSchema.CurrentVersion, hello.InspectionSchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(hello.ApplicationVersion));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Hello_CapabilitiesMatchTheSpecification()
    {
        using var legacy = JsonDocument.Parse(Payload("hello-1.0"));
        using var current = JsonDocument.Parse(Payload("hello-1.1"));
        using var none = JsonDocument.Parse(Payload("hello-1.1-sem-correcoes"));

        Assert.False(legacy.RootElement.TryGetProperty("capabilities", out _));
        Assert.Equal(
            ["corrections"],
            current.RootElement.GetProperty("capabilities").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, none.RootElement.GetProperty("capabilities").GetArrayLength());
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void HelloAck_LegacyFixtureIsExactlyWhatTheServerProducesToday()
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionHelloAck>(Payload("hello-ack-1.0"));

        Assert.Equal("GeoAutoEngine", ack.Application);
        Assert.Equal("Civil 3D", ack.CadProduct);
        Assert.Equal("2026", ack.CadVersion);
        AssertJsonEquivalent(Payload("hello-ack-1.0"), InspectionPipeProtocol.SerializePayload(ack));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void HelloAck_V11FixtureIsReadByTheCurrentTypes()
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionHelloAck>(Payload("hello-ack-1.1"));

        Assert.Equal("GeoAutoEngine", ack.Application);
        Assert.Equal(InspectionPipeProtocol.CurrentVersion, ack.ProtocolVersion);
        Assert.Equal(InspectionExchangeSchema.CurrentVersion, ack.InspectionSchemaVersion);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void InspectionPush_DocumentPassesTheServerValidation()
    {
        var document = _serializer.Deserialize(Payload("inspection-push"));

        Assert.Equal(Guid.Parse("0e0c0000-0000-4000-8000-000000000002"), document.Report.ReportId);
        var section = Assert.Single(document.Sections);
        Assert.Equal(
            ["Point", "Range", "MultiPoint", "EntireSection"],
            section.Occurrences.Select(occurrence => occurrence.LocationMode));
        var point = section.Occurrences[0];
        Assert.Equal(12.5m, point.Meterage);
        Assert.Equal(TimeSpan.FromSeconds(75), point.VideoTimestamp);
        Assert.Single(point.Anomalies);
        Assert.True(Assert.Single(point.Evidence).IsPrimary);
        Assert.Equal([3m, 6m, 9m], section.Occurrences[2].MeteragePoints);
    }

    [Theory]
    [InlineData("inspection-ack-success")]
    [Trait("Category", "FAST")]
    public void InspectionAck_FixtureIsExactlyWhatTheServerProduces(string name)
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionAcknowledgement>(Payload(name));

        Assert.True(ack.Accepted);
        Assert.Equal("Success", ack.Status);
        AssertJsonEquivalent(Payload(name), InspectionPipeProtocol.SerializePayload(ack));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void Error_FixtureIsExactlyWhatTheServerProduces()
    {
        var error = InspectionPipeProtocol.DeserializePayload<InspectionPipeError>(
            Payload("error-incompatible-version"));

        Assert.Equal("IncompatibleVersion", error.Status);
        AssertJsonEquivalent(Payload("error-incompatible-version"), InspectionPipeProtocol.SerializePayload(error));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CorrectionPush_ServerSendsTheCompleteStateWithTheFixtureShape()
    {
        var push = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionPush>(Payload("correction-push"));

        Assert.Equal(Guid.Parse("0e0c0000-0000-4000-8000-000000000002"), push.ReportId);
        Assert.Equal(3, push.Corrections.Count);
        Assert.Equal(13m, push.Corrections[0].Meterage);
        Assert.Null(push.Corrections[0].EndMeterage);
        Assert.Equal(26m, push.Corrections[1].EndMeterage);
        Assert.Equal([3m, 6m], push.Corrections[2].MeteragePoints);

        // Estado completo (SPEC §9.2): o que o servidor serializa tem todos os campos da fixture.
        AssertJsonEquivalent(Payload("correction-push"), InspectionPipeProtocol.SerializePayload(push));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CorrectionAck_PartialFixtureIsReadByTheServer()
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionAcknowledgement>(
            Payload("correction-ack-partial"));

        Assert.True(ack.Accepted);
        Assert.Equal("Partial", ack.Status);
        Assert.Equal(3, ack.RequestedCount);
        Assert.Equal(2, ack.AppliedCount);
        Assert.Equal(1, ack.RejectedCount);
        Assert.Equal(["Applied", "Applied", "Invalid"], ack.Results.Select(result => result.Status));
        Assert.False(ack.Results[2].Applied);
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
            {
                return candidate;
            }

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
        Assert.True(
            expected.ValueKind == actual.ValueKind,
            $"{path}: esperado {expected.ValueKind}, obtido {actual.ValueKind}.");
        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(property => property.Name)
                    .OrderBy(property => property, StringComparer.Ordinal).ToArray();
                var actualNames = actual.EnumerateObject().Select(property => property.Name)
                    .OrderBy(property => property, StringComparer.Ordinal).ToArray();
                Assert.True(
                    expectedNames.SequenceEqual(actualNames, StringComparer.Ordinal),
                    $"{path}: propriedades esperadas [{string.Join(", ", expectedNames)}], " +
                    $"obtidas [{string.Join(", ", actualNames)}].");
                foreach (var property in expected.EnumerateObject())
                {
                    AssertJsonEquivalent(property.Value, actual.GetProperty(property.Name), $"{path}.{property.Name}");
                }

                break;
            case JsonValueKind.Array:
                Assert.True(
                    expected.GetArrayLength() == actual.GetArrayLength(),
                    $"{path}: tamanho esperado {expected.GetArrayLength()}, obtido {actual.GetArrayLength()}.");
                var index = 0;
                foreach (var (expectedItem, actualItem) in expected.EnumerateArray().Zip(actual.EnumerateArray()))
                {
                    AssertJsonEquivalent(expectedItem, actualItem, $"{path}[{index++}]");
                }

                break;
            case JsonValueKind.Number:
                Assert.True(
                    expected.GetDecimal() == actual.GetDecimal(),
                    $"{path}: esperado {expected.GetRawText()}, obtido {actual.GetRawText()}.");
                break;
            case JsonValueKind.String:
                Assert.True(
                    string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal),
                    $"{path}: esperado \"{expected.GetString()}\", obtido \"{actual.GetString()}\".");
                break;
        }
    }
}

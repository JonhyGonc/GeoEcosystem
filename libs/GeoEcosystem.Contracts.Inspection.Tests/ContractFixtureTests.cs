using System.Text.Json;

namespace GeoEcosystem.Contracts.Inspection.Tests;

// A biblioteca é a fonte do contrato: ela tem de ler e produzir exatamente as fixtures
// de contratos/gadx-inspecao/fixtures (M3.2), inclusive as variantes 1.1.
public sealed class ContractFixtureTests
{
    private static readonly string FixturesRoot = FixtureFiles.Root;

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
        Assert.Equal(Payload(name), frame.Payload);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Frame_WriterProducesTheFixtureBytes(string name, int messageType, int requestId)
    {
        using var arrayStream = new MemoryStream();
        using var memoryStream = new MemoryStream();

        await InspectionPipeProtocol.WriteFrameAsync(
            arrayStream, (InspectionPipeMessageType)(ushort)messageType, requestId, Payload(name));
        await InspectionPipeProtocol.WriteFrameAsync(
            memoryStream, (InspectionPipeMessageType)(ushort)messageType, requestId,
            new ReadOnlyMemory<byte>(Payload(name)));

        Assert.Equal(File.ReadAllBytes(FramePath(name)), arrayStream.ToArray());
        Assert.Equal(File.ReadAllBytes(FramePath(name)), memoryStream.ToArray());
    }

    [Theory]
    [InlineData("hello-1.0")]
    [InlineData("hello-1.1")]
    [InlineData("hello-1.1-sem-correcoes")]
    public void Hello_RoundTripsExactly(string name)
    {
        var hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(Payload(name));

        JsonAssert.Equivalent(Payload(name), InspectionPipeProtocol.SerializePayload(hello));
    }

    [Fact]
    public void Hello_CapabilitiesDistinguishAbsentFromEmpty()
    {
        var legacy = InspectionPipeProtocol.DeserializePayload<InspectionHello>(Payload("hello-1.0"));
        var current = InspectionPipeProtocol.DeserializePayload<InspectionHello>(Payload("hello-1.1"));
        var none = InspectionPipeProtocol.DeserializePayload<InspectionHello>(Payload("hello-1.1-sem-correcoes"));

        Assert.Null(legacy.Capabilities);
        Assert.True(InspectionCapabilities.Declares(current.Capabilities, InspectionCapabilities.Corrections));
        Assert.NotNull(none.Capabilities);
        Assert.Empty(none.Capabilities);
        Assert.False(InspectionCapabilities.Declares(none.Capabilities, InspectionCapabilities.Corrections));
    }

    [Theory]
    [InlineData("hello-ack-1.0")]
    [InlineData("hello-ack-1.1")]
    public void HelloAck_RoundTripsExactly(string name)
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionHelloAck>(Payload(name));

        JsonAssert.Equivalent(Payload(name), InspectionPipeProtocol.SerializePayload(ack));
    }

    [Fact]
    public void HelloWithoutCapabilities_DoesNotWriteTheField()
    {
        var json = InspectionPipeProtocol.SerializePayload(new InspectionHello { ApplicationVersion = "1.1.0" });

        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("capabilities", out _));
    }

    [Theory]
    [InlineData("inspection-ack-success")]
    public void InspectionAck_RoundTripsExactly(string name)
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionAcknowledgement>(Payload(name));

        JsonAssert.Equivalent(Payload(name), InspectionPipeProtocol.SerializePayload(ack));
    }

    [Fact]
    public void Error_RoundTripsExactly()
    {
        var error = InspectionPipeProtocol.DeserializePayload<InspectionPipeError>(Payload("error-incompatible-version"));

        JsonAssert.Equivalent(Payload("error-incompatible-version"), InspectionPipeProtocol.SerializePayload(error));
    }

    [Fact]
    public void InspectionPush_DocumentIsValidAndRoundTripsExactly()
    {
        var document = InspectionPipeProtocol.DeserializePayload<InspectionExchangeDocument>(Payload("inspection-push"));

        InspectionExchangeValidator.Validate(document);
        Assert.Equal(
            ["Point", "Range", "MultiPoint", "EntireSection"],
            Assert.Single(document.Sections).Occurrences.Select(occurrence => occurrence.LocationMode));
        JsonAssert.Equivalent(Payload("inspection-push"), InspectionPipeProtocol.SerializePayload(document));
    }

    private static byte[] Payload(string name) => FixtureFiles.Payload(name);

    private static string FramePath(string name) => FixtureFiles.FramePath(name);
}

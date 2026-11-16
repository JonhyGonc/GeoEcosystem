namespace GeoEcosystem.Contracts.Inspection.Tests;

// Regras S1–S3 da SPEC §7, aplicadas às fixtures do Hello.
public sealed class CapabilityRuleTests
{
    [Theory]
    [InlineData("hello-1.0", true)]               // S2: cliente 1.0 (GAD 1.1.0) continua recebendo correções
    [InlineData("hello-1.1", true)]               // declarou "corrections"
    [InlineData("hello-1.1-sem-correcoes", false)] // S1: declarou capacidades, mas não "corrections"
    public void ClientAcceptsCorrections_FollowsTheSpecification(string name, bool expected)
    {
        var hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(FixtureFiles.Payload(name));

        Assert.Equal(expected, InspectionCapabilities.ClientAcceptsCorrections(hello));
    }

    [Theory]
    [InlineData("hello-1.0", "hello-ack-1.1")]
    [InlineData("hello-1.1", "hello-ack-1.1")]
    public void HelloAck_AnnouncesCorrectionsWhenTheServerMaySendThem(string helloName, string helloAckName)
    {
        // S3: o HelloAck do servidor 1.1 é exatamente a fixture hello-ack-1.1.
        var hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(FixtureFiles.Payload(helloName));
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionHelloAck>(FixtureFiles.Payload(helloAckName));
        ack.Capabilities = InspectionCapabilities.ServerCapabilitiesFor(hello);

        JsonAssert.Equivalent(FixtureFiles.Payload(helloAckName), InspectionPipeProtocol.SerializePayload(ack));
    }

    [Fact]
    public void HelloAck_ListIsEmptyButPresentWhenTheClientRefusesCorrections()
    {
        var hello = InspectionPipeProtocol.DeserializePayload<InspectionHello>(
            FixtureFiles.Payload("hello-1.1-sem-correcoes"));

        var capabilities = InspectionCapabilities.ServerCapabilitiesFor(hello);

        Assert.Empty(capabilities);
    }

    [Fact]
    public void Declares_IgnoresCaseAndUnknownValues()
    {
        Assert.True(InspectionCapabilities.Declares(["futura", "CORRECTIONS"], InspectionCapabilities.Corrections));
        Assert.False(InspectionCapabilities.Declares(["futura"], InspectionCapabilities.Corrections));
        Assert.False(InspectionCapabilities.Declares(null, InspectionCapabilities.Corrections));
    }
}

using System.Text;

namespace GeoEcosystem.Contracts.Inspection.Tests;

// Correções (mensagens 6/7, SPEC §9) contra as fixtures do M3.2.
public sealed class CorrectionContractTests
{
    [Fact]
    public void CorrectionPush_RoundTripsWithEveryFieldOfTheCompleteState()
    {
        var push = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionPush>(FixtureFiles.Payload("correction-push"));

        Assert.Equal(3, push.Corrections.Count);
        Assert.All(push.Corrections, correction =>
        {
            Assert.True(correction.HasMeterage);
            Assert.True(correction.HasEndMeterage);
            Assert.True(correction.HasMeteragePoints);
            Assert.True(correction.HasDescription);
        });
        Assert.Equal(["Point", "Range", "MultiPoint"], push.Corrections.Select(correction => correction.LocationMode));
        Assert.Equal([3m, 6m], push.Corrections[2].MeteragePoints);

        // O produtor escreve todos os campos, inclusive nulos (estado completo).
        JsonAssert.Equivalent(FixtureFiles.Payload("correction-push"), InspectionPipeProtocol.SerializePayload(push));
    }

    [Fact]
    public void CorrectionPush_ProducerWritesNullsAndEmptyLists()
    {
        var push = new InspectionCorrectionPush
        {
            ReportId = Guid.NewGuid(),
            Corrections =
            [
                new InspectionOccurrenceCorrection
                {
                    SectionId = Guid.NewGuid(),
                    OccurrenceId = Guid.NewGuid(),
                    LocationMode = "Point",
                    Meterage = 1m,
                    EndMeterage = null,
                    MeteragePoints = [],
                    Description = "Texto",
                },
            ],
        };

        var json = Encoding.UTF8.GetString(InspectionPipeProtocol.SerializePayload(push));

        Assert.Contains("\"endMeterage\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"meteragePoints\":[]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CorrectionItem_AbsentFieldMeansDoNotChange()
    {
        // Tolerância da SPEC §9.2: campo ausente (não nulo) = "não alterar".
        var json = """
            {"reportId":"0e0c0000-0000-4000-8000-000000000002","corrections":[
              {"sectionId":"0e0c0000-0000-4000-8000-000000000004",
               "occurrenceId":"0e0c0000-0000-4000-8000-000000000011",
               "locationMode":"Point","meterage":13.75}]}
            """u8;

        var correction = Assert.Single(InspectionPipeProtocol.DeserializePayload<InspectionCorrectionPush>(json).Corrections);

        Assert.True(correction.HasMeterage);
        Assert.Equal(13.75m, correction.Meterage);
        Assert.False(correction.HasEndMeterage);
        Assert.False(correction.HasMeteragePoints);
        Assert.False(correction.HasDescription);
    }

    [Fact]
    public void CorrectionAck_PartialRoundTripsAndComputesRejectedCount()
    {
        var ack = InspectionPipeProtocol.DeserializePayload<InspectionCorrectionAcknowledgement>(
            FixtureFiles.Payload("correction-ack-partial"));

        Assert.True(ack.Accepted);
        Assert.Equal("Partial", ack.Status);
        Assert.Equal(2, ack.AppliedCount);
        Assert.Equal(1, ack.RejectedCount);
        Assert.Equal(
            [true, true, false],
            ack.Results.Select(InspectionCorrectionItemStatus.IsConfirmed));
        JsonAssert.Equivalent(FixtureFiles.Payload("correction-ack-partial"), InspectionPipeProtocol.SerializePayload(ack));
    }

    [Fact]
    public void CorrectionAck_UnchangedItemsCountAsApplied()
    {
        var ack = new InspectionCorrectionAcknowledgement
        {
            RequestedCount = 2,
            AppliedCount = 1,
            Results =
            [
                new InspectionCorrectionItemAcknowledgement { Applied = true, Status = InspectionCorrectionItemStatus.Applied },
                new InspectionCorrectionItemAcknowledgement { Applied = false, Status = InspectionCorrectionItemStatus.Unchanged },
            ],
        };

        Assert.Equal(2, ack.AppliedCount);
        Assert.Equal(0, ack.RejectedCount);
    }
}

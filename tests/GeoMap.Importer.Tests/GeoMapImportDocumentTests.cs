using System.Text;
using Xunit;

namespace GeoMap.Importer.Tests;

public sealed class GeoMapImportDocumentTests
{
    [Fact]
    public void Parse_reads_normalized_document()
    {
        var document = GeoMapImportDocument.Parse(ValidDocument());

        Assert.Equal("Civil 3D", document.SourceSystem);
        Assert.Equal(31982, document.SourceSrid);
        Assert.Equal("metre", document.Units);
        Assert.Equal("project-001", document.Project.ExternalId);
        Assert.Equal("stormwater", document.Network.Type);
        var feature = Assert.Single(document.Features);
        Assert.Equal("2A7F", feature.SourceId);
        Assert.Equal("Polyline", feature.SourceType);
        Assert.Contains("LineString", feature.GeometryJson, StringComparison.Ordinal);
        Assert.Contains("diameter", feature.PropertiesJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CountReceivedFeatures_returns_zero_for_invalid_json()
    {
        Assert.Equal(0, GeoMapImportDocument.CountReceivedFeatures("{"u8.ToArray()));
    }

    [Fact]
    public void Deterministic_ids_are_stable_and_scoped()
    {
        var document = GeoMapImportDocument.Parse(ValidDocument());
        var projectId = DeterministicGeoMapIds.Project(document);
        var networkId = DeterministicGeoMapIds.Network(projectId, document);
        var featureId = DeterministicGeoMapIds.Feature(networkId, document.Features[0]);

        Assert.Equal(projectId, DeterministicGeoMapIds.Project(document));
        Assert.Equal(networkId, DeterministicGeoMapIds.Network(projectId, document));
        Assert.Equal(featureId, DeterministicGeoMapIds.Feature(networkId, document.Features[0]));
        Assert.NotEqual(projectId, networkId);
        Assert.NotEqual(networkId, featureId);
    }

    [Fact]
    public void Streaming_records_parse_to_the_same_logical_model()
    {
        var metadata = GeoMapImportDocument.ParseStreamingMetadata(
            StreamingMetadata());
        var feature = GeoMapImportDocument.ParseStreamingFeature(
            StreamingFeature(), 42);

        Assert.Equal(31982, metadata.SourceSrid);
        Assert.Equal("stormwater", metadata.Network.Type);
        Assert.Equal(42, feature.Sequence);
        Assert.Equal("2A7F", feature.SourceId);
        Assert.Contains("LineString", feature.GeometryJson, StringComparison.Ordinal);
    }

    internal static byte[] ValidDocument() => Encoding.UTF8.GetBytes("""
        {
          "contractVersion": "0.1",
          "sourceSystem": "Civil 3D",
          "sourceCrs": "EPSG:31982",
          "units": "metre",
          "project": { "externalId": "project-001", "name": "Projeto" },
          "network": { "externalId": "network-001", "name": "Rede", "type": " StormWater " },
          "exportedAt": "2026-09-08T14:00:00Z",
          "features": [
            {
              "sourceId": "2A7F",
              "sourceType": "Polyline",
              "sourceLayer": "REDE-DRENAGEM",
              "geometry": { "type": "LineString", "coordinates": [[688100.25, 7086400.75], [688112.5, 7086411.0]] },
              "properties": { "diameter": 300 }
            }
          ]
        }
        """);

    internal static byte[] StreamingMetadata() => Encoding.UTF8.GetBytes("""
        {"contractVersion":"0.1","sourceSystem":"Civil 3D","sourceCrs":"EPSG:31982","units":"metre","project":{"externalId":"project-001","name":"Projeto"},"network":{"externalId":"network-001","name":"Rede","type":"StormWater"},"exportedAt":"2026-09-08T14:00:00Z"}
        """);

    internal static byte[] StreamingFeature() => Encoding.UTF8.GetBytes("""
        {"sourceId":"2A7F","sourceType":"Polyline","sourceLayer":"REDE-DRENAGEM","geometry":{"type":"LineString","coordinates":[[688100.25,7086400.75],[688112.5,7086411.0]]},"properties":{"diameter":300}}
        """);
}

using System.Text;
using System.Text.Json;
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
    public void Parse_reads_the_optional_network_color_in_uppercase()
    {
        var json = Encoding.UTF8.GetString(ValidDocument())
            .Replace("\"contractVersion\": \"0.1\"", "\"contractVersion\": \"0.2\"", StringComparison.Ordinal)
            .Replace("\"type\": \" StormWater \" }", "\"type\": \" StormWater \", \"color\": \"#1a2b3c\" }", StringComparison.Ordinal);

        var document = GeoMapImportDocument.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal("#1A2B3C", document.Network.Color);
        Assert.Null(GeoMapImportDocument.Parse(ValidDocument()).Network.Color);
    }

    [Fact]
    public void Streaming_metadata_reads_the_optional_network_color()
    {
        var json = Encoding.UTF8.GetString(StreamingMetadata())
            .Replace("\"contractVersion\":\"0.1\"", "\"contractVersion\":\"0.2\"", StringComparison.Ordinal)
            .Replace("\"type\":\"StormWater\"}", "\"type\":\"StormWater\",\"color\":\"#00ff7f\"}", StringComparison.Ordinal);

        var metadata = GeoMapImportDocument.ParseStreamingMetadata(Encoding.UTF8.GetBytes(json));

        Assert.Equal("#00FF7F", metadata.Network.Color);
        Assert.Equal("0.2", metadata.ContractVersion);
    }

    [Fact]
    public void Streaming_metadata_keeps_the_declared_contract_version()
    {
        var json = Encoding.UTF8.GetString(StreamingMetadata())
            .Replace("\"contractVersion\":\"0.1\"", "\"contractVersion\":\"0.3\"", StringComparison.Ordinal);

        Assert.Equal("0.1", GeoMapImportDocument.ParseStreamingMetadata(StreamingMetadata()).ContractVersion);
        Assert.Equal(
            "0.3",
            GeoMapImportDocument.ParseStreamingMetadata(Encoding.UTF8.GetBytes(json)).ContractVersion);
    }

    [Fact]
    public void Streaming_point_feature_parses_for_block_references()
    {
        var feature = GeoMapImportDocument.ParseStreamingFeature(
            Encoding.UTF8.GetBytes("""
                {"sourceId":"B10C","sourceType":"BlockReference","sourceLayer":"CONEXOES","geometry":{"type":"Point","coordinates":[688100.25,7086400.75]},"properties":{"bloco":"TE"}}
                """),
            7);

        Assert.Equal("BlockReference", feature.SourceType);
        Assert.Contains("Point", feature.GeometryJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_accepts_the_reference_GeoAutoEngine_export()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "valid-geoautoengine-polyline.geomap.json");

        var document = GeoMapImportDocument.Parse(File.ReadAllBytes(path));

        Assert.Equal("project-001", document.Project.ExternalId);
        Assert.Equal("project-001:aguas_pluviais", document.Network.ExternalId);
        Assert.Equal("Águas pluviais", document.Network.Name);
        Assert.Equal("stormwater", document.Network.Type);
        var feature = Assert.Single(document.Features);
        Assert.Equal("2A7F", feature.SourceId);
        Assert.Equal("GF-Rede Pluvial", feature.SourceLayer);
        Assert.Equal("Polyline", feature.SourceType);
        using var properties = JsonDocument.Parse(feature.PropertiesJson);
        Assert.Equal("2A7F", properties.RootElement.GetProperty("cadHandle").GetString());
        Assert.Equal("Polyline", properties.RootElement.GetProperty("cadEntityType").GetString());
        Assert.Equal(2, properties.RootElement.GetProperty("vertexCount").GetInt32());
        Assert.False(properties.RootElement.GetProperty("projectedTo2D").GetBoolean());
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

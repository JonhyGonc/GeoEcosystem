using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeoAutoEngine.Application.GeoMapExport;

public static class GeoMapExportSerializer
{
    // Versão mínima que aceita o arquivo, como a ponte DXF: 0.3 com pontos de blocos (M4.3c),
    // 0.2 com cor da rede (M4.3b), senão 0.1.
    public const string ContractVersion = "0.1";
    public const string ContractVersionWithColor = "0.2";
    public const string ContractVersionWithPoints = "0.3";
    // Produtor oficial do contrato GeoMap (DEC-ECO-0003). Antes: "Civil 3D".
    public const string SourceSystem = "GeoAutoEngine";
    public const int MaximumFeatures = 10_000;
    public const int MaximumVerticesPerFeature = 100_000;

    private static readonly HashSet<string> SupportedUnits =
        new(StringComparer.Ordinal)
        {
            "metre",
            "foot",
            "us-survey-foot",
            "degree",
        };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static string Serialize(GeoMapExportRequest request)
    {
        Validate(request);

        var document = new DocumentDto(
            ContractVersionFor(request),
            SourceSystem,
            request.SourceCrs,
            request.Units,
            new ProjectDto(request.ProjectExternalId, request.ProjectName),
            new NetworkDto(
                request.NetworkExternalId,
                request.NetworkName,
                request.NetworkType.Trim().ToLowerInvariant(),
                request.NetworkColor),
            request.ExportedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            request.Features.Select(feature => CreateFeature(feature, request.Units)).ToArray());

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    public static string ContractVersionFor(GeoMapExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Features.Any(feature => feature.Block is not null))
        {
            return ContractVersionWithPoints;
        }

        return request.NetworkColor is null ? ContractVersion : ContractVersionWithColor;
    }

    private static FeatureDto CreateFeature(GeoMapExportFeature feature, string units)
    {
        var layerAttributes = GeoMapLayerNetworks.Attributes(feature.SourceLayer);
        if (feature.Block is { } block)
        {
            var position = feature.Vertices[0];
            return new(
                feature.SourceId,
                "BlockReference",
                feature.SourceLayer,
                new GeometryDto("Point", new[] { position.X, position.Y }),
                BlockProperties(feature, block, layerAttributes));
        }

        // Arcos viram trechos retos com no máximo 5 mm de diferença (M4.3d); sem arcos, as posições são os
        // vértices, e a linha fechada repete o primeiro no fim.
        var positions = GeoMapArcTessellation.Positions(
            feature.Vertices,
            feature.Bulges,
            feature.IsClosed,
            GeoMapArcTessellation.ToleranceFor(units));
        if (positions.Count > MaximumVerticesPerFeature)
        {
            throw new InvalidOperationException(
                $"A {feature.EntityType} {feature.CadHandle} excede {MaximumVerticesPerFeature:N0} posições depois de trocar os arcos por trechos retos.");
        }

        var coordinates = positions.Select(position => new[] { position.X, position.Y }).ToList();
        var arcs = feature.Bulges?.Take(feature.IsClosed ? feature.Vertices.Count : feature.Vertices.Count - 1)
            .Count(GeoMapArcTessellation.IsArc) ?? 0;
        var depths = feature.Depths;
        var measurements = depths is null || depths.Measurements.Count == 0
            ? null
            : depths.Measurements.Select(measurement => new DepthMeasurementDto(
                measurement.Point,
                measurement.DepthMetres,
                measurement.Vertex,
                measurement.StationMetres,
                measurement.Code,
                measurement.Method,
                measurement.Shared ? true : null)).ToArray();
        object properties = feature.EntityType == GeoMapEntityTypes.Line
            ? new LinePropertiesDto(
                feature.CadHandle,
                feature.EntityType,
                feature.Vertices.Count,
                feature.Elevation,
                feature.EndElevation ?? feature.Elevation,
                feature.ProjectedTo2D,
                layerAttributes.DiameterMm,
                layerAttributes.Material,
                depths?.Measurements.Count,
                depths?.StartMetres,
                depths?.EndMetres,
                measurements,
                depths is null ? null : DepthSource,
                depths is null ? null : DepthReference)
            : new PropertiesDto(
                feature.CadHandle,
                feature.EntityType,
                feature.Vertices.Count,
                feature.IsClosed,
                feature.Elevation,
                feature.ProjectedTo2D,
                arcs == 0 ? null : arcs,
                layerAttributes.DiameterMm,
                layerAttributes.Material,
                depths?.Measurements.Count,
                depths?.StartMetres,
                depths?.EndMetres,
                measurements,
                depths is null ? null : DepthSource,
                depths is null ? null : DepthReference);
        if (depths is not null)
        {
            CheckPropertiesSize(feature, properties, depths.Measurements.Count);
        }

        return new(
            feature.SourceId,
            feature.EntityType,
            feature.SourceLayer,
            new GeometryDto("LineString", coordinates),
            properties);
    }

    // Profundidades (pontos _TG): só o conteúdo de properties muda, e o contrato aceita isso sem versão nova.
    // Provisório até as medições ficarem gravadas no DWG (entrega 2).
    internal const string DepthSource = "levantamento _TG (associação automática; provisória até a gravação no DWG)";

    // Convenção confirmada pelo usuário em 17/11/2026 (DEC-ECO-0005): o valor do código vai até a geratriz superior.
    internal const string DepthReference = "geratriz superior";

    // O GeoMap recusa properties com mais de 64 KiB, medidos no texto do arquivo (com a indentação). Uma linha
    // com medições demais para do jeito explícito, em vez de perder medições.
    private const int MaximumPropertiesBytes = 64 * 1024;
    private const int PropertiesIndentInFile = 6;

    private static void CheckPropertiesSize(GeoMapExportFeature feature, object properties, int measurements)
    {
        var text = JsonSerializer.Serialize(properties, properties.GetType(), SerializerOptions);
        var bytes = System.Text.Encoding.UTF8.GetByteCount(text) + (text.Count(character => character == '\n') * PropertiesIndentInFile);
        if (bytes > MaximumPropertiesBytes)
        {
            throw new InvalidOperationException(
                $"A {feature.EntityType} {feature.CadHandle} tem {measurements} medições de profundidade, mais do que " +
                "cabe numa feição do GeoMap (64 KiB). Nada foi gravado. Divida a linha (BREAK) e exporte de novo.");
        }
    }

    private static void Validate(GeoMapExportRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        Required(request.SourceCrs, 16, "O CRS de origem");
        if (!request.SourceCrs.StartsWith("EPSG:", StringComparison.Ordinal) ||
            !int.TryParse(
                request.SourceCrs.Substring("EPSG:".Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var epsg) ||
            epsg <= 0)
        {
            throw new InvalidOperationException("O CRS deve usar o formato EPSG:<código>, por exemplo EPSG:31982.");
        }

        if (!SupportedUnits.Contains(request.Units))
        {
            throw new InvalidOperationException("A unidade deve ser metre, foot, us-survey-foot ou degree.");
        }

        Required(request.ProjectExternalId, 255, "O identificador do projeto");
        Required(request.ProjectName, 200, "O nome do projeto");
        Required(request.NetworkExternalId, 255, "O identificador da rede");
        Required(request.NetworkName, 200, "O nome da rede");
        Required(request.NetworkType, 100, "O tipo da rede");
        if (request.NetworkColor is not null && !GeoMapLayerColor.IsValid(request.NetworkColor))
        {
            throw new InvalidOperationException("A cor da rede deve usar o formato #RRGGBB, em maiúsculas.");
        }

        if (request.Features is null || request.Features.Count == 0)
        {
            throw new InvalidOperationException("Selecione pelo menos uma Polyline ou bloco válido para exportar.");
        }

        if (request.Features.Count > MaximumFeatures)
        {
            throw new InvalidOperationException($"O contrato GeoMap aceita no máximo {MaximumFeatures:N0} feições por arquivo.");
        }

        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in request.Features)
        {
            var kind = feature.Block is null ? feature.EntityType : "bloco";
            Required(feature.SourceId, 255, "O código da feição");
            Required(feature.CadHandle, 255, "O handle da feição");
            Required(feature.SourceLayer, 255, "O layer da feição");
            if (!identities.Add(feature.SourceId))
            {
                throw new InvalidOperationException(
                    $"O código {feature.SourceId} ({kind} {feature.CadHandle}) aparece mais de uma vez na seleção.");
            }

            if (feature.Block is { } block)
            {
                ValidateBlock(feature, block);
                continue;
            }

            if (feature.Vertices is null || feature.Vertices.Count < 2)
            {
                throw new InvalidOperationException($"A {kind} {feature.CadHandle} precisa ter pelo menos dois vértices.");
            }

            ValidateLine(feature);

            var exportedVertexCount = feature.Vertices.Count +
                (feature.IsClosed && feature.Vertices[0] != feature.Vertices[feature.Vertices.Count - 1] ? 1 : 0);
            if (exportedVertexCount > MaximumVerticesPerFeature)
            {
                throw new InvalidOperationException(
                    $"A {kind} {feature.CadHandle} excede {MaximumVerticesPerFeature:N0} vértices no contrato GeoMap.");
            }

            if (!IsFinite(feature.Elevation) ||
                feature.Vertices.Any(vertex => !IsFinite(vertex.X) || !IsFinite(vertex.Y)))
            {
                throw new InvalidOperationException($"A {kind} {feature.CadHandle} contém coordenadas não finitas.");
            }
        }
    }

    // Mesmas propriedades e ordem da ponte DXF (DEC-029 do GeoMap), depois o diâmetro e o material da layer e os
    // atributos do bloco (M4.3e). Um atributo com nome de propriedade gerada ganha o prefixo "atributo_".
    private static readonly HashSet<string> ReservedBlockProperties = new(StringComparer.Ordinal)
    {
        "cadHandle", "cadEntityType", "bloco", "rotacao", "escalaX", "escalaY", "cadElevation",
        "projectedTo2D", "posicaoPeloSimbolo", DiameterProperty, MaterialProperty,
    };

    private const string DiameterProperty = "diametroMm";
    private const string MaterialProperty = "material";

    // Limites do validador do GeoMap para properties.
    private const int MaximumProperties = 128;
    private const int MaximumPropertyStringLength = 4_096;

    private static Dictionary<string, object> BlockProperties(
        GeoMapExportFeature feature,
        GeoMapBlockInfo block,
        GeoMapLayerAttributes layerAttributes)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["cadHandle"] = feature.CadHandle,
            ["cadEntityType"] = "BlockReference",
            ["bloco"] = block.Name,
            ["rotacao"] = block.RotationDegrees,
            ["escalaX"] = block.ScaleX,
            ["escalaY"] = block.ScaleY,
            ["cadElevation"] = feature.Elevation,
            ["projectedTo2D"] = feature.ProjectedTo2D,
            ["posicaoPeloSimbolo"] = block.PositionFromSymbol,
        };
        if (layerAttributes.DiameterMm is { } diameter)
        {
            properties[DiameterProperty] = diameter;
        }

        if (layerAttributes.Material is { } material)
        {
            properties[MaterialProperty] = material;
        }

        foreach (var attribute in block.Attributes ?? [])
        {
            properties[BlockAttributeName(attribute.Key)] = attribute.Value;
        }

        return properties;
    }

    private static string BlockAttributeName(string tag)
    {
        var name = tag.Trim();
        return ReservedBlockProperties.Contains(name) ? "atributo_" + name : name;
    }

    private static void ValidateLine(GeoMapExportFeature feature)
    {
        if (feature.EntityType is not (GeoMapEntityTypes.Polyline or GeoMapEntityTypes.Line or GeoMapEntityTypes.Arc))
        {
            throw new InvalidOperationException(
                $"O tipo {feature.EntityType} ({feature.CadHandle}) não é exportado para o GeoMap.");
        }

        if (feature.EntityType == GeoMapEntityTypes.Line &&
            (feature.Vertices.Count != 2 || feature.IsClosed || feature.Bulges is not null ||
             feature.EndElevation is not { } end || !IsFinite(end)))
        {
            throw new InvalidOperationException(
                $"A Line {feature.CadHandle} precisa ter dois pontos, sem arco, e as duas elevações.");
        }

        if (feature.Bulges is not null &&
            (feature.Bulges.Count != feature.Vertices.Count || feature.Bulges.Any(bulge => !IsFinite(bulge))))
        {
            throw new InvalidOperationException(
                $"A {feature.EntityType} {feature.CadHandle} precisa de um bulge finito por vértice.");
        }
    }

    private static void ValidateBlock(GeoMapExportFeature feature, GeoMapBlockInfo block)
    {
        Required(block.Name, 255, $"O nome do bloco {feature.CadHandle}");
        if (feature.Vertices is null || feature.Vertices.Count != 1)
        {
            throw new InvalidOperationException($"O bloco {feature.CadHandle} precisa ter exatamente uma posição.");
        }

        var attributes = block.Attributes ?? [];
        if (attributes.Any(attribute =>
                string.IsNullOrWhiteSpace(attribute.Key) ||
                BlockAttributeName(attribute.Key).Length > 128 ||
                attribute.Value is null ||
                attribute.Value.Length > MaximumPropertyStringLength) ||
            attributes.Select(attribute => BlockAttributeName(attribute.Key)).Distinct(StringComparer.Ordinal).Count() +
            ReservedBlockProperties.Count > MaximumProperties)
        {
            throw new InvalidOperationException(
                $"O bloco {feature.CadHandle} tem atributo vazio, longo demais (mais de {MaximumPropertyStringLength} " +
                $"caracteres) ou atributos demais para o contrato GeoMap.");
        }

        if (!IsFinite(feature.Elevation) ||
            !IsFinite(feature.Vertices[0].X) ||
            !IsFinite(feature.Vertices[0].Y) ||
            !IsFinite(block.RotationDegrees) ||
            !IsFinite(block.ScaleX) ||
            !IsFinite(block.ScaleY))
        {
            throw new InvalidOperationException($"O bloco {feature.CadHandle} contém valores não finitos.");
        }
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void Required(string? value, int maximumLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} é obrigatório.");
        }

        if (value.Length > maximumLength)
        {
            throw new InvalidOperationException($"{label} excede {maximumLength} caracteres.");
        }
    }

    private sealed record DocumentDto(
        [property: JsonPropertyName("contractVersion")] string ContractVersion,
        [property: JsonPropertyName("sourceSystem")] string SourceSystem,
        [property: JsonPropertyName("sourceCrs")] string SourceCrs,
        [property: JsonPropertyName("units")] string Units,
        [property: JsonPropertyName("project")] ProjectDto Project,
        [property: JsonPropertyName("network")] NetworkDto Network,
        [property: JsonPropertyName("exportedAt")] string ExportedAt,
        [property: JsonPropertyName("features")] IReadOnlyList<FeatureDto> Features);

    private sealed record ProjectDto(
        [property: JsonPropertyName("externalId")] string ExternalId,
        [property: JsonPropertyName("name")] string Name);

    private sealed record NetworkDto(
        [property: JsonPropertyName("externalId")] string ExternalId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("color"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Color);

    private sealed record FeatureDto(
        [property: JsonPropertyName("sourceId")] string SourceId,
        [property: JsonPropertyName("sourceType")] string SourceType,
        [property: JsonPropertyName("sourceLayer")] string SourceLayer,
        [property: JsonPropertyName("geometry")] GeometryDto Geometry,
        [property: JsonPropertyName("properties")] object Properties);

    // object: LineString usa uma lista de posições e Point uma posição só; o System.Text.Json
    // serializa o tipo real.
    private sealed record GeometryDto(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("coordinates")] object Coordinates);

    private sealed record PropertiesDto(
        [property: JsonPropertyName("cadHandle")] string CadHandle,
        [property: JsonPropertyName("cadEntityType")] string CadEntityType,
        [property: JsonPropertyName("vertexCount")] int VertexCount,
        [property: JsonPropertyName("closed")] bool Closed,
        [property: JsonPropertyName("cadElevation")] double CadElevation,
        [property: JsonPropertyName("projectedTo2D")] bool ProjectedTo2D,
        [property: JsonPropertyName("arcosTesselados"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ArcosTesselados,
        [property: JsonPropertyName(DiameterProperty), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DiametroMm,
        [property: JsonPropertyName(MaterialProperty), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Material,
        [property: JsonPropertyName("quantidadeMedicoesProfundidade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? QuantidadeMedicoesProfundidade,
        [property: JsonPropertyName("profundidadeInicialM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ProfundidadeInicialM,
        [property: JsonPropertyName("profundidadeFinalM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ProfundidadeFinalM,
        [property: JsonPropertyName("medicoesProfundidade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DepthMeasurementDto>? MedicoesProfundidade,
        [property: JsonPropertyName("profundidadeOrigem"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProfundidadeOrigem,
        [property: JsonPropertyName("profundidadeReferencia"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProfundidadeReferencia);

    // LINE: as mesmas propriedades da ponte DXF, com a elevação de cada ponta (M4.3d).
    private sealed record LinePropertiesDto(
        [property: JsonPropertyName("cadHandle")] string CadHandle,
        [property: JsonPropertyName("cadEntityType")] string CadEntityType,
        [property: JsonPropertyName("vertexCount")] int VertexCount,
        [property: JsonPropertyName("cadStartElevation")] double CadStartElevation,
        [property: JsonPropertyName("cadEndElevation")] double CadEndElevation,
        [property: JsonPropertyName("projectedTo2D")] bool ProjectedTo2D,
        [property: JsonPropertyName(DiameterProperty), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? DiametroMm,
        [property: JsonPropertyName(MaterialProperty), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Material,
        [property: JsonPropertyName("quantidadeMedicoesProfundidade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? QuantidadeMedicoesProfundidade,
        [property: JsonPropertyName("profundidadeInicialM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ProfundidadeInicialM,
        [property: JsonPropertyName("profundidadeFinalM"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? ProfundidadeFinalM,
        [property: JsonPropertyName("medicoesProfundidade"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DepthMeasurementDto>? MedicoesProfundidade,
        [property: JsonPropertyName("profundidadeOrigem"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProfundidadeOrigem,
        [property: JsonPropertyName("profundidadeReferencia"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProfundidadeReferencia);

    // Uma medição de profundidade (pontos _TG): ponto, valor, onde está na linha e o código original.
    private sealed record DepthMeasurementDto(
        [property: JsonPropertyName("ponto")] string Ponto,
        [property: JsonPropertyName("valorM")] double ValorM,
        [property: JsonPropertyName("vertice"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Vertice,
        [property: JsonPropertyName("distanciaInicioM")] double DistanciaInicioM,
        [property: JsonPropertyName("codigo")] string Codigo,
        [property: JsonPropertyName("metodo")] string Metodo,
        [property: JsonPropertyName("compartilhada"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Compartilhada);
}

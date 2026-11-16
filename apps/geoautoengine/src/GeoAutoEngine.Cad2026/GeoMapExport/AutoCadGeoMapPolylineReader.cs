using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Cad2026.GeoMapExport;

/// <summary>
/// Resultado da leitura: as feições prontas para exportar (Polylines e blocos), os códigos que ainda
/// precisam ser gravados, a cor de cada layer e os blocos deixados de fora, por motivo.
/// </summary>
public sealed record GeoMapPolylineReadResult(
    IReadOnlyList<GeoMapExportFeature> Features,
    IReadOnlyList<GeoMapFeatureIdWrite> PendingWrites,
    IReadOnlyDictionary<string, string?> LayerColors,
    IReadOnlyDictionary<string, int> SkippedBlocks)
{
    public int AssignedCount => PendingWrites.Count(write => write.Assignment.Status == GeoMapFeatureIdStatus.Assigned);

    public int RenewedCount => PendingWrites.Count(write => write.Assignment.Status == GeoMapFeatureIdStatus.Renewed);
}

public sealed record GeoMapFeatureIdWrite(ObjectId ObjectId, GeoMapFeatureIdAssignment Assignment);

/// <summary>
/// Lê da seleção, para o contrato GeoMap: Polylines 2D, inclusive com arcos, LINE e ARC (linhas; arcos desde o
/// M4.3d) e blocos (pontos, desde o M4.3c).
/// </summary>
public static class AutoCadGeoMapPolylineReader
{
    private const double Tolerance = 1e-10;

    public static GeoMapPolylineReadResult Read(
        Document document,
        IReadOnlyCollection<ObjectId> objectIds)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (objectIds is null || objectIds.Count == 0)
        {
            throw new InvalidOperationException("Selecione pelo menos uma Polyline 2D ou bloco.");
        }

        var items = new List<ReadItem>(objectIds.Count);
        var layerColors = new Dictionary<string, string?>(StringComparer.Ordinal);
        var skippedBlocks = new Dictionary<string, int>(StringComparer.Ordinal);
        var definitionExtents = new Dictionary<ObjectId, GeoMapBlockDefinitionExtents?>();
        using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var objectId in objectIds)
            {
                var entity = transaction.GetObject(objectId, OpenMode.ForRead);
                ReadItem? item = entity switch
                {
                    Polyline polyline => ReadPolyline(objectId, polyline),
                    Line line => ReadLine(objectId, line),
                    Arc arc => ReadArc(objectId, arc),
                    BlockReference block => ReadBlock(transaction, objectId, block, definitionExtents, skippedBlocks),
                    _ => throw new InvalidOperationException(
                        "A seleção contém uma entidade que não é Polyline 2D, LINE, ARC nem bloco."),
                };
                if (item is null)
                {
                    continue;
                }

                if (!layerColors.ContainsKey(item.Layer) &&
                    transaction.GetObject(((Entity)entity).LayerId, OpenMode.ForRead) is LayerTableRecord layer)
                {
                    layerColors[item.Layer] = LayerColor(layer.Color);
                }

                items.Add(item);
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma entidade da seleção pode ir para o GeoMap. Blocos deixados de fora: " +
                GeoMapBlockRules.Describe(skippedBlocks) + ".");
        }

        // Código próprio de cada feição (DEC-ECO-0003): mantém o que existe e resolve cópias/BREAK.
        var assignments = GeoMapFeatureIdentity.Resolve(
            items.Select(item => new GeoMapFeatureIdCandidate(item.Handle, item.ExistingId)).ToArray(),
            Guid.NewGuid);

        var features = new List<GeoMapExportFeature>(items.Count);
        var pendingWrites = new List<GeoMapFeatureIdWrite>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var assignment = assignments[index];
            features.Add(new GeoMapExportFeature(
                assignment.FeatureId.ToString("D"),
                item.Handle,
                item.Layer,
                item.Vertices,
                item.Closed,
                item.Elevation,
                item.ProjectedTo2D,
                item.Block,
                item.Bulges,
                item.EntityType,
                item.EndElevation));
            if (assignment.MustBeWritten)
            {
                pendingWrites.Add(new GeoMapFeatureIdWrite(item.Id, assignment));
            }
        }

        return new GeoMapPolylineReadResult(features, pendingWrites, layerColors, skippedBlocks);
    }

    private static ReadItem ReadPolyline(ObjectId objectId, Polyline polyline)
    {
        var handle = polyline.Handle.ToString();
        if (polyline.NumberOfVertices < 2)
        {
            throw new InvalidOperationException($"A Polyline {handle} possui menos de dois vértices.");
        }

        var bulges = Bulges(polyline, handle);
        var vertices = new List<GeoMapCoordinate>(polyline.NumberOfVertices);
        var projectedTo2D = false;
        for (var index = 0; index < polyline.NumberOfVertices; index++)
        {
            var point = polyline.GetPoint3dAt(index);
            vertices.Add(new GeoMapCoordinate(point.X, point.Y));
            projectedTo2D |= Math.Abs(point.Z) > Tolerance;
        }

        return new ReadItem(
            objectId,
            handle,
            ReadFeatureId(polyline),
            polyline.Layer,
            vertices,
            polyline.Closed,
            polyline.Elevation,
            projectedTo2D,
            null,
            bulges,
            GeoMapEntityTypes.Polyline,
            null);
    }

    // LINE (M4.3d): dois pontos, cada um com a sua elevação, como na ponte DXF.
    private static ReadItem ReadLine(ObjectId objectId, Line line)
    {
        var start = line.StartPoint;
        var end = line.EndPoint;
        return new ReadItem(
            objectId,
            line.Handle.ToString(),
            ReadFeatureId(line),
            line.Layer,
            [new GeoMapCoordinate(start.X, start.Y), new GeoMapCoordinate(end.X, end.Y)],
            false,
            start.Z,
            Math.Abs(start.Z) > Tolerance || Math.Abs(end.Z) > Tolerance,
            null,
            null,
            GeoMapEntityTypes.Line,
            end.Z);
    }

    // ARC (M4.3d): vira uma linha de dois pontos com um arco, que a serialização troca por trechos retos.
    private static ReadItem ReadArc(ObjectId objectId, Arc arc)
    {
        var handle = arc.Handle.ToString();
        var normalPointsDown = PlaneDirection(arc.Normal.Z, $"O ARC {handle}");
        var start = arc.StartPoint;
        var end = arc.EndPoint;
        return new ReadItem(
            objectId,
            handle,
            ReadFeatureId(arc),
            arc.Layer,
            [new GeoMapCoordinate(start.X, start.Y), new GeoMapCoordinate(end.X, end.Y)],
            false,
            arc.Center.Z,
            Math.Abs(arc.Center.Z) > Tolerance,
            null,
            [GeoMapArcTessellation.ArcBulge(arc.StartAngle, arc.EndAngle, normalPointsDown), 0],
            GeoMapEntityTypes.Arc,
            null);
    }

    // Bulges da Polyline no sentido visto de cima (WCS); null quando todos os segmentos são retos.
    private static List<double>? Bulges(Polyline polyline, string handle)
    {
        var bulges = new List<double>(polyline.NumberOfVertices);
        for (var index = 0; index < polyline.NumberOfVertices; index++)
        {
            bulges.Add(polyline.GetBulgeAt(index));
        }

        if (!bulges.Any(GeoMapArcTessellation.IsArc))
        {
            return null;
        }

        // Polyline espelhada (normal -Z): o sentido do arco se inverte quando visto de cima.
        return PlaneDirection(polyline.Normal.Z, $"A Polyline {handle}")
            ? bulges.Select(bulge => -bulge).ToList()
            : bulges;
    }

    // Arcos só no plano XY: normal +Z ou -Z (entidade espelhada). Retorna true para -Z.
    private static bool PlaneDirection(double normalZ, string entity)
    {
        if (Math.Abs(Math.Abs(normalZ) - 1) > 1e-9)
        {
            throw new InvalidOperationException(
                $"{entity} tem arco fora do plano XY (normal diferente de Z). Exportação cancelada.");
        }

        return normalZ < 0;
    }

    // Bloco como ponto (M4.3c), com as regras da ponte DXF (DEC-029 do GeoMap). Retorna null quando o
    // bloco fica de fora; o motivo é contado em skipped.
    private static ReadItem? ReadBlock(
        Transaction transaction,
        ObjectId objectId,
        BlockReference block,
        Dictionary<ObjectId, GeoMapBlockDefinitionExtents?> definitionExtents,
        Dictionary<string, int> skipped)
    {
        var handle = block.Handle.ToString();
        if (block is MInsertBlock)
        {
            Count(skipped, "MINSERT");
            return null;
        }

        var definition = (BlockTableRecord)transaction.GetObject(block.BlockTableRecord, OpenMode.ForRead);
        if (definition.IsFromExternalReference || definition.IsFromOverlayReference)
        {
            Count(skipped, "referência externa (xref)");
            return null;
        }

        // Bloco dinâmico: a referência aponta para uma definição anônima (*U…); o nome que diz o que
        // o elemento é fica na definição dinâmica.
        var name = block.IsDynamicBlock
            ? ((BlockTableRecord)transaction.GetObject(block.DynamicBlockTableRecord, OpenMode.ForRead)).Name
            : definition.Name;
        var reason = GeoMapBlockRules.SkipReason(block.Layer, name);
        if (reason is not null)
        {
            Count(skipped, reason);
            return null;
        }

        if (Math.Abs(block.Normal.Z - 1) > 1e-9)
        {
            throw new InvalidOperationException(
                $"O bloco {handle} não está no plano XY (normal diferente de Z). Exportação cancelada.");
        }

        if (!definitionExtents.TryGetValue(definition.ObjectId, out var extents))
        {
            extents = DefinitionExtents(transaction, definition);
            definitionExtents[definition.ObjectId] = extents;
        }

        var rotation = GeoMapBlockRules.RotationDegrees(block.Rotation);
        var scale = block.ScaleFactors;
        var insertion = block.Position;
        var position = GeoMapBlockRules.Resolve(
            new GeoMapCoordinate(insertion.X, insertion.Y),
            extents,
            rotation,
            scale.X,
            scale.Y);
        return new ReadItem(
            objectId,
            handle,
            ReadFeatureId(block),
            block.Layer,
            [position.Position],
            false,
            insertion.Z,
            Math.Abs(insertion.Z) > Tolerance,
            new GeoMapBlockInfo(name, rotation, scale.X, scale.Y, position.FromSymbol, BlockAttributes(transaction, block)),
            null,
            GeoMapEntityTypes.Polyline,
            null);
    }

    // Atributos (ATTRIB) do bloco, tag e valor, como a ponte DXF (M4.3e). Atributo multilinha vai como texto simples.
    private static List<KeyValuePair<string, string>> BlockAttributes(Transaction transaction, BlockReference block)
    {
        var attributes = new List<KeyValuePair<string, string>>();
        foreach (ObjectId attributeId in block.AttributeCollection)
        {
            if (attributeId.IsErased ||
                transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute ||
                string.IsNullOrWhiteSpace(attribute.Tag))
            {
                continue;
            }

            string text;
            if (attribute.IsMTextAttribute)
            {
                using var mtext = attribute.MTextAttribute;
                text = mtext.Text;
            }
            else
            {
                text = attribute.TextString;
            }

            attributes.Add(new KeyValuePair<string, string>(attribute.Tag.Trim(), (text ?? string.Empty).Trim()));
        }

        return attributes;
    }

    // Extensão do desenho da definição, sem textos e definições de atributo (como a ponte DXF).
    private static GeoMapBlockDefinitionExtents? DefinitionExtents(Transaction transaction, BlockTableRecord definition)
    {
        double? minX = null, minY = null, maxX = null, maxY = null;
        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not Entity entity ||
                entity is AttributeDefinition or DBText or MText ||
                entity.Bounds is not { } bounds)
            {
                continue;
            }

            minX = Math.Min(minX ?? double.MaxValue, bounds.MinPoint.X);
            minY = Math.Min(minY ?? double.MaxValue, bounds.MinPoint.Y);
            maxX = Math.Max(maxX ?? double.MinValue, bounds.MaxPoint.X);
            maxY = Math.Max(maxY ?? double.MinValue, bounds.MaxPoint.Y);
        }

        return minX is null
            ? null
            : new GeoMapBlockDefinitionExtents(
                definition.Origin.X,
                definition.Origin.Y,
                minX.Value,
                minY!.Value,
                maxX!.Value,
                maxY!.Value);
    }

    private static void Count(Dictionary<string, int> skipped, string reason) =>
        skipped[reason] = skipped.TryGetValue(reason, out var count) ? count + 1 : 1;

    /// <summary>
    /// Grava na XData das entidades os códigos novos ou renovados. Altera o desenho: o usuário
    /// precisa salvar o DWG para que os códigos permaneçam.
    /// </summary>
    public static int WriteFeatureIds(Document document, IReadOnlyList<GeoMapFeatureIdWrite> writes)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (writes is null || writes.Count == 0)
        {
            return 0;
        }

        var database = document.Database;
        using var transaction = database.TransactionManager.StartTransaction();
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (!table.Has(GeoMapFeatureIdentity.RegisteredApplicationName))
        {
            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = GeoMapFeatureIdentity.RegisteredApplicationName };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        foreach (var write in writes)
        {
            var entity = (Entity)transaction.GetObject(write.ObjectId, OpenMode.ForWrite);
            var values = new List<TypedValue>
            {
                new((int)DxfCode.ExtendedDataRegAppName, GeoMapFeatureIdentity.RegisteredApplicationName),
            };
            values.AddRange(GeoMapFeatureIdentity.XDataValues(write.Assignment.FeatureId)
                .Select(value => new TypedValue((int)DxfCode.ExtendedDataAsciiString, value)));
            using var buffer = new ResultBuffer(values.ToArray());
            entity.XData = buffer;
        }

        transaction.Commit();
        return writes.Count;
    }

    // Cor da layer em #RRGGBB para o contrato GeoMap 0.2 (M4.3b). True color e ACI usam a conversão
    // do próprio AutoCAD; o ACI 7 (branco/preto conforme o fundo) vira preto, como na ponte DXF.
    private static string? LayerColor(Color color)
    {
        if (color.IsByAci && color.ColorIndex == GeoMapLayerColor.AciForegroundIndex)
        {
            return GeoMapLayerColor.ForegroundColor;
        }

        if (color.IsByAci || color.IsByColor)
        {
            var value = color.ColorValue;
            return GeoMapLayerColor.ToHex(value.R, value.G, value.B);
        }

        return null;
    }

    private sealed record ReadItem(
        ObjectId Id,
        string Handle,
        Guid? ExistingId,
        string Layer,
        List<GeoMapCoordinate> Vertices,
        bool Closed,
        double Elevation,
        bool ProjectedTo2D,
        GeoMapBlockInfo? Block,
        List<double>? Bulges,
        string EntityType,
        double? EndElevation);

    private static Guid? ReadFeatureId(Entity entity)
    {
        using var data = entity.GetXDataForApplication(GeoMapFeatureIdentity.RegisteredApplicationName);
        if (data is null)
        {
            return null;
        }

        return GeoMapFeatureIdentity.ParseXData(data.AsArray()
            .Where(item => item.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(item => Convert.ToString(item.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
    }
}

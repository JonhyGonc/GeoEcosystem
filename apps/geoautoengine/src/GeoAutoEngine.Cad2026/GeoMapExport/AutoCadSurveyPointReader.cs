using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Cad2026.GeoMapExport;

/// <summary>Pontos do levantamento lidos e quantos tiveram a posição tirada da inserção do bloco.</summary>
public sealed record SurveyPointReadResult(
    IReadOnlyList<SurveyPointInput> Points,
    int FromBlockPoint,
    int FromInsertion,
    int FromFirstOfMany,
    int WithoutDescription);

/// <summary>
/// Lê os blocos de pontos do levantamento (layers <c>_TG*</c>, normalmente <c>SIMB_PONTO</c>), só leitura.
/// A posição de cada ponto é onde o OSNAP NODE prende: o ponto (DBPoint) de dentro do bloco, levado para o desenho
/// pela transformação do bloco (escala, rotação e posição). A inserção do bloco só é usada quando o bloco não tem
/// ponto, e isso fica marcado no diagnóstico.
/// </summary>
public static class AutoCadSurveyPointReader
{
    public const string NameTag = "PONTO";
    public const string DescriptionTag = "DESC";
    public const string ElevationTag = "COTA";

    public static SurveyPointReadResult Read(Database database, IReadOnlyCollection<ObjectId> objectIds)
    {
        if (database is null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        if (objectIds is null)
        {
            throw new ArgumentNullException(nameof(objectIds));
        }

        var points = new List<SurveyPointInput>(objectIds.Count);
        var nodesByDefinition = new Dictionary<ObjectId, Point3d[]>();
        int fromBlockPoint = 0, fromInsertion = 0, fromFirstOfMany = 0, withoutDescription = 0;
        using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var objectId in objectIds)
            {
                if (transaction.GetObject(objectId, OpenMode.ForRead) is not BlockReference block ||
                    block is MInsertBlock)
                {
                    continue;
                }

                if (!nodesByDefinition.TryGetValue(block.BlockTableRecord, out var nodes))
                {
                    nodes = DefinitionNodes(transaction, block.BlockTableRecord);
                    nodesByDefinition[block.BlockTableRecord] = nodes;
                }

                Point3d node;
                string source;
                if (nodes.Length == 0)
                {
                    node = block.Position;
                    source = SurveyNodeSources.Insertion;
                    fromInsertion++;
                }
                else
                {
                    node = nodes[0].TransformBy(block.BlockTransform);
                    source = nodes.Length == 1 ? SurveyNodeSources.BlockPoint : SurveyNodeSources.FirstOfManyBlockPoints;
                    if (nodes.Length == 1)
                    {
                        fromBlockPoint++;
                    }
                    else
                    {
                        fromFirstOfMany++;
                    }
                }

                var attributes = Attributes(transaction, block);
                var description = Attribute(attributes, DescriptionTag);
                if (description.Length == 0)
                {
                    withoutDescription++;
                }

                var handle = block.Handle.ToString();
                var name = Attribute(attributes, NameTag);
                points.Add(new SurveyPointInput(
                    handle,
                    name.Length == 0 ? handle : name,
                    description,
                    Attribute(attributes, ElevationTag),
                    new GeoMapCoordinate(node.X, node.Y),
                    source));
            }
        }

        return new SurveyPointReadResult(points, fromBlockPoint, fromInsertion, fromFirstOfMany, withoutDescription);
    }

    // Pontos (DBPoint) diretamente dentro da definição do bloco, nas coordenadas do bloco.
    private static Point3d[] DefinitionNodes(Transaction transaction, ObjectId definitionId)
    {
        var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
        var nodes = new List<Point3d>();
        foreach (ObjectId id in definition)
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is DBPoint point)
            {
                nodes.Add(point.Position);
            }
        }

        return nodes.ToArray();
    }

    private static Dictionary<string, string> Attributes(Transaction transaction, BlockReference block)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectId attributeId in block.AttributeCollection)
        {
            if (attributeId.IsErased ||
                transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute ||
                string.IsNullOrWhiteSpace(attribute.Tag))
            {
                continue;
            }

            var tag = attribute.Tag.Trim();
            if (!values.ContainsKey(tag))
            {
                values[tag] = (attribute.TextString ?? string.Empty).Trim();
            }
        }

        return values;
    }

    private static string Attribute(Dictionary<string, string> attributes, string tag) =>
        attributes.TryGetValue(tag, out var value) ? value : string.Empty;
}

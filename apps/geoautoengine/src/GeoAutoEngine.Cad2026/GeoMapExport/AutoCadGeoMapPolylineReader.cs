using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.GeoMapExport;

namespace GeoAutoEngine.Cad2026.GeoMapExport;

/// <summary>Resultado da leitura: as feições prontas para exportar e os códigos que ainda precisam ser gravados.</summary>
public sealed record GeoMapPolylineReadResult(
    IReadOnlyList<GeoMapExportFeature> Features,
    IReadOnlyList<GeoMapFeatureIdWrite> PendingWrites)
{
    public int AssignedCount => PendingWrites.Count(write => write.Assignment.Status == GeoMapFeatureIdStatus.Assigned);

    public int RenewedCount => PendingWrites.Count(write => write.Assignment.Status == GeoMapFeatureIdStatus.Renewed);
}

public sealed record GeoMapFeatureIdWrite(ObjectId ObjectId, GeoMapFeatureIdAssignment Assignment);

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
            throw new InvalidOperationException("Selecione pelo menos uma Polyline 2D.");
        }

        var polylines = new List<ReadPolyline>(objectIds.Count);
        using (var transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
        {
            foreach (var objectId in objectIds)
            {
                if (transaction.GetObject(objectId, OpenMode.ForRead) is not Polyline polyline)
                {
                    throw new InvalidOperationException("A seleção contém uma entidade que não é Polyline 2D.");
                }

                var handle = polyline.Handle.ToString();
                if (polyline.NumberOfVertices < 2)
                {
                    throw new InvalidOperationException($"A Polyline {handle} possui menos de dois vértices.");
                }

                RejectCurvedSegments(polyline, handle);

                var vertices = new List<GeoMapCoordinate>(polyline.NumberOfVertices);
                var projectedTo2D = false;
                for (var index = 0; index < polyline.NumberOfVertices; index++)
                {
                    var point = polyline.GetPoint3dAt(index);
                    vertices.Add(new GeoMapCoordinate(point.X, point.Y));
                    projectedTo2D |= Math.Abs(point.Z) > Tolerance;
                }

                polylines.Add(new ReadPolyline(
                    objectId,
                    handle,
                    ReadFeatureId(polyline),
                    polyline.Layer,
                    vertices,
                    polyline.Closed,
                    polyline.Elevation,
                    projectedTo2D));
            }
        }

        // Código próprio de cada feição (DEC-ECO-0003): mantém o que existe e resolve cópias/BREAK.
        var assignments = GeoMapFeatureIdentity.Resolve(
            polylines.Select(item => new GeoMapFeatureIdCandidate(item.Handle, item.ExistingId)).ToArray(),
            Guid.NewGuid);

        var features = new List<GeoMapExportFeature>(polylines.Count);
        var pendingWrites = new List<GeoMapFeatureIdWrite>();
        for (var index = 0; index < polylines.Count; index++)
        {
            var item = polylines[index];
            var assignment = assignments[index];
            features.Add(new GeoMapExportFeature(
                assignment.FeatureId.ToString("D"),
                item.Handle,
                item.Layer,
                item.Vertices,
                item.Closed,
                item.Elevation,
                item.ProjectedTo2D));
            if (assignment.MustBeWritten)
            {
                pendingWrites.Add(new GeoMapFeatureIdWrite(item.Id, assignment));
            }
        }

        return new GeoMapPolylineReadResult(features, pendingWrites);
    }

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

    private sealed record ReadPolyline(
        ObjectId Id,
        string Handle,
        Guid? ExistingId,
        string Layer,
        List<GeoMapCoordinate> Vertices,
        bool Closed,
        double Elevation,
        bool ProjectedTo2D);

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

    private static void RejectCurvedSegments(Polyline polyline, string handle)
    {
        var segmentCount = polyline.Closed
            ? polyline.NumberOfVertices
            : polyline.NumberOfVertices - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            if (Math.Abs(polyline.GetBulgeAt(index)) > Tolerance)
            {
                throw new InvalidOperationException(
                    $"A Polyline {handle} contém arco. O exportador GeoMap v0.1 aceita somente segmentos retos.");
            }
        }
    }
}

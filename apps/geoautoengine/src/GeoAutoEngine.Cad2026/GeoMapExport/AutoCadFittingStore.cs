using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using GeoAutoEngine.Application.GeoMapExport;
using HostMapApplicationServices = Autodesk.Gis.Map.HostMapApplicationServices;
using MapDataType = Autodesk.Gis.Map.Constants.DataType;
using MapOpenMode = Autodesk.Gis.Map.Constants.OpenMode;
using OdRecord = Autodesk.Gis.Map.ObjectData.Record;
using OdTable = Autodesk.Gis.Map.ObjectData.Table;

namespace GeoAutoEngine.Cad2026.GeoMapExport;

/// <summary>
/// Tipo das peças gravado nos blocos (pedido do usuário, 19/11/2026), do mesmo jeito das profundidades:
/// tabela Object Data <see cref="TableName"/> (TIPO e ORIGEM, visível e editável na paleta Propriedades) e cópia
/// escondida do tipo que o plugin gravou (XRecord <see cref="SnapshotKey"/>), para proteger a correção feita à mão.
/// </summary>
public static class AutoCadFittingStore
{
    public const string TableName = "GEO_PECA";
    public const string SnapshotKey = "GEOAUTOENGINE_FITTING";

    public const string TypeField = "TIPO";
    public const string OriginField = "ORIGEM";

    public static Dictionary<string, FittingStoredState> Read(Database database, IEnumerable<string> handles)
    {
        if (database is null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        if (handles is null)
        {
            throw new ArgumentNullException(nameof(handles));
        }

        var result = new Dictionary<string, FittingStoredState>(StringComparer.Ordinal);
        var tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
        using var table = tables.IsTableDefined(TableName) ? tables[TableName] : null;
        using var transaction = database.TransactionManager.StartTransaction();
        foreach (var handle in handles.Distinct(StringComparer.Ordinal))
        {
            if (!TryGetObjectId(database, handle, out var objectId))
            {
                continue;
            }

            var values = table is null ? null : ReadValues(table, objectId);
            var written = ReadSnapshot(transaction, objectId);
            if (values is not null || written is not null)
            {
                result[handle] = new FittingStoredState(
                    values?.GetValueOrDefault(TypeField),
                    values?.GetValueOrDefault(OriginField),
                    written);
            }
        }

        transaction.Commit();
        return result;
    }

    public static int Write(Database database, FittingWritePlan plan)
    {
        if (database is null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        var writes = plan.Writes;
        if (writes.Count == 0)
        {
            return 0;
        }

        using var table = Step("ao criar a tabela " + TableName, EnsureTable);
        using var transaction = database.TransactionManager.StartTransaction();
        var written = 0;
        foreach (var item in writes)
        {
            if (!TryGetObjectId(database, item.Handle, out var objectId) || item.Type is not { } type)
            {
                continue;
            }

            var origin = item.Action == FittingWriteAction.MarkManual ? PipeDepthOrigins.Manual : PipeDepthOrigins.Automatic;
            Step($"ao gravar o tipo na peça {item.Handle}", () => WriteValues(table, objectId, type, origin));
            if (item.Action != FittingWriteAction.MarkManual)
            {
                Step($"ao gravar a cópia escondida na peça {item.Handle}", () => WriteSnapshot(transaction, objectId, type));
            }

            written++;
        }

        transaction.Commit();
        return written;
    }

    private static T Step<T>(string step, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (System.Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Falhou {step}: {exception.GetType().Name} {exception.Message}", exception);
        }
    }

    private static void Step(string step, Action action) => Step(step, () =>
    {
        action();
        return true;
    });

    private static OdTable EnsureTable()
    {
        var project = HostMapApplicationServices.Application.ActiveProject;
        var tables = project.ODTables;
        if (!tables.IsTableDefined(TableName))
        {
            var definitions = project.MapUtility.NewODFieldDefinitions();
            definitions.Add(TypeField, "Tipo da peca", MapDataType.Character, 0);
            definitions.Add(OriginField, "Origem do valor", MapDataType.Character, 1);
            tables.Add(TableName, definitions, "Pecas GeoAutoEngine", true);
        }

        return tables[TableName];
    }

    private static Dictionary<string, string>? ReadValues(OdTable table, ObjectId objectId)
    {
        using var records = table.GetObjectTableRecords(0, objectId, MapOpenMode.OpenForRead, false);

        // No Map 3D, percorrer uma lista vazia dá erro (MapException em Current): conferir a quantidade antes.
        if (records.Count == 0)
        {
            return null;
        }

        var definitions = table.FieldDefinitions;
        foreach (OdRecord record in records)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < record.Count; index++)
            {
                var value = record[index];
                values[definitions[index].Name] = value.Type == MapDataType.Character ? value.StrValue ?? string.Empty : string.Empty;
            }

            return values;
        }

        return null;
    }

    private static void WriteValues(OdTable table, ObjectId objectId, string type, string origin)
    {
        using (var records = table.GetObjectTableRecords(0, objectId, MapOpenMode.OpenForWrite, false))
        {
            if (records.Count > 0)
            {
                foreach (OdRecord record in records)
                {
                    Assign(table, record, type, origin);
                    records.UpdateRecord(record);
                    return;
                }
            }
        }

        using var created = OdRecord.Create();
        table.InitRecord(created);
        Assign(table, created, type, origin);
        table.AddRecord(created, objectId);
    }

    private static void Assign(OdTable table, OdRecord record, string type, string origin)
    {
        var definitions = table.FieldDefinitions;
        for (var index = 0; index < record.Count; index++)
        {
            var name = definitions[index].Name;
            if (string.Equals(name, TypeField, StringComparison.OrdinalIgnoreCase))
            {
                record[index].Assign(type);
            }
            else if (string.Equals(name, OriginField, StringComparison.OrdinalIgnoreCase))
            {
                record[index].Assign(origin);
            }
        }
    }

    private static string? ReadSnapshot(Transaction transaction, ObjectId objectId)
    {
        var entity = transaction.GetObject(objectId, OpenMode.ForRead);
        if (entity.ExtensionDictionary.IsNull)
        {
            return null;
        }

        var dictionary = (DBDictionary)transaction.GetObject(entity.ExtensionDictionary, OpenMode.ForRead);
        if (!dictionary.Contains(SnapshotKey) ||
            transaction.GetObject(dictionary.GetAt(SnapshotKey), OpenMode.ForRead) is not Xrecord record ||
            record.Data is null)
        {
            return null;
        }

        return record.Data.AsArray()
            .Where(value => value.TypeCode == (short)DxfCode.Text)
            .Select(value => value.Value as string)
            .FirstOrDefault();
    }

    private static void WriteSnapshot(Transaction transaction, ObjectId objectId, string type)
    {
        var entity = transaction.GetObject(objectId, OpenMode.ForWrite);
        if (entity.ExtensionDictionary.IsNull)
        {
            entity.CreateExtensionDictionary();
        }

        var dictionary = (DBDictionary)transaction.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);
        var data = new ResultBuffer(new TypedValue((int)DxfCode.Text, type));
        if (dictionary.Contains(SnapshotKey))
        {
            var existing = (Xrecord)transaction.GetObject(dictionary.GetAt(SnapshotKey), OpenMode.ForWrite);
            existing.Data = data;
            return;
        }

        var record = new Xrecord { Data = data };
        dictionary.SetAt(SnapshotKey, record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static bool TryGetObjectId(Database database, string handle, out ObjectId objectId)
    {
        objectId = ObjectId.Null;
        if (!long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        return database.TryGetObjectId(new Handle(value), out objectId) && !objectId.IsErased;
    }
}

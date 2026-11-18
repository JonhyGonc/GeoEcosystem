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
/// Profundidades gravadas nas linhas do desenho (M7.2, DEC-ECO-0005):
/// <list type="bullet">
/// <item>tabela Object Data <see cref="TableName"/> do Map 3D, visível e editável na paleta Propriedades;</item>
/// <item>cópia escondida do que o plugin gravou (XRecord <see cref="SnapshotKey"/> no dicionário da linha), com o detalhe
/// de cada ponto, para saber se o usuário corrigiu os campos à mão.</item>
/// </list>
/// O código da feição (XData GEOAUTOENGINE_FEATURE) e a vídeo-inspeção não são tocados.
/// </summary>
public static class AutoCadPipeDepthStore
{
    public const string TableName = "GEO_PROFUNDIDADE";
    public const string SnapshotKey = "GEOAUTOENGINE_DEPTHS";

    public const string AverageField = "PROF_MEDIA_M";
    public const string StartField = "PROF_INICIAL_M";
    public const string EndField = "PROF_FINAL_M";
    public const string PointsField = "PONTOS";
    public const string OriginField = "ORIGEM";

    /// <summary>O que está gravado em cada linha (campos visíveis e cópia escondida). Só leitura.</summary>
    public static Dictionary<string, PipeDepthStoredState> Read(Database database, IEnumerable<string> handles)
    {
        if (database is null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        if (handles is null)
        {
            throw new ArgumentNullException(nameof(handles));
        }

        var result = new Dictionary<string, PipeDepthStoredState>(StringComparer.Ordinal);
        var tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
        using var table = tables.IsTableDefined(TableName) ? tables[TableName] : null;
        using var transaction = database.TransactionManager.StartTransaction();
        foreach (var handle in handles.Distinct(StringComparer.Ordinal))
        {
            if (!TryGetObjectId(database, handle, out var objectId))
            {
                continue;
            }

            var fields = table is null ? null : ReadFields(table, objectId);
            var snapshot = ReadSnapshot(transaction, objectId);
            if (fields is not null || snapshot is not null)
            {
                result[handle] = new PipeDepthStoredState(fields, snapshot);
            }
        }

        transaction.Commit();
        return result;
    }

    /// <summary>
    /// Grava o plano numa transação só. Se algo falhar, a transação é desfeita e o erro sobe; o comando inteiro
    /// também pode ser desfeito com U.
    /// </summary>
    public static int Write(Database database, PipeDepthWritePlan plan)
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
            if (!TryGetObjectId(database, item.Handle, out var objectId))
            {
                continue;
            }

            // Primeiro os campos (o Map abre a linha sozinho), depois a cópia escondida.
            Step($"ao gravar os campos na linha {item.Handle}", () => WriteFields(table, objectId, item.Fields!));
            if (item.Snapshot is { } snapshot)
            {
                Step($"ao gravar a cópia escondida na linha {item.Handle}", () => WriteSnapshot(transaction, objectId, snapshot));
            }

            written++;
        }

        transaction.Commit();
        return written;
    }

    // Diz em que passo a gravação parou: o erro do Map 3D (MapException) não traz detalhe nenhum.
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
            // Nomes e descrições curtos e sem acento; armazenamento como nos exemplos da Autodesk (último argumento true).
            definitions.Add(AverageField, "Profundidade media", MapDataType.Real, 0);
            definitions.Add(StartField, "Primeira profundidade", MapDataType.Character, 1);
            definitions.Add(EndField, "Ultima profundidade", MapDataType.Character, 2);
            definitions.Add(PointsField, "Pontos na media", MapDataType.Character, 3);
            definitions.Add(OriginField, "Origem do valor", MapDataType.Character, 4);
            tables.Add(TableName, definitions, "Profundidades GeoAutoEngine", true);
        }

        return tables[TableName];
    }

    private static PipeDepthFields? ReadFields(OdTable table, ObjectId objectId)
    {
        using var records = table.GetObjectTableRecords(0, objectId, MapOpenMode.OpenForRead, false);

        // No Map 3D, percorrer uma lista vazia dá erro (MapException em Current): conferir a quantidade antes.
        if (records.Count == 0)
        {
            return null;
        }

        foreach (OdRecord record in records)
        {
            var values = Values(table, record);
            return new PipeDepthFields(
                values.TryGetValue(AverageField, out var average) && PipeDepthFields.TryParse(average, out var number) && number is { } value
                    ? value
                    : 0,
                values.GetValueOrDefault(StartField, string.Empty),
                values.GetValueOrDefault(EndField, string.Empty),
                values.GetValueOrDefault(PointsField, string.Empty),
                values.GetValueOrDefault(OriginField, string.Empty));
        }

        return null;
    }

    private static void WriteFields(OdTable table, ObjectId objectId, PipeDepthFields fields)
    {
        using (var records = table.GetObjectTableRecords(0, objectId, MapOpenMode.OpenForWrite, false))
        {
            // Lista vazia: não percorrer (dá erro no Map 3D); a linha ainda não tem registro e ele é criado abaixo.
            if (records.Count > 0)
            {
                foreach (OdRecord record in records)
                {
                    Assign(table, record, fields);
                    records.UpdateRecord(record);
                    return;
                }
            }
        }

        using var created = OdRecord.Create();
        table.InitRecord(created);
        Assign(table, created, fields);
        table.AddRecord(created, objectId);
    }

    private static void Assign(OdTable table, OdRecord record, PipeDepthFields fields)
    {
        var definitions = table.FieldDefinitions;
        for (var index = 0; index < record.Count; index++)
        {
            var value = record[index];
            switch (definitions[index].Name)
            {
                case AverageField:
                    value.Assign(fields.AverageMetres);
                    break;
                case StartField:
                    value.Assign(fields.Start);
                    break;
                case EndField:
                    value.Assign(fields.End);
                    break;
                case PointsField:
                    value.Assign(fields.Points);
                    break;
                case OriginField:
                    value.Assign(fields.Origin);
                    break;
            }
        }
    }

    private static Dictionary<string, string> Values(OdTable table, OdRecord record)
    {
        var definitions = table.FieldDefinitions;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < record.Count; index++)
        {
            var value = record[index];
            values[definitions[index].Name] = value.Type switch
            {
                MapDataType.Real => value.DoubleValue.ToString("R", CultureInfo.InvariantCulture),
                MapDataType.Integer => value.Int32Value.ToString(CultureInfo.InvariantCulture),
                MapDataType.Character => value.StrValue ?? string.Empty,
                _ => string.Empty,
            };
        }

        return values;
    }

    private static PipeDepthSnapshot? ReadSnapshot(Transaction transaction, ObjectId objectId)
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

        return PipeDepthSnapshotCodec.Decode(record.Data.AsArray()
            .Where(value => value.TypeCode == (short)DxfCode.Text)
            .Select(value => value.Value as string ?? string.Empty));
    }

    private static void WriteSnapshot(Transaction transaction, ObjectId objectId, PipeDepthSnapshot snapshot)
    {
        var entity = transaction.GetObject(objectId, OpenMode.ForWrite);
        if (entity.ExtensionDictionary.IsNull)
        {
            entity.CreateExtensionDictionary();
        }

        var dictionary = (DBDictionary)transaction.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);
        var data = new ResultBuffer(PipeDepthSnapshotCodec.Encode(snapshot)
            .Select(chunk => new TypedValue((int)DxfCode.Text, chunk))
            .ToArray());
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

using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using GeoAutoEngine.Application;
using GeoAutoEngine.Application.GeoMapExport;
using GeoAutoEngine.Cad2026.GeoMapExport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(GeoAutoEngine.Plugin.GeoMapExportCommands))]

namespace GeoAutoEngine.Plugin;

public sealed class GeoMapExportCommands
{
    // Sem NoUndoMarker: desde o M4.3a o comando grava os códigos das feições no desenho (DEC-ECO-0003).
    [CommandMethod("GEOAUTO3GEOMAPEXPORT", CommandFlags.Modal)]
    public void ExportPolylines()
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var selection = SelectPolylines(document.Editor);
            if (selection is null)
            {
                document.Editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            var read = AutoCadGeoMapPolylineReader.Read(document, selection);
            // DEC-029 do GeoMap: blocos numa rede separada das linhas. Uma seleção com os dois gera
            // dois arquivos, um por rede (M4.3c, decisão do usuário de 16/11/2026).
            var lines = read.Features.Where(feature => feature.Block is null).ToArray();
            var blocks = read.Features.Where(feature => feature.Block is not null).ToArray();
            var inferredNetwork = InferNetwork(lines.Length > 0 ? lines : blocks);
            var drawingName = Path.GetFileNameWithoutExtension(document.Name);
            var editor = document.Editor;
            if (read.SkippedBlocks.Count > 0)
            {
                editor.WriteMessage(
                    $"\nGeoAutoEngine: blocos deixados de fora: {GeoMapBlockRules.Describe(read.SkippedBlocks)}.\n");
            }

            if (!TryPrompt(editor, "Identificador do projeto", drawingName, out var projectExternalId) ||
                !TryPrompt(editor, "Nome do projeto", drawingName, out var projectName))
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            // Rede das linhas e, se houver blocos, a rede dos blocos, sugerida a partir da primeira.
            var networkExternalId = $"{projectExternalId}:{inferredNetwork.NetworkId}";
            var networkName = inferredNetwork.DisplayName;
            var blockNetworkExternalId = string.Empty;
            var blockNetworkName = string.Empty;
            if ((lines.Length > 0 &&
                 (!TryPrompt(editor, "Identificador da rede", networkExternalId, out networkExternalId) ||
                  !TryPrompt(editor, "Nome da rede", networkName, out networkName))) ||
                (blocks.Length > 0 &&
                 (!TryPrompt(
                      editor,
                      "Identificador da rede dos blocos",
                      GeoMapBlockRules.BlockNetworkExternalId(networkExternalId),
                      out blockNetworkExternalId) ||
                  !TryPrompt(
                      editor,
                      "Nome da rede dos blocos",
                      GeoMapBlockRules.BlockNetworkName(networkName),
                      out blockNetworkName))) ||
                !TryPrompt(editor, "Tipo da rede", CanonicalNetworkType(inferredNetwork.NetworkId), out var networkType))
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            if (!TryPromptCrs(editor, out var sourceCrs) ||
                !TryPrompt(
                    editor,
                    "Unidade (metre, foot, us-survey-foot ou degree)",
                    DrawingUnit(document.Database),
                    out var units))
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            // Cada rede leva a cor da layer com mais feições dela (M4.3b). Os dois arquivos são montados
            // e validados antes de qualquer gravação.
            var exportedAt = DateTimeOffset.UtcNow;
            var outputs = new List<NetworkOutput>(2);
            if (lines.Length > 0)
            {
                outputs.Add(CreateOutput(
                    "linhas", lines, read, sourceCrs, units, projectExternalId, projectName,
                    networkExternalId, networkName, networkType, exportedAt));
            }

            if (blocks.Length > 0)
            {
                outputs.Add(CreateOutput(
                    "blocos", blocks, read, sourceCrs, units, projectExternalId, projectName,
                    blockNetworkExternalId, blockNetworkName, networkType, exportedAt));
            }

            var suggestedName = $"{projectExternalId}-{inferredNetwork.NetworkId}{GeoMapBlockRules.FileExtension}";
            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "geomap.json",
                FileName = SafeFileName(lines.Length > 0 ? suggestedName : GeoMapBlockRules.BlockFilePath(suggestedName)),
                Filter = "Contrato GeoMap (*.geomap.json)|*.geomap.json",
                OverwritePrompt = true,
                RestoreDirectory = true,
                Title = lines.Length > 0 && blocks.Length > 0
                    ? "Exportar para o GeoMap (os blocos vão num segundo arquivo, com -blocos no nome)"
                    : "Exportar rede para o GeoMap",
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            outputs[0] = outputs[0] with { Path = dialog.FileName };
            if (outputs.Count == 2)
            {
                outputs[1] = outputs[1] with { Path = GeoMapBlockRules.BlockFilePath(dialog.FileName) };
            }

            // Os códigos novos são gravados antes dos arquivos: se a gravação de um arquivo falhar,
            // os códigos já estão no desenho e a próxima exportação usa os mesmos (DEC-ECO-0003).
            var written = AutoCadGeoMapPolylineReader.WriteFeatureIds(document, read.PendingWrites);
            foreach (var output in outputs)
            {
                File.WriteAllText(output.Path, output.Json, new UTF8Encoding(false));
                editor.WriteMessage(
                    $"\nGeoAutoEngine: {output.Count} {output.Kind} na rede \"{output.NetworkName}\" " +
                    $"(versão {output.Version}, cor {output.Color ?? "nenhuma"}" +
                    (output.Color is null ? string.Empty : $", da layer {output.ColorLayer}") +
                    $") -> {output.Path}\n");
            }

            editor.WriteMessage(
                $"GeoAutoEngine: códigos novos={read.AssignedCount}; renovados por cópia/BREAK={read.RenewedCount}; " +
                $"mantidos={read.Features.Count - written}.\n");
            if (written > 0)
            {
                editor.WriteMessage(
                    "\nGeoAutoEngine: o desenho recebeu os códigos das feições. Salve o DWG para mantê-los.\n");
            }

            PluginLog.Information(
                $"Exportação GeoMap: {lines.Length} linha(s), {blocks.Length} bloco(s); novos={read.AssignedCount}; " +
                $"renovados={read.RenewedCount}; fora={GeoMapBlockRules.Describe(read.SkippedBlocks)}; " +
                $"arquivos={string.Join(", ", outputs.Select(output => Path.GetFileName(output.Path)))}.");
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Exportação de Polyline para o GeoMap", exception);
            document.Editor.WriteMessage($"\nGeoAutoEngine: não foi possível exportar para o GeoMap. {exception.Message}\n");
        }
    }

    private static NetworkOutput CreateOutput(
        string kind,
        IReadOnlyList<GeoMapExportFeature> features,
        GeoMapPolylineReadResult read,
        string sourceCrs,
        string units,
        string projectExternalId,
        string projectName,
        string networkExternalId,
        string networkName,
        string networkType,
        DateTimeOffset exportedAt)
    {
        var color = GeoMapLayerColor.ChooseNetworkColor(features, read.LayerColors);
        var request = new GeoMapExportRequest(
            sourceCrs,
            units.Trim().ToLowerInvariant(),
            projectExternalId,
            projectName,
            networkExternalId,
            networkName,
            networkType,
            exportedAt,
            features,
            color.Color);
        return new NetworkOutput(
            kind,
            features.Count,
            networkName,
            GeoMapExportSerializer.ContractVersionFor(request),
            color.Color,
            color.Layer,
            GeoMapExportSerializer.Serialize(request),
            string.Empty);
    }

    private sealed record NetworkOutput(
        string Kind,
        int Count,
        string NetworkName,
        string Version,
        string? Color,
        string? ColorLayer,
        string Json,
        string Path);

    private static ObjectId[]? SelectPolylines(Editor editor)
    {
        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nSelecione as Polylines 2D e os blocos da rede: ",
            MessageForRemoval = "\nRemova entidades da seleção: ",
        };
        // Blocos (INSERT) desde o M4.3c: viram pontos numa rede separada.
        var filter = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "LWPOLYLINE,INSERT"),
        ]);
        var result = editor.GetSelection(options, filter);
        return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : null;
    }

    private static bool TryPrompt(
        Editor editor,
        string label,
        string? defaultValue,
        out string value)
    {
        // Com valor padrão, o próprio AutoCAD mostra " <padrão>: " depois do texto. Antes o texto
        // também trazia o padrão, e ele aparecia duas vezes na linha de comando.
        var message = string.IsNullOrWhiteSpace(defaultValue) ? $"\n{label}: " : $"\n{label}";
        var options = new PromptStringOptions(message)
        {
            AllowSpaces = true,
        };
        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            options.DefaultValue = defaultValue;
            options.UseDefaultValue = true;
        }

        var result = editor.GetString(options);
        value = result.StringResult?.Trim() ?? string.Empty;
        return result.Status == PromptStatus.OK;
    }

    // O CRS não tem valor padrão (nunca é adivinhado). Vazio ou fora do formato: pergunta de novo
    // na hora, em vez de deixar o comando falhar depois de todas as perguntas. Esc cancela.
    private static bool TryPromptCrs(Editor editor, out string sourceCrs)
    {
        while (true)
        {
            if (!TryPrompt(editor, "CRS de origem (EPSG, ex.: 31983 ou EPSG:31983)", null, out var value))
            {
                sourceCrs = string.Empty;
                return false;
            }

            sourceCrs = NormalizeEpsg(value);
            if (IsEpsg(sourceCrs))
            {
                return true;
            }

            editor.WriteMessage(
                "\nGeoAutoEngine: informe o EPSG do sistema de coordenadas do desenho, por exemplo 31983 " +
                "(SIRGAS 2000 / UTM 23S). Esc cancela a exportação.");
        }
    }

    private static bool IsEpsg(string value) =>
        value.StartsWith("EPSG:", StringComparison.Ordinal) &&
        int.TryParse(value.Substring("EPSG:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var code) &&
        code > 0;

    private static NetworkCadDefinition InferNetwork(IReadOnlyList<GeoMapExportFeature> features)
    {
        var matches = features
            .Select(feature => NetworkCadCatalog.All.FirstOrDefault(definition =>
                definition.CurveLayers.Contains(feature.SourceLayer, StringComparer.OrdinalIgnoreCase)))
            .Where(definition => definition is not null)
            .Distinct()
            .ToArray();

        return matches.Length == 1
            ? matches[0]!
            : NetworkCadCatalog.All.First(definition => definition.NetworkId == "nao_identificada");
    }

    private static string CanonicalNetworkType(string networkId) => networkId switch
    {
        "agua_potavel" => "potable_water",
        "aguas_pluviais" => "stormwater",
        "esgoto_sanitario" => "sanitary_sewer",
        "esgoto_industrial" => "industrial_sewer",
        "rede_eletrica" => "electricity",
        "telefone" => "telephone",
        "fibra_optica" => "fiber_optic",
        "rede_incendio" => "fire_network",
        "gas" => "gas",
        "outra" => "other",
        _ => "unidentified",
    };

    private static string? DrawingUnit(Database database) => database.Insunits.ToString() switch
    {
        "Meters" => "metre",
        "Feet" => "foot",
        "USSurveyFeet" => "us-survey-foot",
        _ => null,
    };

    private static string NormalizeEpsg(string value)
    {
        value = value.Trim().ToUpperInvariant();
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var epsg) && epsg > 0
            ? $"EPSG:{epsg}"
            : value;
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    }
}

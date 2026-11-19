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

public sealed partial class GeoMapExportCommands
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
            var selection = SelectPolylines(
                document.Editor,
                ExportableTypes,
                "\nSelecione as linhas (Polyline, LINE, ARC) e os blocos: ");
            if (selection is null)
            {
                document.Editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            var read = AutoCadGeoMapPolylineReader.Read(document, selection);
            // Uma rede por layer (M4.3e, decisão do usuário de 16/11/2026), como a ponte DXF; os blocos de cada
            // layer vão numa rede separada (DEC-029 do GeoMap). Cada rede vira um arquivo.
            var networks = GeoMapLayerNetworks.Split(read.Features);
            var lines = read.Features.Where(feature => feature.Block is null).ToArray();
            IReadOnlyList<GeoMapExportFeature> features = read.Features;
            var inferredNetwork = InferNetwork(lines.Length > 0 ? lines : read.Features);
            var drawingName = Path.GetFileNameWithoutExtension(document.Name);
            var editor = document.Editor;
            if (read.SkippedBlocks.Count > 0)
            {
                editor.WriteMessage(
                    $"\nGeoAutoEngine: blocos deixados de fora: {GeoMapBlockRules.Describe(read.SkippedBlocks)}.\n");
            }

            editor.WriteMessage(
                $"\nGeoAutoEngine: {networks.Count} rede(s), uma por layer: " +
                string.Join(", ", networks.Select(network =>
                    $"{GeoMapLayerNetworks.NetworkName(network)} ({network.Features.Count})")) + ".\n");

            if (!TryPrompt(editor, "Identificador do projeto", drawingName, out var projectExternalId) ||
                !TryPrompt(editor, "Nome do projeto", drawingName, out var projectName) ||
                !TryPrompt(editor, "Tipo das redes", CanonicalNetworkType(inferredNetwork.NetworkId), out var networkType) ||
                !TryPromptCrs(editor, out var sourceCrs) ||
                !TryPrompt(
                    editor,
                    "Unidade (metre, foot, us-survey-foot ou degree)",
                    DrawingUnit(document.Database),
                    out var units))
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            // Profundidades dos pontos _TG (entrega 1): associadas na hora, só leitura no desenho.
            // Tipo das peças (pela tabela GEO_PECA ou pelo nome do bloco).
            features = AddFittingTypes(document, features);
            networks = GeoMapLayerNetworks.Split(features);

            if (!TryAddDepthsForExport(
                document, features, lines, units.Trim().ToLowerInvariant(), out var withDepths, out var depths))
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            if (withDepths is not null)
            {
                features = withDepths;
                networks = GeoMapLayerNetworks.Split(features);
            }

            // Todos os arquivos são montados e validados antes de qualquer gravação.
            var exportedAt = DateTimeOffset.UtcNow;
            var outputs = networks
                .Select(network => CreateOutput(
                    network, read, sourceCrs, units, projectExternalId, projectName, networkType, exportedAt))
                .ToList();

            using var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = "geomap.json",
                FileName = SafeFileName($"{projectExternalId}{GeoMapBlockRules.FileExtension}"),
                Filter = "Contrato GeoMap (*.geomap.json)|*.geomap.json",
                OverwritePrompt = false,
                RestoreDirectory = true,
                Title = "Escolha a pasta e o nome base (cada layer vira um arquivo <nome>-<layer>.geomap.json)",
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                editor.WriteMessage("\nGeoAutoEngine: exportação GeoMap cancelada.\n");
                return;
            }

            // Os códigos novos são gravados antes dos arquivos: se a gravação de um arquivo falhar,
            // os códigos já estão no desenho e a próxima exportação usa os mesmos (DEC-ECO-0003).
            var written = AutoCadGeoMapPolylineReader.WriteFeatureIds(document, read.PendingWrites);
            foreach (var output in outputs)
            {
                var path = GeoMapLayerNetworks.FilePath(dialog.FileName, output.Network);
                File.WriteAllText(path, output.Json, new UTF8Encoding(false));
                editor.WriteMessage(
                    $"\nGeoAutoEngine: {output.Network.Features.Count} {(output.Network.IsBlocks ? "blocos" : "linhas")} " +
                    $"na rede \"{GeoMapLayerNetworks.NetworkName(output.Network)}\" (versão {output.Version}, " +
                    $"cor {output.Color ?? "nenhuma"}) -> {path}");
            }

            if (depths is not null)
            {
                var diagnosticPath = DepthDiagnosticPath(dialog.FileName);
                File.WriteAllText(diagnosticPath, PipeDepthReport.DiagnosticCsv(depths), new UTF8Encoding(true));
                editor.WriteMessage(
                    $"\nGeoAutoEngine: profundidades em {depths.PipesWithMeasurements} de {depths.Pipes.Count} linhas; " +
                    $"diagnóstico -> {diagnosticPath}" +
                    "\nGeoAutoEngine: essas profundidades não estão gravadas no desenho (provisórias). " +
                    "Para gravar e poder corrigir à mão, use GEOAUTO3DEPTHSCAN.");
            }

            editor.WriteMessage(
                $"\nGeoAutoEngine: códigos novos={read.AssignedCount}; renovados por cópia/BREAK={read.RenewedCount}; " +
                $"mantidos={read.Features.Count - written}.\n");
            if (written > 0)
            {
                editor.WriteMessage(
                    "\nGeoAutoEngine: o desenho recebeu os códigos das feições. Salve o DWG para mantê-los.\n");
            }

            PluginLog.Information(
                $"Exportação GeoMap: {read.Features.Count} feição(ões) em {outputs.Count} rede(s); " +
                $"novos={read.AssignedCount}; renovados={read.RenewedCount}; " +
                $"fora={GeoMapBlockRules.Describe(read.SkippedBlocks)}.");
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Exportação de Polyline para o GeoMap", exception);
            document.Editor.WriteMessage($"\nGeoAutoEngine: não foi possível exportar para o GeoMap. {exception.Message}\n");
        }
    }

    private static NetworkOutput CreateOutput(
        GeoMapLayerNetwork network,
        GeoMapPolylineReadResult read,
        string sourceCrs,
        string units,
        string projectExternalId,
        string projectName,
        string networkType,
        DateTimeOffset exportedAt)
    {
        var color = GeoMapLayerColor.ChooseNetworkColor(network.Features, read.LayerColors);
        var request = new GeoMapExportRequest(
            sourceCrs,
            units.Trim().ToLowerInvariant(),
            projectExternalId,
            projectName,
            GeoMapLayerNetworks.NetworkExternalId(projectExternalId, network),
            GeoMapLayerNetworks.NetworkName(network),
            networkType,
            exportedAt,
            network.Features,
            color.Color);
        return new NetworkOutput(
            network,
            GeoMapExportSerializer.ContractVersionFor(request),
            color.Color,
            GeoMapExportSerializer.Serialize(request));
    }

    private sealed record NetworkOutput(GeoMapLayerNetwork Network, string Version, string? Color, string Json);

    // Seleção por layer (M4.3e): o usuário digita as layers (aceita curinga e vírgula) e o comando pega todas as
    // entidades exportáveis delas no espaço do modelo. Ou seleciona os objetos, como antes.
    private const string ExportableTypes = "LWPOLYLINE,LINE,ARC,INSERT";

    private static ObjectId[]? SelectPolylines(Editor editor, string types, string objectsMessage)
    {
        var mode = new PromptKeywordOptions("\nSelecionar por")
        {
            AllowNone = true,
        };
        mode.Keywords.Add("Layers");
        mode.Keywords.Add("Objetos");
        mode.Keywords.Default = "Layers";
        var modeResult = editor.GetKeywords(mode);
        if (modeResult.Status is not (PromptStatus.OK or PromptStatus.None))
        {
            return null;
        }

        if (modeResult.Status == PromptStatus.OK && modeResult.StringResult == "Objetos")
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding = objectsMessage,
                MessageForRemoval = "\nRemova entidades da seleção: ",
            };
            var picked = editor.GetSelection(options, new SelectionFilter(
            [
                new TypedValue((int)DxfCode.Start, types),
            ]));
            return picked.Status == PromptStatus.OK ? picked.Value.GetObjectIds() : null;
        }

        if (!TryPrompt(editor, "Layers (curinga e vírgula, ex.: RD_*,TÊ)", null, out var layers) ||
            string.IsNullOrWhiteSpace(layers))
        {
            return null;
        }

        var all = editor.SelectAll(new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, types),
            new TypedValue((int)DxfCode.LayerName, layers),
            // Só o espaço do modelo, como a ponte DXF.
            new TypedValue((int)DxfCode.LayoutName, "Model"),
        ]));
        if (all.Status != PromptStatus.OK)
        {
            editor.WriteMessage($"\nGeoAutoEngine: nenhuma linha ou bloco nas layers {layers}.");
            return null;
        }

        return all.Value.GetObjectIds();
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

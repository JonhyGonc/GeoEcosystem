using System.IO;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using GeoAutoEngine.Application.GeoMapExport;
using GeoAutoEngine.Cad2026.GeoMapExport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Plugin;

/// <summary>
/// Profundidades das tubulações a partir dos pontos do levantamento (<c>_TG</c>), M7:
/// <c>GEOAUTO3DEPTHSCAN</c> analisa, mostra o resumo, salva o diagnóstico em CSV e, com confirmação, grava as
/// profundidades nas linhas (tabela Object Data GEO_PROFUNDIDADE, visível na paleta Propriedades);
/// <c>GEOAUTO3GEOMAPEXPORT</c> leva ao GeoMap o que está gravado (inclusive o corrigido à mão).
/// </summary>
public sealed partial class GeoMapExportCommands
{
    private const string LineTypes = "LWPOLYLINE,LINE,ARC";
    private const string SurveyLayers = GeoMapBlockRules.SurveyLayerPrefix + "*";

    // Sem NoUndoMarker: desde o M7.2 o comando pode gravar no desenho, e U desfaz a gravação inteira.
    [CommandMethod("GEOAUTO3DEPTHSCAN", CommandFlags.Modal)]
    public void ScanDepths()
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        try
        {
            var selection = SelectPolylines(editor, LineTypes, "\nSelecione as tubulações (Polyline, LINE, ARC): ");
            if (selection is null)
            {
                editor.WriteMessage("\nGeoAutoEngine: análise de profundidades cancelada.\n");
                return;
            }

            var units = DrawingUnit(document.Database);
            if (units is null)
            {
                if (!TryPrompt(editor, "Unidade do desenho (metre, foot ou us-survey-foot)", "metre", out var typed))
                {
                    editor.WriteMessage("\nGeoAutoEngine: análise de profundidades cancelada.\n");
                    return;
                }

                units = typed;
            }

            var analysis = Analyze(document, selection, units.Trim().ToLowerInvariant());
            if (analysis is null)
            {
                return;
            }

            var plan = TryPlanWrite(document.Database, editor, analysis);
            if (plan is not null)
            {
                editor.WriteMessage("\n" + string.Join("\n", plan.SummaryLines()) + "\n");
            }

            while (true)
            {
                var canWrite = plan is not null && plan.Writes.Count > 0;
                var options = new PromptKeywordOptions(canWrite
                    ? "\nGravar as profundidades nas linhas?"
                    : "\nNada a gravar. Salvar o diagnóstico em CSV?")
                {
                    AllowNone = true,
                };
                if (canWrite)
                {
                    options.Keywords.Add("Gravar");
                }

                options.Keywords.Add("Diagnostico");
                options.Keywords.Add("Sair");
                options.Keywords.Default = canWrite ? "Gravar" : "Diagnostico";
                var answer = editor.GetKeywords(options);
                var choice = answer.Status == PromptStatus.OK ? answer.StringResult : answer.Status == PromptStatus.None ? options.Keywords.Default : "Sair";
                if (choice == "Diagnostico")
                {
                    SaveDiagnostic(document, analysis);
                    continue;
                }

                if (choice == "Gravar" && canWrite)
                {
                    var written = AutoCadPipeDepthStore.Write(document.Database, plan!);
                    editor.WriteMessage(
                        $"\nGeoAutoEngine: profundidades gravadas em {written} linha(s) (tabela {AutoCadPipeDepthStore.TableName}). " +
                        "Veja na paleta Propriedades (Ctrl+1). Para desfazer, use U.\n");
                    PluginLog.Information($"Profundidades gravadas em {written} linha(s).");
                    return;
                }

                editor.WriteMessage("\nGeoAutoEngine: nada foi gravado no desenho.\n");
                return;
            }
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Análise de profundidades", exception);
            editor.WriteMessage(
                $"\nGeoAutoEngine: não foi possível concluir as profundidades. {exception.Message}" +
                "\nSe parte já tinha sido gravada, use U para desfazer o comando inteiro.\n");
        }
    }

    // Plano de gravação, ou null quando o Object Data do Map 3D não está disponível (só o diagnóstico funciona).
    private static PipeDepthWritePlan? TryPlanWrite(Database database, Editor editor, PipeDepthAnalysis analysis)
    {
        try
        {
            var stored = AutoCadPipeDepthStore.Read(database, analysis.Pipes.Select(pipe => pipe.Pipe.CadHandle));
            return PipeDepthWritePlanner.Plan(analysis, stored);
        }
        catch (System.Exception exception) when (IsMissingMap(exception))
        {
            PluginLog.Error("Object Data indisponível", exception);
            editor.WriteMessage(
                "\nGeoAutoEngine: o Object Data do Map 3D não está disponível neste programa; só o diagnóstico funciona.\n");
            return null;
        }
    }

    private static void SaveDiagnostic(Document document, PipeDepthAnalysis analysis)
    {
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "csv",
            FileName = Path.GetFileNameWithoutExtension(document.Name) + "-profundidades-diagnostico.csv",
            Filter = "CSV (*.csv)|*.csv",
            RestoreDirectory = true,
            Title = "Salvar o diagnóstico das profundidades",
        };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, PipeDepthReport.DiagnosticCsv(analysis), new UTF8Encoding(true));
        document.Editor.WriteMessage($"\nGeoAutoEngine: diagnóstico das profundidades -> {dialog.FileName}\n");
    }

    // Na exportação. False quando o usuário cancela (Esc). withDepths: as linhas com o resumo das profundidades
    // (null quando não há o que levar ou o usuário responde Não). analysis: só na associação provisória, para o CSV.
    private static bool TryAddDepthsForExport(
        Document document,
        IReadOnlyList<GeoMapExportFeature> features,
        IReadOnlyList<GeoMapExportFeature> lines,
        string units,
        out IReadOnlyList<GeoMapExportFeature>? withDepths,
        out PipeDepthAnalysis? analysis)
    {
        withDepths = null;
        analysis = null;
        var editor = document.Editor;
        if (lines.Count == 0)
        {
            return true;
        }

        // 1) Profundidades gravadas no desenho, inclusive as corrigidas à mão.
        Dictionary<string, PipeDepthFields>? stored = null;
        try
        {
            stored = AutoCadPipeDepthStore.Read(document.Database, lines.Select(line => line.CadHandle))
                .Where(entry => entry.Value.Fields?.ToSummary() is not null)
                .ToDictionary(entry => entry.Key, entry => entry.Value.Fields!, StringComparer.Ordinal);
        }
        catch (System.Exception exception) when (IsMissingMap(exception))
        {
            PluginLog.Error("Object Data indisponível", exception);
            editor.WriteMessage("\nGeoAutoEngine: o Object Data do Map 3D não está disponível; profundidades gravadas não serão lidas.");
        }

        if (stored is { Count: > 0 })
        {
            var manual = stored.Count(entry => entry.Value.NormalizedOrigin == PipeDepthOrigins.Manual);
            var answer = AskYesNo(editor, $"\nLevar as profundidades gravadas no desenho ({stored.Count} linhas, {manual} manuais)?");
            if (answer is null)
            {
                return false;
            }

            if (answer == true)
            {
                withDepths = PipeDepthExport.FromStored(features, stored);
            }

            return true;
        }

        // 2) Nada gravado: associação na hora, provisória.
        if (units == "degree")
        {
            editor.WriteMessage("\nGeoAutoEngine: desenho em graus; exportação sem profundidades.");
            return true;
        }

        var survey = SelectSurveyPoints(editor);
        if (survey.Length == 0)
        {
            editor.WriteMessage($"\nGeoAutoEngine: nenhum ponto nas layers {SurveyLayers}; exportação sem profundidades.");
            return true;
        }

        var ask = AskYesNo(
            editor,
            $"\nNenhuma profundidade gravada nas linhas. Levar as dos {survey.Length} pontos {SurveyLayers} (provisórias)?");
        if (ask is null)
        {
            return false;
        }

        if (ask == true)
        {
            analysis = Analyze(document.Database, editor, lines, survey, units);
            withDepths = PipeDepthExport.Attach(features, analysis);
        }

        return true;
    }

    // true = Sim, false = Não, null = cancelado.
    private static bool? AskYesNo(Editor editor, string message)
    {
        var options = new PromptKeywordOptions(message)
        {
            AllowNone = true,
        };
        options.Keywords.Add("Sim");
        options.Keywords.Add("Nao");
        options.Keywords.Default = "Sim";
        var answer = editor.GetKeywords(options);
        if (answer.Status is not (PromptStatus.OK or PromptStatus.None))
        {
            return null;
        }

        return answer.StringResult != "Nao";
    }

    private static bool IsMissingMap(System.Exception exception) =>
        exception is FileNotFoundException or FileLoadException or TypeLoadException or MissingMethodException;

    private static PipeDepthAnalysis? Analyze(Document document, ObjectId[] selection, string units)
    {
        var editor = document.Editor;
        var survey = SelectSurveyPoints(editor);
        if (survey.Length == 0)
        {
            editor.WriteMessage($"\nGeoAutoEngine: nenhum ponto nas layers {SurveyLayers}.\n");
            return null;
        }

        // Só leitura: os códigos novos das linhas (FEATURE_ID) não são gravados aqui.
        var read = AutoCadGeoMapPolylineReader.Read(document, selection);
        return Analyze(document.Database, editor, read.Features, survey, units);
    }

    private static PipeDepthAnalysis Analyze(
        Database database,
        Editor editor,
        IReadOnlyList<GeoMapExportFeature> lines,
        ObjectId[] survey,
        string units)
    {
        var options = PipeDepthOptions.ForUnits(units);
        var points = AutoCadSurveyPointReader.Read(database, survey);
        var analysis = PipeDepthAssociator.Analyze(lines, points.Points, options);
        editor.WriteMessage("\n" + string.Join("\n", PipeDepthReport.SummaryLines(analysis)));
        editor.WriteMessage(
            $"\nPosição dos pontos: ponto do bloco (NODE)={points.FromBlockPoint}; " +
            $"inserção do bloco (sem ponto dentro)={points.FromInsertion}; " +
            $"primeiro de vários pontos={points.FromFirstOfMany}; sem DESC={points.WithoutDescription}.\n");
        PluginLog.Information(
            $"Profundidades: {analysis.Pipes.Count} linha(s), {analysis.Points.Count} ponto(s) _TG, " +
            $"{analysis.PipeDepthPoints} com profundidade, {analysis.PipesWithMeasurements} linha(s) com medição.");
        return analysis;
    }

    // Todos os blocos das layers _TG* no espaço do modelo.
    private static ObjectId[] SelectSurveyPoints(Editor editor)
    {
        var result = editor.SelectAll(new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "INSERT"),
            new TypedValue((int)DxfCode.LayerName, SurveyLayers),
            new TypedValue((int)DxfCode.LayoutName, "Model"),
        ]));
        return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : Array.Empty<ObjectId>();
    }

    private static string DepthDiagnosticPath(string exportPath)
    {
        var stem = exportPath.EndsWith(GeoMapBlockRules.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? exportPath.Substring(0, exportPath.Length - GeoMapBlockRules.FileExtension.Length)
            : Path.Combine(Path.GetDirectoryName(exportPath) ?? string.Empty, Path.GetFileNameWithoutExtension(exportPath));
        return stem + "-profundidades-diagnostico.csv";
    }
}

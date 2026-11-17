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
/// Profundidades das tubulações a partir dos pontos do levantamento (<c>_TG</c>), entrega 1:
/// <c>GEOAUTO3DEPTHSCAN</c> só analisa (nada é gravado no desenho) e salva o diagnóstico em CSV;
/// <c>GEOAUTO3GEOMAPEXPORT</c> pergunta se leva as profundidades para o GeoMap.
/// </summary>
public sealed partial class GeoMapExportCommands
{
    private const string LineTypes = "LWPOLYLINE,LINE,ARC";
    private const string SurveyLayers = GeoMapBlockRules.SurveyLayerPrefix + "*";

    [CommandMethod("GEOAUTO3DEPTHSCAN", CommandFlags.Modal | CommandFlags.NoUndoMarker)]
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

            editor.WriteMessage("\nGeoAutoEngine: nada foi gravado no desenho. Para levar ao GeoMap, use GEOAUTO3GEOMAPEXPORT.\n");
            var save = new PromptKeywordOptions("\nSalvar o diagnóstico em CSV?")
            {
                AllowNone = true,
            };
            save.Keywords.Add("Sim");
            save.Keywords.Add("Nao");
            save.Keywords.Default = "Sim";
            var answer = editor.GetKeywords(save);
            if (answer.Status is not (PromptStatus.OK or PromptStatus.None) || answer.StringResult == "Nao")
            {
                return;
            }

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
            editor.WriteMessage($"\nGeoAutoEngine: diagnóstico das profundidades -> {dialog.FileName}\n");
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Análise de profundidades", exception);
            editor.WriteMessage($"\nGeoAutoEngine: não foi possível analisar as profundidades. {exception.Message}\n");
        }
    }

    // Na exportação: pergunta se leva as profundidades. False quando o usuário cancela (Esc); depths fica null
    // quando não há o que associar ou o usuário responde Não.
    private static bool TryAnalyzeDepthsForExport(
        Document document,
        IReadOnlyList<GeoMapExportFeature> lines,
        string units,
        out PipeDepthAnalysis? depths)
    {
        depths = null;
        var editor = document.Editor;
        if (lines.Count == 0)
        {
            return true;
        }

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

        var ask = new PromptKeywordOptions($"\nLevar as profundidades dos {survey.Length} pontos {SurveyLayers}?")
        {
            AllowNone = true,
        };
        ask.Keywords.Add("Sim");
        ask.Keywords.Add("Nao");
        ask.Keywords.Default = "Sim";
        var answer = editor.GetKeywords(ask);
        if (answer.Status is not (PromptStatus.OK or PromptStatus.None))
        {
            return false;
        }

        if (answer.StringResult == "Nao")
        {
            return true;
        }

        depths = Analyze(document.Database, editor, lines, survey, units);
        return true;
    }

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

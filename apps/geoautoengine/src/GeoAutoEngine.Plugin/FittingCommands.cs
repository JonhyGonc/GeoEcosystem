using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using GeoAutoEngine.Application.GeoMapExport;
using GeoAutoEngine.Cad2026.GeoMapExport;
using AutoCadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace GeoAutoEngine.Plugin;

/// <summary>
/// Tipo das peças (tê, registro, cruzeta, ventosa…) pelo nome do bloco (pedido do usuário, 19/11/2026):
/// <c>GEOAUTO3FITTINGS</c> mostra o resumo e, com confirmação, grava o tipo nos blocos (tabela Object Data
/// GEO_PECA, visível na paleta Propriedades); <c>GEOAUTO3GEOMAPEXPORT</c> leva o tipo em <c>tipoPeca</c>.
/// </summary>
public sealed partial class GeoMapExportCommands
{
    [CommandMethod("GEOAUTO3FITTINGS", CommandFlags.Modal)]
    public void WriteFittingTypes()
    {
        var document = AutoCadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        try
        {
            var selection = SelectPolylines(editor, "INSERT", "\nSelecione as peças (blocos): ");
            if (selection is null)
            {
                editor.WriteMessage("\nGeoAutoEngine: tipo das peças cancelado.\n");
                return;
            }

            // Só leitura: os códigos novos (FEATURE_ID) não são gravados aqui.
            var read = AutoCadGeoMapPolylineReader.Read(document, selection);
            var blocks = read.Features.Where(feature => feature.Block is not null).ToArray();
            if (read.SkippedBlocks.Count > 0)
            {
                editor.WriteMessage($"\nGeoAutoEngine: blocos que não são peça, ignorados: {GeoMapBlockRules.Describe(read.SkippedBlocks)}.");
            }

            if (blocks.Length == 0)
            {
                editor.WriteMessage("\nGeoAutoEngine: nenhuma peça selecionada.\n");
                return;
            }

            var stored = AutoCadFittingStore.Read(document.Database, blocks.Select(block => block.CadHandle));
            var plan = FittingWritePlanner.Plan(blocks.Select(block => (block.CadHandle, block.Block!.Name)), stored);
            editor.WriteMessage("\n" + string.Join("\n", plan.SummaryLines()) + "\n");
            if (plan.Writes.Count == 0)
            {
                editor.WriteMessage("\nGeoAutoEngine: nada a gravar.\n");
                return;
            }

            var answer = AskYesNo(editor, $"\nGravar o tipo em {plan.Writes.Count} peça(s)?");
            if (answer != true)
            {
                editor.WriteMessage("\nGeoAutoEngine: nada foi gravado no desenho.\n");
                return;
            }

            var written = AutoCadFittingStore.Write(document.Database, plan);
            editor.WriteMessage(
                $"\nGeoAutoEngine: tipo gravado em {written} peça(s) (tabela {AutoCadFittingStore.TableName}). " +
                "Veja na paleta Propriedades (Ctrl+1). Para desfazer, use U.\n");
            PluginLog.Information($"Tipo das peças gravado em {written} bloco(s).");
        }
        catch (System.Exception exception) when (IsMissingMap(exception))
        {
            PluginLog.Error("Object Data indisponível", exception);
            editor.WriteMessage("\nGeoAutoEngine: o Object Data do Map 3D não está disponível neste programa.\n");
        }
        catch (System.Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            PluginLog.Error("Tipo das peças", exception);
            editor.WriteMessage(
                $"\nGeoAutoEngine: não foi possível gravar o tipo das peças. {exception.Message}" +
                "\nSe parte já tinha sido gravada, use U para desfazer o comando inteiro.\n");
        }
    }

    // Na exportação: o tipo gravado no desenho (inclusive manual) e, sem ele, o tipo pela lista de nomes.
    private static IReadOnlyList<GeoMapExportFeature> AddFittingTypes(
        Document document,
        IReadOnlyList<GeoMapExportFeature> features)
    {
        var blocks = features.Where(feature => feature.Block is not null).Select(feature => feature.CadHandle).ToArray();
        if (blocks.Length == 0)
        {
            return features;
        }

        var stored = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var (handle, state) in AutoCadFittingStore.Read(document.Database, blocks))
            {
                if (!string.IsNullOrWhiteSpace(state.Type))
                {
                    stored[handle] = state.Type;
                }
            }
        }
        catch (System.Exception exception) when (IsMissingMap(exception))
        {
            PluginLog.Error("Object Data indisponível", exception);
        }

        var result = FittingExport.Apply(features, stored);
        var typed = result.Count(feature => feature.Block?.FittingType is not null);
        document.Editor.WriteMessage($"\nGeoAutoEngine: tipo da peça em {typed} de {blocks.Length} bloco(s).");
        return result;
    }
}

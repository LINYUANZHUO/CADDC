using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Plugin.Printing;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class BatchPrintCommands
{
    [CommandMethod("DC_CMD_BATCH_PRINT", CommandFlags.Modal)]
    [ShortcutCommand("批量打印", "打开批量打印面板", "DCDY")]
    public static void ShowBatchPrintModule()
    {
        ToolboxPalette.Show(ToolboxModule.BatchPrint);
    }

    [CommandMethod("DC_CMD_PRINT_PAPER", CommandFlags.Modal)]
    [ShortcutCommand("批量打印", "选中图框打印到纸张", "DCD1")]
    public static void PrintSelectedFramesToPaper()
    {
        OpenSelectedFrameCenter(toPdf: false);
    }

    [CommandMethod("DC_CMD_PRINT_PDF", CommandFlags.Modal)]
    [ShortcutCommand("批量打印", "选中图框打印到 PDF", "DCD2")]
    public static void PrintSelectedFramesToPdf()
    {
        OpenSelectedFrameCenter(toPdf: true);
    }

    [CommandMethod("DC_CMD_PRINT_LAYOUT_PAPER", CommandFlags.Modal)]
    [ShortcutCommand("批量打印", "当前布局打印到纸张", "DCD3")]
    public static void PrintLayoutsToPaper()
    {
        OpenLayoutCenter(toPdf: false);
    }

    [CommandMethod("DC_CMD_PRINT_LAYOUT_PDF", CommandFlags.Modal)]
    [ShortcutCommand("批量打印", "当前布局打印到 PDF", "DCD4")]
    public static void PrintLayoutsToPdf()
    {
        OpenLayoutCenter(toPdf: true);
    }

    [CommandMethod("DC_CMD_DRAW_PRINT_FRAME", CommandFlags.Modal)]
    public static void DrawPrintFrame()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var created = 0;
        while (true)
        {
            var first = editor.GetPoint("\n指定打印图框第一个角点，按 Esc 结束：");
            if (first.Status != PromptStatus.OK)
            {
                break;
            }

            var second = editor.GetCorner(new PromptCornerOptions("\n指定另一个角点：", first.Value));
            if (second.Status != PromptStatus.OK)
            {
                break;
            }

            using var transaction = document.Database.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
            var minX = Math.Min(first.Value.X, second.Value.X);
            var minY = Math.Min(first.Value.Y, second.Value.Y);
            var maxX = Math.Max(first.Value.X, second.Value.X);
            var maxY = Math.Max(first.Value.Y, second.Value.Y);
            using var polyline = new Polyline(4);
            polyline.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
            polyline.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
            polyline.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
            polyline.Closed = true;
            space.AppendEntity(polyline);
            transaction.AddNewlyCreatedDBObject(polyline, true);
            transaction.Commit();
            created++;
        }

        editor.WriteMessage($"\n已绘制 {created} 个打印图框。");
    }

    [CommandMethod("DC_CMD_PDF_TOOL", CommandFlags.Modal)]
    public static void ShowPdfTool()
    {
        AcadApplication.ShowModalWindow(new PdfToolWindow());
    }

    private static void OpenSelectedFrameCenter(bool toPdf)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var jobs = FrameRecognitionService.SelectFrames(document, PrintRuntime.Settings);
            if (jobs.Count == 0)
            {
                document.Editor.WriteMessage("\n未识别到可打印图框；请检查图框类型或关闭“识别空白图框”。");
                return;
            }

            AcadApplication.ShowModalWindow(new BatchPrintWindow(document, jobs, toPdf));
        }
        catch (System.Exception exception)
        {
            document.Editor.WriteMessage($"\n批量打印初始化失败：{exception.Message}");
        }
    }

    private static void OpenLayoutCenter(bool toPdf)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var job = FrameRecognitionService.GetCurrentOrActivateFirstPaperSpaceLayout(document, PrintRuntime.Settings);
            if (job is null)
            {
                document.Editor.WriteMessage("\n当前图纸没有可打印的布局；请先新建布局，再执行 DCD3 或 DCD4。");
                return;
            }

            AcadApplication.ShowModalWindow(new BatchPrintWindow(document, [job], toPdf));
        }
        catch (System.Exception exception)
        {
            document.Editor.WriteMessage($"\n批量打印初始化失败：{exception.Message}");
        }
    }
}

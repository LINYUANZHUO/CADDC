using System.Collections;
using System.Globalization;
using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: CommandClass(typeof(YiZhiMao.Dc.HostProbe.HostProbeCommands))]

namespace YiZhiMao.Dc.HostProbe;

public static class HostProbeCommands
{
    [CommandMethod("DC_TEST_FRAME_RECOGNITION", CommandFlags.Modal)]
    public static void RecognizeFrames()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var pluginAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => string.Equals(
                    assembly.GetName().Name,
                    "YiZhiMao.Dc.Plugin",
                    StringComparison.Ordinal));
            var serviceType = pluginAssembly.GetType(
                "YiZhiMao.Dc.Plugin.Printing.FrameRecognitionService",
                throwOnError: true)!;
            var selectFrames = serviceType.GetMethod(
                "SelectFrames",
                BindingFlags.Public | BindingFlags.Static) ??
                throw new MissingMethodException(serviceType.FullName, "SelectFrames");
            var settings = new PrintSettings
            {
                DetectClosedPolylines = true,
                DetectLooseLines = true,
                DetectBlocks = false,
                IgnoreEmptyFrames = false
            };
            var result = selectFrames.Invoke(null, [document, settings]) as IEnumerable ??
                         throw new InvalidOperationException("Frame recognition returned no enumerable result.");
            var frames = new List<PrintFrame>();
            foreach (var job in result)
            {
                var frame = job?.GetType().GetProperty("Frame")?.GetValue(job);
                if (frame is PrintFrame printFrame)
                {
                    frames.Add(printFrame);
                }
            }

            var coordinates = string.Join("|", frames.Select(FormatFrame));
            document.Editor.WriteMessage($"\nDC_HOST_FRAME_SCAN count={frames.Count} frames={coordinates}");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            WriteError(document, exception.InnerException);
        }
        catch (System.Exception exception)
        {
            WriteError(document, exception);
        }
    }

    [CommandMethod("DC_TEST_FRAME_CONTENT_GROUPING", CommandFlags.Modal)]
    public static void GroupFrameContent()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            using var transaction = document.Database.TransactionManager.StartTransaction();
            var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForWrite);
            const string blockName = "DC_HOST_OVERLAP_BLOCK";
            var blockDefinition = new BlockTableRecord { Name = blockName };
            var blockDefinitionId = blockTable.Add(blockDefinition);
            transaction.AddNewlyCreatedDBObject(blockDefinition, true);
            var blockGeometry = CreateRectangle(0, 0, 320, 300);
            blockDefinition.AppendEntity(blockGeometry);
            transaction.AddNewlyCreatedDBObject(blockGeometry, true);

            var currentSpace = (BlockTableRecord)transaction.GetObject(
                document.Database.CurrentSpaceId,
                OpenMode.ForWrite);
            var ordinaryGeometry = CreateRectangle(200000, 200000, 201000, 200700);
            currentSpace.AppendEntity(ordinaryGeometry);
            transaction.AddNewlyCreatedDBObject(ordinaryGeometry, true);
            var overlappingBlock = new BlockReference(
                new Point3d(199980, 200100, 0),
                blockDefinitionId);
            currentSpace.AppendEntity(overlappingBlock);
            transaction.AddNewlyCreatedDBObject(overlappingBlock, true);

            var generatedFrameDefinition = new BlockTableRecord { Name = "DC_FRAME_HOST_EXISTING" };
            var generatedFrameDefinitionId = blockTable.Add(generatedFrameDefinition);
            transaction.AddNewlyCreatedDBObject(generatedFrameDefinition, true);
            var generatedFrameGeometry = CreateRectangle(0, 0, 800, 600);
            generatedFrameDefinition.AppendEntity(generatedFrameGeometry);
            transaction.AddNewlyCreatedDBObject(generatedFrameGeometry, true);
            var existingGeneratedFrame = new BlockReference(
                new Point3d(202000, 200000, 0),
                generatedFrameDefinitionId);
            currentSpace.AppendEntity(existingGeneratedFrame);
            transaction.AddNewlyCreatedDBObject(existingGeneratedFrame, true);

            var pluginAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => string.Equals(
                    assembly.GetName().Name,
                    "YiZhiMao.Dc.Plugin",
                    StringComparison.Ordinal));
            var commandsType = pluginAssembly.GetType(
                "YiZhiMao.Dc.Plugin.FrameCommands",
                throwOnError: true)!;
            var collect = commandsType.GetMethod(
                "CollectContentFrames",
                BindingFlags.NonPublic | BindingFlags.Static) ??
                throw new MissingMethodException(commandsType.FullName, "CollectContentFrames");
            var result = collect.Invoke(
                null,
                [new[]
                {
                    ordinaryGeometry.ObjectId,
                    overlappingBlock.ObjectId,
                    existingGeneratedFrame.ObjectId
                }, transaction]) as IEnumerable ??
                throw new InvalidOperationException("Frame content grouping returned no enumerable result.");
            var frames = result.Cast<PrintFrame>().ToList();
            var expected = new PrintFrame(199980, 200000, 201000, 200700);
            var passed = frames.Count == 1 && frames[0] == expected;
            document.Editor.WriteMessage(
                $"\nDC_HOST_FRAME_CONTENT_{(passed ? "PASS" : "FAIL")} count={frames.Count} frames={string.Join("|", frames.Select(FormatFrame))}");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            WriteError(document, exception.InnerException, "DC_HOST_FRAME_CONTENT_ERROR");
        }
        catch (System.Exception exception)
        {
            WriteError(document, exception, "DC_HOST_FRAME_CONTENT_ERROR");
        }
    }

    [CommandMethod("DC_TEST_DIM_STYLE_SYNC", CommandFlags.Modal)]
    public static void SynchronizeDimensionStyles()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var prompt = document.Editor.GetString(new Autodesk.AutoCAD.EditorInput.PromptStringOptions(
                "\nDimension style source DWG: ")
            {
                AllowSpaces = true
            });
            if (prompt.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                document.Editor.WriteMessage("\nDC_HOST_DIM_STYLE_SYNC_FAIL canceled");
                return;
            }

            var pluginAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly => string.Equals(
                    assembly.GetName().Name,
                    "YiZhiMao.Dc.Plugin",
                    StringComparison.Ordinal));
            var serviceType = pluginAssembly.GetType(
                "YiZhiMao.Dc.Plugin.DimensionStyleSyncService",
                throwOnError: true)!;
            var synchronize = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method =>
                    method.Name == "TrySynchronize" &&
                    method.GetParameters().Length == 4 &&
                    method.GetParameters()[1].ParameterType == typeof(string));
            object?[] arguments = [document, prompt.StringResult, "雷神标注", null];
            var succeeded = (bool)(synchronize.Invoke(null, arguments) ?? false);

            using var transaction = document.Database.TransactionManager.StartOpenCloseTransaction();
            var table = (DimStyleTable)transaction.GetObject(
                document.Database.DimStyleTableId,
                OpenMode.ForRead);
            var targetStyleExists = table.Has("雷神标注");
            var marker = succeeded && targetStyleExists ? "PASS" : "FAIL";
            document.Editor.WriteMessage(
                $"\nDC_HOST_DIM_STYLE_SYNC_{marker} message={arguments[3]}");
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            WriteError(document, exception.InnerException, "DC_HOST_DIM_STYLE_SYNC_ERROR");
        }
        catch (System.Exception exception)
        {
            WriteError(document, exception, "DC_HOST_DIM_STYLE_SYNC_ERROR");
        }
    }

    private static Polyline CreateRectangle(double minX, double minY, double maxX, double maxY)
    {
        var polyline = new Polyline(4) { Closed = true };
        polyline.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
        return polyline;
    }

    private static string FormatFrame(PrintFrame frame) => string.Join(",",
        frame.MinX.ToString("0.###", CultureInfo.InvariantCulture),
        frame.MinY.ToString("0.###", CultureInfo.InvariantCulture),
        frame.MaxX.ToString("0.###", CultureInfo.InvariantCulture),
        frame.MaxY.ToString("0.###", CultureInfo.InvariantCulture));

    private static void WriteError(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        System.Exception exception,
        string prefix = "DC_HOST_FRAME_SCAN_ERROR")
    {
        var message = exception.Message.Replace('\r', ' ').Replace('\n', ' ');
        document.Editor.WriteMessage($"\n{prefix} type={exception.GetType().Name} message={message}");
    }
}

using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Runtime.InteropServices;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

internal static class AdvancedBoardRuntimeSettings
{
    public static BoardToolOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.Board;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.Board = value);
    }

    public static void Save(BoardToolOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.Board = value);

    public static double AbaFirstThickness => Current.EffectiveAbaFirstThickness;
    public static double AbaSecondThickness => Current.EffectiveAbaSecondThickness;
    public static double AbaThirdThickness => Current.EffectiveAbaThirdThickness;
    public static double AbaFirstDepth => Current.EffectiveAbaFirstDepth;
    public static double AbaSecondDepth => Current.EffectiveAbaSecondDepth;
    public static double AbaThirdDepth => Current.EffectiveAbaThirdDepth;
    public static double AbaPlacementInset => Current.AbaPlacementInset;
    public static double DisplaceDistance => Current.DisplaceDistance;
    public static int DivideCount => Current.DivideCount;
    public static double DivideDepth => Current.DivideDepth;
    public static double DivideMaximumClearSpan => Current.DivideMaximumClearSpan;
    public static bool DivideSmartMode => Current.DivideSmartMode;
    public static bool DivideRemainderAtEnd => Current.DivideRemainderAtEnd;
    public static double BackboardThickness => Current.BackboardThickness;
    public static double BackboardInset => Current.BackboardInset;
    public static double BackboardLeftExtension => Current.BackboardLeftExtension;
    public static double BackboardRightExtension => Current.BackboardRightExtension;
    public static double BackboardTopExtension => Current.BackboardTopExtension;
    public static double BackboardBottomExtension => Current.BackboardBottomExtension;
    public static bool BackboardGeneratePlan => Current.BackboardGeneratePlan;
    public static bool BackboardGenerateElevation => Current.BackboardGenerateElevation;
    public static bool BackboardHideElevationLayer => Current.BackboardHideElevationLayer;
}

public static class AdvancedBoardCommands
{
    private const string StructureLayerName = "0-柜体结构";
    private const string BackboardPlanLayerName = "0-背板-俯视辅助图";
    private const string BackboardElevationLayerName = "0-正视图背板";
    private const string ProjectionLayerName = "0-层立板投影";
    private const double Tolerance = 0.000001;
    private const int ClipboardBusyErrorCode = unchecked((int)0x800401D0);

    [CommandMethod("DC_CMD_BOARD_DISPLACE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "位移板件", "DCYC")]
    public static void DisplaceBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !BoardCommands.TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var distanceOptions = new PromptDoubleOptions(
            $"\n请输入板边到参照内空边的内退距离 <{AdvancedBoardRuntimeSettings.DisplaceDistance:0.###}>: ")
        {
            AllowNegative = false,
            AllowZero = false,
            DefaultValue = AdvancedBoardRuntimeSettings.DisplaceDistance,
            UseDefaultValue = true
        };
        var distanceResult = document.Editor.GetDouble(distanceOptions);
        if (distanceResult.Status != PromptStatus.OK)
        {
            return;
        }

        var distance = distanceResult.Value;

        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = center - Vector3d.YAxis *
            Math.Max(bounds.Top - bounds.Bottom, BoardRuntimeSettings.Thickness);
        var jig = new DisplaceBoardPreviewJig(
            bounds,
            defaultCursor,
            distance,
            BoardRuntimeSettings.Thickness);
        var dragResult = document.Editor.Drag(jig);
        if (dragResult.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetPlacement(out var placement))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 位移板预览计算失败：{jig.LastErrorMessage ?? "内退距离与当前内空不匹配"}");
            return;
        }

        BoardCommands.WritePlacement(
            document.Database,
            placement.Layout,
            BoardRuntimeSettings.PropertyType,
            -BoardRuntimeSettings.CenterDepth,
            0);
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var side = placement.Side switch
        {
            BoardOffsetSide.Left => "左侧",
            BoardOffsetSide.Right => "右侧",
            BoardOffsetSide.Bottom => "下侧",
            _ => "上侧"
        };
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 位移板已按{source}和鼠标方向生成；距{side}内空边 {distance:0.###}。" );
    }

    [CommandMethod("DC_CMD_BOARD_ABA", CommandFlags.Modal)]
    [ShortcutCommand("板件", "生成ABA夹层", "DCABA")]
    public static void DrawAbaBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !BoardCommands.TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var spaceInsetOptions = new PromptDoubleOptions(
            $"\n请输入ABA夹心层板到所选内空边的空间内退 <{AdvancedBoardRuntimeSettings.AbaPlacementInset:0.###}>: ")
        {
            AllowNegative = false,
            AllowZero = true,
            DefaultValue = AdvancedBoardRuntimeSettings.AbaPlacementInset,
            UseDefaultValue = true
        };
        var spaceInsetResult = document.Editor.GetDouble(spaceInsetOptions);
        if (spaceInsetResult.Status != PromptStatus.OK)
        {
            return;
        }

        var layerLayout = AbaBoardLayoutCalculator.Calculate(
            AdvancedBoardRuntimeSettings.AbaFirstThickness,
            AdvancedBoardRuntimeSettings.AbaFirstDepth,
            AdvancedBoardRuntimeSettings.AbaSecondThickness,
            AdvancedBoardRuntimeSettings.AbaSecondDepth,
            AdvancedBoardRuntimeSettings.AbaThirdThickness,
            AdvancedBoardRuntimeSettings.AbaThirdDepth);
        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = center - Vector3d.YAxis *
            Math.Max(bounds.Top - bounds.Bottom, layerLayout.TotalThickness);
        var jig = new AbaBoardPreviewJig(
            bounds,
            defaultCursor,
            spaceInsetResult.Value,
            layerLayout);
        var dragResult = document.Editor.Drag(jig);
        if (dragResult.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetPlacement(out var placement))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] ABA夹心层板预览计算失败：{jig.LastErrorMessage ?? "空间内退与当前内空不匹配"}");
            return;
        }

        var start = new Point3d(placement.Start.X, placement.Start.Y, 0);
        var end = new Point3d(placement.End.X, placement.End.Y, 0);
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (var layer in layerLayout.Layers)
        {
            var plate = AppendPlate(
                space,
                transaction,
                start + normal * layer.CenterOffset,
                end + normal * layer.CenterOffset,
                layer.Thickness,
                layer.Depth,
                StructureLayerName,
                -layer.FrontOffset);
            BoardCommands.SetBoardProperty(
                document.Database,
                transaction,
                plate,
                "夹心层板",
                $"第{GetChineseLayerNumber(layer.LayerNumber)}层板");
        }

        transaction.Commit();
        var layerSummary = string.Join("；", layerLayout.Layers.Select(layer =>
            $"第{GetChineseLayerNumber(layer.LayerNumber)}层 {layer.Thickness:0.###}×{layer.Depth:0.###}" +
            (layerLayout.MaximumDepth - layer.Depth > Tolerance
                ? $"，相对深度内退 {layerLayout.MaximumDepth - layer.Depth:0.###}"
                : string.Empty)));
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var side = placement.Side switch
        {
            BoardOffsetSide.Left => "左侧",
            BoardOffsetSide.Right => "右侧",
            BoardOffsetSide.Bottom => "下侧",
            _ => "上侧"
        };
        document.Editor.WriteMessage(
            $"\n[一只猫dc] ABA夹心层板已按{source}和鼠标方向生成：距{side}内空边 {spaceInsetResult.Value:0.###}；" +
            $"{layerSummary}；三层后端对齐。");
    }

    [CommandMethod("DC_CMD_BOARD_DIVIDE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "均分板件", "DCFC")]
    public static void DivideBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !BoardCommands.TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var compartmentCount = Math.Max(2, AdvancedBoardRuntimeSettings.DivideCount);
        var mode = AdvancedBoardRuntimeSettings.DivideSmartMode
            ? BoardDivisionMode.SmartMaximumClearSpan
            : BoardDivisionMode.CompartmentCount;
        if (mode == BoardDivisionMode.CompartmentCount)
        {
            var countOptions = new PromptIntegerOptions(
                $"\n请输入均分格数 <{compartmentCount}>: ")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = compartmentCount,
                UseDefaultValue = true
            };
            var countResult = document.Editor.GetInteger(countOptions);
            if (countResult.Status != PromptStatus.OK)
            {
                return;
            }

            if (countResult.Value < 2)
            {
                document.Editor.WriteMessage("\n[一只猫dc] 按格数分时格数至少为 2。");
                return;
            }

            compartmentCount = countResult.Value;
        }

        var divisionOptions = new BoardDivisionOptions(
            mode,
            BoardRuntimeSettings.Thickness,
            AdvancedBoardRuntimeSettings.DivideRemainderAtEnd,
            AdvancedBoardRuntimeSettings.DivideMaximumClearSpan,
            compartmentCount);
        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = center + Vector3d.XAxis *
            Math.Max(bounds.Right - bounds.Left, BoardRuntimeSettings.Thickness);
        var jig = new DivideBoardPreviewJig(bounds, defaultCursor, divisionOptions);
        var dragResult = document.Editor.Drag(jig);
        if (dragResult.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetPlacement(out var placement))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 均分板件预览计算失败：{jig.LastErrorMessage ?? "当前参数与内空不匹配"}");
            return;
        }

        if (placement.DividerCoordinates.Count == 0)
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 当前方向内空已不超过最大净空 {AdvancedBoardRuntimeSettings.DivideMaximumClearSpan:0.###}，无需增加均分板件。");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (var coordinate in placement.DividerCoordinates)
        {
            var start = placement.IsVertical
                ? new Point3d(coordinate, bounds.Bottom, 0)
                : new Point3d(bounds.Left, coordinate, 0);
            var end = placement.IsVertical
                ? new Point3d(coordinate, bounds.Top, 0)
                : new Point3d(bounds.Right, coordinate, 0);
            AppendPlate(
                space,
                transaction,
                start,
                end,
                BoardRuntimeSettings.Thickness,
                AdvancedBoardRuntimeSettings.DivideDepth,
                StructureLayerName);
        }

        transaction.Commit();
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var modeText = mode == BoardDivisionMode.SmartMaximumClearSpan
            ? $"最大净空 {AdvancedBoardRuntimeSettings.DivideMaximumClearSpan:0.###}"
            : $"指定 {placement.Layout.CompartmentCount} 格";
        var remainderSide = placement.RemainderSide switch
        {
            BoardOffsetSide.Left => "左侧",
            BoardOffsetSide.Right => "右侧",
            BoardOffsetSide.Bottom => "下侧",
            _ => "上侧"
        };
        var clearSpans = string.Join("/", placement.Layout.ClearSpans.Select(value => value.ToString("0.###")));
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 已按{source}和鼠标方向完成均分：{modeText}，" +
            $"生成 {placement.DividerCoordinates.Count} 块板，净空 {clearSpans}；" +
            (AdvancedBoardRuntimeSettings.DivideRemainderAtEnd
                ? $"余量留在{remainderSide}。"
                : "全部严格等分。"));
    }

    [CommandMethod("DC_CMD_BOARD_BACKBOARD", CommandFlags.Modal)]
    [ShortcutCommand("板件", "生成背板", "DCBB")]
    public static void DrawBackboard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !BoardCommands.TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var options = new BackboardLayoutOptions(
            AdvancedBoardRuntimeSettings.BackboardThickness,
            AdvancedBoardRuntimeSettings.BackboardInset,
            AdvancedBoardRuntimeSettings.BackboardLeftExtension,
            AdvancedBoardRuntimeSettings.BackboardRightExtension,
            AdvancedBoardRuntimeSettings.BackboardTopExtension,
            AdvancedBoardRuntimeSettings.BackboardBottomExtension);
        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = center + Vector3d.YAxis *
            Math.Max(bounds.Top - bounds.Bottom, options.Thickness);
        var dashedLinetype = TryEnsureDashedLinetype(document.Database);
        var jig = new BackboardPreviewJig(
            bounds,
            defaultCursor,
            options,
            AdvancedBoardRuntimeSettings.BackboardGeneratePlan,
            AdvancedBoardRuntimeSettings.BackboardGenerateElevation,
            dashedLinetype);
        var dragResult = document.Editor.Drag(jig);
        if (dragResult.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetLayout(out var layout))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 背板预览计算失败：{jig.LastErrorMessage ?? "当前缩进与背板厚度不适合所选内空"}");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        if (AdvancedBoardRuntimeSettings.BackboardGeneratePlan)
        {
            EnsureLayer(document.Database, transaction, BackboardPlanLayerName, 8);
            var plan = AppendPolygon(space, transaction, layout.PlanPolygon, 0, BackboardPlanLayerName);
            ApplyBackboardStyle(plan, dashedLinetype);
        }

        if (AdvancedBoardRuntimeSettings.BackboardGenerateElevation)
        {
            EnsureLayer(document.Database, transaction, BackboardElevationLayerName, 8);
            var elevation = AppendPolygon(
                space,
                transaction,
                layout.ElevationPolygon,
                -Math.Abs(options.Thickness),
                BackboardElevationLayerName);
            ApplyBackboardStyle(elevation, dashedLinetype);
            BoardCommands.SetBoardProperty(
                document.Database,
                transaction,
                elevation,
                "背板",
                "正视图背板");
            SetLayerOff(
                document.Database,
                transaction,
                BackboardElevationLayerName,
                AdvancedBoardRuntimeSettings.BackboardHideElevationLayer);
        }

        transaction.Commit();
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var rearSide = layout.RearSide switch
        {
            BoardOffsetSide.Left => "左侧",
            BoardOffsetSide.Right => "右侧",
            BoardOffsetSide.Bottom => "下侧",
            _ => "上侧"
        };
        var output = AdvancedBoardRuntimeSettings.BackboardGeneratePlan
            ? "俯视辅助图"
            : "正视拆单背板";
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 已按{source}生成{output}；鼠标指定{rearSide}为后沿，缩进 {options.Inset:0.###}。" );
    }

    [CommandMethod("DC_CMD_BOARD_SPLIT", CommandFlags.Modal)]
    [ShortcutCommand("板件", "分割板件", "DCFG")]
    public static void SplitBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            SplitBoardCore(document);
        }
        catch (ExternalException exception) when (exception.ErrorCode == ClipboardBusyErrorCode)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 剪贴板暂时被占用，本次点选未执行，请重新运行分割命令。" );
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 分割操作未完成：{exception.Message}" );
        }
    }

    private static void SplitBoardCore(Autodesk.AutoCAD.ApplicationServices.Document document)
    {
        if (!TrySelectRectangle(document.Editor, "请选择需要分割的矩形板件", out var objectId))
        {
            return;
        }

        BoardSpaceBounds bounds;
        using (var readTransaction = document.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var sourcePreview = (Polyline)readTransaction.GetObject(objectId, OpenMode.ForRead);
            if (!TryGetAxisAlignedBounds(sourcePreview, out var min, out var max))
            {
                document.Editor.WriteMessage("\n[一只猫dc] 仅支持由四条水平/竖直边组成的闭合矩形。");
                return;
            }

            bounds = new BoardSpaceBounds(min.X, max.X, min.Y, max.Y);
        }

        if (!TryGetSplitPoints(document.Editor, out var firstPoint, out var secondPoint))
        {
            return;
        }

        var layout = BoardGeometryCalculator.CalculateSplitLayoutFromTwoPoints(
            bounds,
            new BoardPlacementPoint(firstPoint.X, firstPoint.Y),
            new BoardPlacementPoint(secondPoint.X, secondPoint.Y));

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var source = (Polyline)transaction.GetObject(objectId, OpenMode.ForWrite);
        var space = (BlockTableRecord)transaction.GetObject(source.OwnerId, OpenMode.ForWrite);
        foreach (var partBounds in layout.Parts)
        {
            var part = AppendRectangle(
                space,
                transaction,
                partBounds.Left,
                partBounds.Bottom,
                partBounds.Right,
                partBounds.Top,
                source.Thickness,
                source.Layer);
            CopyEntityProperties(source, part);
        }

        source.Erase();
        transaction.Commit();
        var boardDirection = layout.Orientation == BoardSplitOrientation.Vertical ? "横向" : "竖向";
        document.Editor.WriteMessage(
            $"\n[一只猫dc] {boardDirection}板件已按 {layout.Coordinates.Count} 个分割位置生成 {layout.Parts.Count} 个独立矩形。" );
    }

    [CommandMethod("DC_CMD_BOARD_MERGE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "合并板件", "DCHB")]
    public static void MergeBoards()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var filter = new SelectionFilter([new TypedValue((int)DxfCode.Start, "LWPOLYLINE")]);
        var selection = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择两块相邻矩形板件: "
        }, filter);
        if (selection.Status != PromptStatus.OK || selection.Value.Count != 2)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 合并操作必须恰好选择两块矩形板件。");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var first = (Polyline)transaction.GetObject(selection.Value[0].ObjectId, OpenMode.ForWrite);
        var second = (Polyline)transaction.GetObject(selection.Value[1].ObjectId, OpenMode.ForWrite);
        if (!TryGetAxisAlignedBounds(first, out var firstMin, out var firstMax) ||
            !TryGetAxisAlignedBounds(second, out var secondMin, out var secondMax) ||
            !BoardGeometryCalculator.TryMergeBounds(
                new BoardSpaceBounds(firstMin.X, firstMax.X, firstMin.Y, firstMax.Y),
                new BoardSpaceBounds(secondMin.X, secondMax.X, secondMin.Y, secondMax.Y),
                out var mergedBounds) ||
            first.OwnerId != second.OwnerId ||
            first.Normal.GetAngleTo(second.Normal) > Tolerance ||
            !HaveCompatibleEntityStyle(first, second) ||
            !HaveMatchingXData(first, second) ||
            !BoardGeometryCalculator.HaveCompatibleMergeProperties(
                first.Layer,
                second.Layer,
                first.Thickness,
                second.Thickness,
                first.Elevation,
                second.Elevation))
        {
            document.Editor.WriteMessage("\n[一只猫dc] 只能合并完整边缘相接且图层、样式、深度、高程和板件属性一致的矩形板件。");
            return;
        }

        var space = (BlockTableRecord)transaction.GetObject(first.OwnerId, OpenMode.ForWrite);
        var merged = AppendRectangle(
            space,
            transaction,
            mergedBounds.Left,
            mergedBounds.Bottom,
            mergedBounds.Right,
            mergedBounds.Top,
            first.Thickness,
            first.Layer);
        CopyEntityProperties(first, merged);
        first.Erase();
        second.Erase();
        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] 两块板件已合并。");
    }

    [CommandMethod("DC_CMD_BOARD_EXTEND", CommandFlags.Modal)]
    [ShortcutCommand("板件", "延伸矩形", "DCEXX")]
    public static void ExtendBoard() => ExtendRectanglesToNearestBoundary();

    [CommandMethod("DC_CMD_BOARD_TRIM", CommandFlags.Modal)]
    [ShortcutCommand("板件", "修剪线或矩形", "DCTRR")]
    public static void TrimBoard() => TrimNearest();

    [CommandMethod("DC_CMD_BOARD_SHELF_PROJECT", CommandFlags.Modal)]
    [ShortcutCommand("板件", "生成层板侧视/立板俯视", "DCFS")]
    public static void ProjectShelf()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !TrySelectRectangle(document.Editor, "请选择正视图中的横向层板或竖向立板", out var objectId))
        {
            return;
        }

        BoardSpaceBounds sourceBounds;
        using (var readTransaction = document.Database.TransactionManager.StartOpenCloseTransaction())
        {
            var source = (Polyline)readTransaction.GetObject(objectId, OpenMode.ForRead);
            if (!TryGetAxisAlignedBounds(source, out var min, out var max))
            {
                document.Editor.WriteMessage("\n[一只猫dc] 投影参考板必须是水平或竖直的闭合矩形。");
                return;
            }

            sourceBounds = new BoardSpaceBounds(min.X, max.X, min.Y, max.Y);
        }

        var isShelf = sourceBounds.Right - sourceBounds.Left >= sourceBounds.Top - sourceBounds.Bottom;
        var from = document.Editor.GetPoint(isShelf
            ? "\n请指定侧视图横向深度范围起点: "
            : "\n请指定俯视图竖向深度范围起点: ");
        if (from.Status != PromptStatus.OK)
        {
            return;
        }

        var defaultCursor = isShelf
            ? from.Value + Vector3d.XAxis * Math.Max(sourceBounds.Right - sourceBounds.Left, 500)
            : from.Value + Vector3d.YAxis * Math.Max(sourceBounds.Top - sourceBounds.Bottom, 500);
        var dashedLinetype = TryEnsureDashedLinetype(document.Database);
        var jig = new BoardProjectionPreviewJig(sourceBounds, from.Value, defaultCursor, dashedLinetype);
        var drag = document.Editor.Drag(jig);
        if (drag.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetLayout(out var layout))
        {
            document.Editor.WriteMessage($"\n[一只猫dc] {jig.LastErrorMessage ?? "投影深度范围无效。"}");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        EnsureLayer(document.Database, transaction, ProjectionLayerName, 8);
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        var projection = AppendRectangle(
            space,
            transaction,
            layout.Bounds.Left,
            layout.Bounds.Bottom,
            layout.Bounds.Right,
            layout.Bounds.Top,
            0,
            ProjectionLayerName);
        ApplyProjectionStyle(projection, dashedLinetype);
        transaction.Commit();
        var viewName = layout.Kind == BoardProjectionKind.ShelfSideView ? "层板侧视" : "立板俯视";
        document.Editor.WriteMessage($"\n[一只猫dc] 已生成{viewName}投影；灰色8虚线，不参与拆单。");
    }

    private static void ExtendRectanglesToNearestBoundary()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editedCount = 0;
        while (true)
        {
            var sourceOptions = new PromptEntityOptions(
                "\n请单击矩形靠近需要延伸的一端，回车结束: ")
            {
                AllowNone = true
            };
            sourceOptions.SetRejectMessage("\n请选择闭合矩形多段线。" );
            sourceOptions.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult sourceResult;
            try
            {
                sourceResult = document.Editor.GetEntity(sourceOptions);
            }
            catch (ExternalException exception) when (exception.ErrorCode == ClipboardBusyErrorCode)
            {
                document.Editor.WriteMessage("\n[一只猫dc] 剪贴板暂时被占用，本次点选未执行，请重新点取。" );
                continue;
            }

            if (sourceResult.Status is PromptStatus.None or PromptStatus.Cancel)
            {
                break;
            }

            if (sourceResult.Status != PromptStatus.OK)
            {
                continue;
            }

            var segments = CadAxisAlignedSegmentReader.CollectCurrentSpace(document.Database);
            using var transaction = document.Database.TransactionManager.StartTransaction();
            var source = (Polyline)transaction.GetObject(sourceResult.ObjectId, OpenMode.ForWrite);
            if (!TryGetAxisAlignedBounds(source, out var min, out var max))
            {
                document.Editor.WriteMessage("\n[一只猫dc] 仅支持由四条水平/竖直边组成的闭合矩形。" );
                continue;
            }

            BoardRectangleEditLayout layout;
            try
            {
                layout = BoardGeometryCalculator.CalculateNearestRectangleExtension(
                    new BoardSpaceBounds(min.X, max.X, min.Y, max.Y),
                    new BoardPlacementPoint(sourceResult.PickedPoint.X, sourceResult.PickedPoint.Y),
                    segments);
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
            {
                document.Editor.WriteMessage($"\n[一只猫dc] {exception.Message}" );
                continue;
            }

            ApplyRectangleSide(source, min, max, layout.Side, layout.Bounds);
            transaction.Commit();
            editedCount++;
        }

        if (editedCount > 0)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 已延伸 {editedCount} 个矩形板件；仅改变板件长度。" );
        }
    }

    private static void TrimNearest()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editedCount = 0;
        while (true)
        {
            var sourceOptions = new PromptEntityOptions(
                "\n请点击需要修剪掉的直线段或矩形端部，回车结束: ")
            {
                AllowNone = true
            };
            sourceOptions.SetRejectMessage("\n请选择直线或闭合矩形多段线。");
            sourceOptions.AddAllowedClass(typeof(Line), true);
            sourceOptions.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult sourceResult;
            try
            {
                sourceResult = document.Editor.GetEntity(sourceOptions);
            }
            catch (ExternalException exception) when (exception.ErrorCode == ClipboardBusyErrorCode)
            {
                document.Editor.WriteMessage("\n[一只猫dc] 剪贴板暂时被占用，本次点选未执行，请重新点取。" );
                continue;
            }
            if (sourceResult.Status is PromptStatus.None or PromptStatus.Cancel)
            {
                break;
            }

            if (sourceResult.Status != PromptStatus.OK)
            {
                continue;
            }

            var segments = CadAxisAlignedSegmentReader.CollectCurrentSpace(document.Database);
            using var transaction = document.Database.TransactionManager.StartTransaction();
            var source = (Entity)transaction.GetObject(sourceResult.ObjectId, OpenMode.ForWrite);
            var changed = source switch
            {
                Line line => TrimLineToNearestBoundary(line, sourceResult.PickedPoint, segments),
                Polyline polyline => TrimRectangleToNearestBoundary(polyline, sourceResult.PickedPoint, segments),
                _ => false
            };
            if (!changed)
            {
                document.Editor.WriteMessage("\n[一只猫dc] 点击端内侧没有找到可用的最近交线，该对象未修改。");
                continue;
            }

            transaction.Commit();
            editedCount++;
        }

        if (editedCount > 0)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 已连续修剪 {editedCount} 个对象。" );
        }
    }

    private static bool TrimLineToNearestBoundary(
        Line source,
        Point3d pickedPoint,
        IReadOnlyList<AxisAlignedSegment> segments)
    {
        var sourceVector = source.EndPoint - source.StartPoint;
        BoardLineEditParameters parameters;
        try
        {
            parameters = BoardGeometryCalculator.CalculateNearestLineTrim(
                new BoardPlacementPoint(source.StartPoint.X, source.StartPoint.Y),
                new BoardPlacementPoint(source.EndPoint.X, source.EndPoint.Y),
                new BoardPlacementPoint(pickedPoint.X, pickedPoint.Y),
                segments);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return false;
        }

        var originalStart = source.StartPoint;
        source.StartPoint = originalStart + sourceVector * parameters.StartParameter;
        source.EndPoint = originalStart + sourceVector * parameters.EndParameter;
        return source.Length > Tolerance;
    }

    private static bool TrimRectangleToNearestBoundary(
        Polyline source,
        Point3d pickedPoint,
        IReadOnlyList<AxisAlignedSegment> segments)
    {
        if (!TryGetAxisAlignedBounds(source, out var min, out var max))
        {
            return false;
        }

        BoardRectangleEditLayout layout;
        try
        {
            layout = BoardGeometryCalculator.CalculateNearestRectangleTrim(
                new BoardSpaceBounds(min.X, max.X, min.Y, max.Y),
                new BoardPlacementPoint(pickedPoint.X, pickedPoint.Y),
                segments);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return false;
        }

        ApplyRectangleSide(source, min, max, layout.Side, layout.Bounds);
        return true;
    }

    private static void ApplyRectangleSide(
        Polyline source,
        Point3d min,
        Point3d max,
        BoardRectangleSide side,
        BoardSpaceBounds editedBounds)
    {
        var verticalSide = side is BoardRectangleSide.Left or BoardRectangleSide.Right;
        for (var index = 0; index < source.NumberOfVertices; index++)
        {
            var point = source.GetPoint2dAt(index);
            var onSide = side switch
            {
                BoardRectangleSide.Left => Math.Abs(point.X - min.X) <= Tolerance,
                BoardRectangleSide.Right => Math.Abs(point.X - max.X) <= Tolerance,
                BoardRectangleSide.Bottom => Math.Abs(point.Y - min.Y) <= Tolerance,
                _ => Math.Abs(point.Y - max.Y) <= Tolerance
            };
            if (onSide)
            {
                source.SetPointAt(index, verticalSide
                    ? new Point2d(
                        side == BoardRectangleSide.Left ? editedBounds.Left : editedBounds.Right,
                        point.Y)
                    : new Point2d(
                        point.X,
                        side == BoardRectangleSide.Bottom ? editedBounds.Bottom : editedBounds.Top));
            }
        }
    }

    private sealed class BoardProjectionPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds sourceBounds;
        private readonly Point3d depthStart;
        private readonly string? dashedLinetype;
        private Point3d cursorPoint;

        public BoardProjectionPreviewJig(
            BoardSpaceBounds sourceBounds,
            Point3d depthStart,
            Point3d defaultCursor,
            string? dashedLinetype)
        {
            this.sourceBounds = sourceBounds;
            this.depthStart = depthStart;
            this.dashedLinetype = dashedLinetype;
            cursorPoint = defaultCursor;
        }

        public string? LastErrorMessage { get; private set; }

        public bool TryGetLayout(out BoardProjectionLayout layout)
        {
            try
            {
                layout = BoardGeometryCalculator.CalculateProjection(
                    sourceBounds,
                    new BoardPlacementPoint(depthStart.X, depthStart.Y),
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y));
                LastErrorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                layout = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var isShelf = sourceBounds.Right - sourceBounds.Left >= sourceBounds.Top - sourceBounds.Bottom;
            var options = new JigPromptPointOptions(isShelf
                ? "\n移动鼠标指定侧视图横向深度范围，单击确认: "
                : "\n移动鼠标指定俯视图竖向深度范围，单击确认: ")
            {
                BasePoint = depthStart,
                UseBasePoint = true,
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            using var outline = new Polyline(4)
            {
                Closed = true,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
            };
            if (dashedLinetype is not null)
            {
                outline.Linetype = dashedLinetype;
            }

            outline.AddVertexAt(0, new Point2d(layout.Bounds.Left, layout.Bounds.Bottom), 0, 0, 0);
            outline.AddVertexAt(1, new Point2d(layout.Bounds.Right, layout.Bounds.Bottom), 0, 0, 0);
            outline.AddVertexAt(2, new Point2d(layout.Bounds.Right, layout.Bounds.Top), 0, 0, 0);
            outline.AddVertexAt(3, new Point2d(layout.Bounds.Left, layout.Bounds.Top), 0, 0, 0);
            draw.Geometry.Draw(outline);
            return true;
        }
    }

    private sealed class BackboardPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly BackboardLayoutOptions options;
        private readonly bool drawPlan;
        private readonly bool drawElevation;
        private readonly string? dashedLinetype;
        private readonly Point3d center;
        private Point3d cursorPoint;

        public BackboardPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            BackboardLayoutOptions options,
            bool drawPlan,
            bool drawElevation,
            string? dashedLinetype)
        {
            this.bounds = bounds;
            this.options = options;
            this.drawPlan = drawPlan;
            this.drawElevation = drawElevation;
            this.dashedLinetype = dashedLinetype;
            cursorPoint = defaultCursor;
            center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
        }

        public string? LastErrorMessage { get; private set; }

        public bool TryGetLayout(out BackboardLayout layout)
        {
            try
            {
                layout = BackboardLayoutCalculator.Calculate(
                    bounds,
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y),
                    options);
                LastErrorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                layout = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var prompt = new JigPromptPointOptions(
                "\n移动鼠标指定柜体后沿方向（上/下/左/右），单击确认: ")
            {
                BasePoint = center,
                UseBasePoint = true,
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(prompt);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            if (drawPlan)
            {
                using var plan = CreatePreviewPolygon(layout.PlanPolygon, dashedLinetype);
                draw.Geometry.Draw(plan);
            }

            if (drawElevation)
            {
                using var elevation = CreatePreviewPolygon(layout.ElevationPolygon, dashedLinetype);
                draw.Geometry.Draw(elevation);
            }

            return true;
        }

        private static Polyline CreatePreviewPolygon(BoardPlacementPolygon polygon, string? dashedLinetype)
        {
            var outline = new Polyline(polygon.Points.Count)
            {
                Closed = true,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
            };
            if (dashedLinetype is not null)
            {
                outline.Linetype = dashedLinetype;
            }

            for (var index = 0; index < polygon.Points.Count; index++)
            {
                var point = polygon.Points[index];
                outline.AddVertexAt(index, new Point2d(point.X, point.Y), 0, 0, 0);
            }

            return outline;
        }
    }

    private sealed class DivideBoardPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly BoardDivisionOptions options;
        private readonly Point3d center;
        private Point3d cursorPoint;

        public DivideBoardPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            BoardDivisionOptions options)
        {
            this.bounds = bounds;
            this.options = options;
            cursorPoint = defaultCursor;
            center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
        }

        public string? LastErrorMessage { get; private set; }

        public bool TryGetPlacement(out BoardDivisionPlacement placement)
        {
            try
            {
                placement = BoardDivisionCalculator.CalculatePlacement(
                    bounds,
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y),
                    options);
                LastErrorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                placement = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var prompt = new JigPromptPointOptions(
                "\n鼠标向左/右生成竖板，向上/下生成横板；余量留在鼠标方向，单击确认: ")
            {
                BasePoint = center,
                UseBasePoint = true,
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(prompt);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetPlacement(out var placement))
            {
                return true;
            }

            var halfThickness = options.Thickness / 2;
            foreach (var coordinate in placement.DividerCoordinates)
            {
                using var outline = new Polyline(4)
                {
                    Closed = true,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 3)
                };
                Point2d[] points = placement.IsVertical
                    ?
                    [
                        new Point2d(coordinate - halfThickness, bounds.Bottom),
                        new Point2d(coordinate + halfThickness, bounds.Bottom),
                        new Point2d(coordinate + halfThickness, bounds.Top),
                        new Point2d(coordinate - halfThickness, bounds.Top)
                    ]
                    :
                    [
                        new Point2d(bounds.Left, coordinate - halfThickness),
                        new Point2d(bounds.Right, coordinate - halfThickness),
                        new Point2d(bounds.Right, coordinate + halfThickness),
                        new Point2d(bounds.Left, coordinate + halfThickness)
                    ];
                for (var index = 0; index < points.Length; index++)
                {
                    outline.AddVertexAt(index, points[index], 0, 0, 0);
                }

                draw.Geometry.Draw(outline);
            }

            return true;
        }
    }

    private sealed class DisplaceBoardPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly double distance;
        private readonly double thickness;
        private readonly Point3d center;
        private Point3d cursorPoint;

        public DisplaceBoardPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            double distance,
            double thickness)
        {
            this.bounds = bounds;
            this.distance = distance;
            this.thickness = thickness;
            cursorPoint = defaultCursor;
            center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
        }

        public string? LastErrorMessage { get; private set; }

        public bool TryGetPlacement(out BoardOffsetPlacement placement)
        {
            try
            {
                placement = BoardPlacementCalculator.CalculateOffsetBoard(
                    bounds,
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y),
                    distance,
                    thickness);
                LastErrorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                placement = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions(
                "\n鼠标向上/下生成横板，向左/右生成竖板，单击确认: ")
            {
                BasePoint = center,
                UseBasePoint = true,
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetPlacement(out var placement))
            {
                return true;
            }

            foreach (var polygon in placement.Layout.Polygons)
            {
                using var outline = new Polyline(polygon.Points.Count)
                {
                    Closed = true,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 3)
                };
                for (var index = 0; index < polygon.Points.Count; index++)
                {
                    outline.AddVertexAt(
                        index,
                        new Point2d(polygon.Points[index].X, polygon.Points[index].Y),
                        0,
                        0,
                        0);
                }

                draw.Geometry.Draw(outline);
            }

            return true;
        }
    }

    private sealed class AbaBoardPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly double spaceInset;
        private readonly AbaBoardLayout layerLayout;
        private readonly Point3d center;
        private Point3d cursorPoint;

        public AbaBoardPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            double spaceInset,
            AbaBoardLayout layerLayout)
        {
            this.bounds = bounds;
            this.spaceInset = spaceInset;
            this.layerLayout = layerLayout;
            cursorPoint = defaultCursor;
            center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
        }

        public string? LastErrorMessage { get; private set; }

        public bool TryGetPlacement(out AbaBoardSpacePlacement placement)
        {
            try
            {
                placement = AbaBoardLayoutCalculator.CalculateSpacePlacement(
                    bounds,
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y),
                    spaceInset,
                    layerLayout.TotalThickness);
                LastErrorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                placement = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions(
                "\n鼠标向上/下生成横向ABA层板，向左/右生成竖向ABA层板，单击确认: ")
            {
                BasePoint = center,
                UseBasePoint = true,
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetPlacement(out var placement))
            {
                return true;
            }

            var start = new Point3d(placement.Start.X, placement.Start.Y, 0);
            var end = new Point3d(placement.End.X, placement.End.Y, 0);
            var direction = (end - start).GetNormal();
            var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);
            foreach (var layer in layerLayout.Layers)
            {
                var layerStart = start + normal * layer.CenterOffset;
                var layerEnd = end + normal * layer.CenterOffset;
                var halfThickness = normal * (layer.Thickness / 2);
                using var outline = new Polyline(4)
                {
                    Closed = true,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 3)
                };
                Point3d[] points =
                [
                    layerStart + halfThickness,
                    layerEnd + halfThickness,
                    layerEnd - halfThickness,
                    layerStart - halfThickness
                ];
                for (var index = 0; index < points.Length; index++)
                {
                    outline.AddVertexAt(index, new Point2d(points[index].X, points[index].Y), 0, 0, 0);
                }

                draw.Geometry.Draw(outline);
            }

            return true;
        }
    }

    private static bool TrySelectRectangle(Editor editor, string message, out ObjectId objectId)
    {
        objectId = ObjectId.Null;
        var options = new PromptEntityOptions($"\n{message}: ");
        options.SetRejectMessage("\n请选择闭合矩形多段线。");
        options.AddAllowedClass(typeof(Polyline), true);
        var result = editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        objectId = result.ObjectId;
        return true;
    }

    private static bool TryGetSplitPoints(Editor editor, out Point3d start, out Point3d end)
    {
        start = default;
        end = default;
        var first = editor.GetPoint("\n请选择第一个分割位置: ");
        if (first.Status != PromptStatus.OK)
        {
            return false;
        }

        var second = editor.GetPoint(new PromptPointOptions("\n请选择第二个分割位置: ")
        {
            BasePoint = first.Value,
            UseBasePoint = true,
            UseDashedLine = true
        });
        if (second.Status != PromptStatus.OK)
        {
            return false;
        }

        start = first.Value;
        end = second.Value;
        return true;
    }

    private static bool TryGetRectangle(Editor editor, string purpose, out Point3d min, out Point3d max)
    {
        min = default;
        max = default;
        var first = editor.GetPoint($"\n请选择{purpose}第一角点: ");
        if (first.Status != PromptStatus.OK)
        {
            return false;
        }

        var second = editor.GetCorner(new PromptCornerOptions($"\n请选择{purpose}对角点: ", first.Value));
        if (second.Status != PromptStatus.OK)
        {
            return false;
        }

        min = new Point3d(Math.Min(first.Value.X, second.Value.X), Math.Min(first.Value.Y, second.Value.Y), 0);
        max = new Point3d(Math.Max(first.Value.X, second.Value.X), Math.Max(first.Value.Y, second.Value.Y), 0);
        return max.X - min.X > Tolerance && max.Y - min.Y > Tolerance;
    }

    private static bool TryGetAxisAlignedBounds(Polyline polyline, out Point3d min, out Point3d max)
    {
        min = default;
        max = default;
        if (!polyline.Closed || polyline.NumberOfVertices != 4)
        {
            return false;
        }

        var points = Enumerable.Range(0, 4).Select(polyline.GetPoint2dAt).ToArray();
        for (var index = 0; index < points.Length; index++)
        {
            var next = points[(index + 1) % points.Length];
            if (Math.Abs(points[index].X - next.X) > Tolerance && Math.Abs(points[index].Y - next.Y) > Tolerance)
            {
                return false;
            }
        }

        min = new Point3d(points.Min(point => point.X), points.Min(point => point.Y), 0);
        max = new Point3d(points.Max(point => point.X), points.Max(point => point.Y), 0);
        return max.X - min.X > Tolerance && max.Y - min.Y > Tolerance;
    }

    private static Polyline AppendPlate(
        BlockTableRecord space,
        Transaction transaction,
        Point3d start,
        Point3d end,
        double thickness,
        double depth,
        string layer,
        double elevation = 0)
    {
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis) * (thickness / 2);
        var polyline = AppendPolyline(space, transaction,
            [start + normal, end + normal, end - normal, start - normal], -Math.Abs(depth), layer);
        polyline.Elevation = elevation;
        return polyline;
    }

    private static string GetChineseLayerNumber(int layerNumber) => layerNumber switch
    {
        1 => "一",
        2 => "二",
        3 => "三",
        _ => layerNumber.ToString()
    };

    private static Polyline AppendPolygon(
        BlockTableRecord space,
        Transaction transaction,
        BoardPlacementPolygon polygon,
        double thickness,
        string layer) => AppendPolyline(
        space,
        transaction,
        polygon.Points.Select(point => new Point3d(point.X, point.Y, 0)).ToArray(),
        thickness,
        layer);

    private static Polyline AppendRectangle(
        BlockTableRecord space,
        Transaction transaction,
        double left,
        double bottom,
        double right,
        double top,
        double thickness,
        string layer) => AppendPolyline(space, transaction,
        [
            new Point3d(left, bottom, 0),
            new Point3d(right, bottom, 0),
            new Point3d(right, top, 0),
            new Point3d(left, top, 0)
        ], thickness, layer);

    private static Polyline AppendPolyline(
        BlockTableRecord space,
        Transaction transaction,
        IReadOnlyList<Point3d> points,
        double thickness,
        string layer)
    {
        var polyline = new Polyline(points.Count)
        {
            Closed = true,
            Layer = layer,
            Thickness = thickness,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        for (var index = 0; index < points.Count; index++)
        {
            polyline.AddVertexAt(index, new Point2d(points[index].X, points[index].Y), 0, 0, 0);
        }

        space.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, true);
        return polyline;
    }

    private static void CopyEntityProperties(Polyline source, Polyline target)
    {
        target.SetPropertiesFrom(source);
        target.Thickness = source.Thickness;
        target.Elevation = source.Elevation;
        target.Normal = source.Normal;
        using var xdata = source.XData;
        if (xdata is not null)
        {
            target.XData = xdata;
        }
    }

    private static bool HaveCompatibleEntityStyle(Entity first, Entity second) =>
        first.Color.ColorMethod == second.Color.ColorMethod &&
        first.Color.ColorIndex == second.Color.ColorIndex &&
        first.LinetypeId == second.LinetypeId &&
        Math.Abs(first.LinetypeScale - second.LinetypeScale) <= Tolerance &&
        first.LineWeight == second.LineWeight;

    private static bool HaveMatchingXData(Entity first, Entity second)
    {
        using var firstData = first.GetXDataForApplication(BoardCommands.BoardPropertyAppName);
        using var secondData = second.GetXDataForApplication(BoardCommands.BoardPropertyAppName);
        if (firstData is null || secondData is null)
        {
            return firstData is null && secondData is null;
        }

        var firstValues = firstData.AsArray();
        var secondValues = secondData.AsArray();
        return firstValues.Length == secondValues.Length &&
               firstValues.Zip(secondValues, (left, right) =>
                       left.TypeCode == right.TypeCode && Equals(left.Value, right.Value))
                   .All(equal => equal);
    }

    private static void ApplyProjectionStyle(Polyline polyline, string? dashedLinetype)
    {
        polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
        if (dashedLinetype is not null)
        {
            polyline.Linetype = dashedLinetype;
        }
    }

    private static void ApplyBackboardStyle(Polyline polyline, string? dashedLinetype)
    {
        polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
        if (dashedLinetype is not null)
        {
            polyline.Linetype = dashedLinetype;
        }
    }

    private static string? TryEnsureDashedLinetype(Database database)
    {
        if (HasLinetype(database, "DASHED"))
        {
            return "DASHED";
        }

        foreach (var file in new[] { "acadiso.lin", "acad.lin" })
        {
            try
            {
                database.LoadLineTypeFile("DASHED", file);
            }
            catch
            {
                continue;
            }

            if (HasLinetype(database, "DASHED"))
            {
                return "DASHED";
            }
        }

        return null;
    }

    private static bool HasLinetype(Database database, string name)
    {
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
        return table.Has(name);
    }

    private static void EnsureLayer(Database database, Transaction transaction, string name, short colorIndex)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layers.Has(name))
        {
            return;
        }

        layers.UpgradeOpen();
        var layer = new LayerTableRecord
        {
            Name = name,
            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
        };
        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
    }

    private static void SetLayerOff(
        Database database,
        Transaction transaction,
        string name,
        bool isOff)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (!layers.Has(name))
        {
            return;
        }

        var layer = (LayerTableRecord)transaction.GetObject(layers[name], OpenMode.ForWrite);
        layer.IsOff = isOff;
    }
}

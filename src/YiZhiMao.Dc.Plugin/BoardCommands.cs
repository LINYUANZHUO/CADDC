using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

internal static class BoardRuntimeSettings
{
    public static BoardToolOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.Board;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.Board = value);
    }

    public static void Save(BoardToolOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.Board = value);

    public static double Thickness => Current.Thickness;
    public static double Depth => Current.Depth;
    public static double CenterDepth => Current.CenterDepth;
    public static double TagScale => Current.TagScale;
    public static string TagType => Current.TagType;
    public static string PropertyType => Current.PropertyType;
    public static double TrimFaceWidth => Current.TrimFaceWidth;
    public static double TrimAuxiliaryDepth => Current.TrimAuxiliaryDepth;
    public static string SideTrimSectionType => Current.SideTrimSectionType;
    public static string TopTrimSectionType => Current.TopTrimSectionType;
    public static string PlacementView => Current.PlacementView;
    public static double LineThickness => Current.LineThickness;
    public static bool DoubleAsRectangle => Current.DoubleAsRectangle;
    public static bool TripleAsRectangle => Current.TripleAsRectangle;
    public static double ConvertLineThickness => Current.ConvertLineThickness;
    public static bool ConvertDoubleAsRectangle => Current.ConvertDoubleAsRectangle;
    public static bool ConvertTripleAsRectangle => Current.ConvertTripleAsRectangle;
    public static double DoorPartitionTopExtension => Current.DoorPartitionTopExtension;
    public static double DoorPartitionBottomExtension => Current.DoorPartitionBottomExtension;
    public static double DoorPartitionMaximumWidth => Current.DoorPartitionMaximumWidth;
    public static int DoorPartitionDefaultCount => Current.DoorPartitionDefaultCount;
    public static bool DoorPartitionSmartMode => Current.DoorPartitionSmartMode;
    public static double ShoeShelfDepth => Current.ShoeShelfDepth;
    public static bool ShoeShelfSmartAngle => Current.ShoeShelfSmartAngle;
    public static double ShoeShelfAngleDegrees => Current.ShoeShelfAngleDegrees;
    public static double ShoeShelfTargetSpacing => Current.ShoeShelfTargetSpacing;
    public static double ShoeShelfTopReserve => Current.ShoeShelfTopReserve;
    public static double ShoeShelfBottomReserve => Current.ShoeShelfBottomReserve;
    public static double ShoeShelfFrontClearance => Current.ShoeShelfFrontClearance;
    public static double ShoeShelfRearClearance => Current.ShoeShelfRearClearance;
    public static double ShoeShelfStopperHeight => Current.ShoeShelfStopperHeight;
    public static bool ShoeShelfGenerateStopper => Current.ShoeShelfGenerateStopper;
    public static double ExtendStart => Current.ExtendStart;
    public static double ExtendEnd => Current.ExtendEnd;
    public static double WineRackDepth => Current.WineRackDepth;
    public static double CrossThickness => Current.CrossThickness;
    public static double DiagonalThickness => Current.DiagonalThickness;
    public static double WineCellWidth => Current.WineCellWidth;
    public static int WineRows => Current.WineRows;
    public static int WineColumns => Current.WineColumns;
}

public static class BoardCommands
{
    private const string StructureLayerName = "0-柜体结构";
    private const string TagLayerName = "0-板件标识图层";
    internal const string BoardPropertyAppName = "YIZHIMAO_DC_BOARD";
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_BOARD_DRAW", CommandFlags.Modal)]
    [ShortcutCommand("板件", "绘制板件", "DCCB")]
    public static void DrawBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        if (BoardRuntimeSettings.PropertyType != "普通板件")
        {
            DrawPlacedBoard(document);
            return;
        }

        var editor = document.Editor;
        var first = editor.GetPoint("\n请选择板件起点: ");
        if (first.Status != PromptStatus.OK)
        {
            return;
        }

        var second = editor.GetPoint(new PromptPointOptions("\n请选择板件终点: ")
        {
            BasePoint = first.Value,
            UseBasePoint = true,
            UseDashedLine = true
        });
        if (second.Status != PromptStatus.OK || first.Value.DistanceTo(second.Value) <= Tolerance)
        {
            return;
        }

        WriteBoard(document.Database, first.Value, second.Value, BoardRuntimeSettings.Thickness, BoardRuntimeSettings.Depth, true);
        editor.WriteMessage("\n[一只猫dc] 板件已生成。" );
    }

    private static void DrawPlacedBoard(Autodesk.AutoCAD.ApplicationServices.Document document)
    {
        switch (BoardRuntimeSettings.PropertyType)
        {
            case "侧收口":
                DrawSideTrim(document);
                break;
            case "顶收口":
                DrawTopTrim(document);
                break;
            case "见光板":
                DrawVisiblePanel(document);
                break;
        }
    }

    private static void DrawSideTrim(Autodesk.AutoCAD.ApplicationServices.Document document)
    {
        if (!TryPromptPlacementView(document.Editor, out var isPlan))
        {
            return;
        }

        if (isPlan)
        {
            DrawPlanTrimSection(document, "侧收口", BoardRuntimeSettings.SideTrimSectionType, false);
            return;
        }

        if (!TryPromptOrthogonalBaseline(document.Editor, "立面侧收口", out var start, out var end))
        {
            return;
        }

        var edgeJig = CreateEdgeJig(start, end, BoardRuntimeSettings.TrimFaceWidth, false, "立面侧收口");
        if (!TryDragPlacement(document.Editor, edgeJig, out var elevationLayout))
        {
            return;
        }

        WritePlacement(document.Database, elevationLayout, "侧收口", -BoardRuntimeSettings.Thickness, -1);
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 立面侧收口已按正面宽度 {BoardRuntimeSettings.TrimFaceWidth:0.###} 生成。");
    }

    private static void DrawTopTrim(Autodesk.AutoCAD.ApplicationServices.Document document)
    {
        if (!TryPromptTopTrimView(document.Editor, out var isSideView))
        {
            return;
        }

        if (isSideView)
        {
            DrawPlanTrimSection(document, "顶收口", BoardRuntimeSettings.TopTrimSectionType, true);
            return;
        }

        if (!TryPromptOrthogonalBaseline(document.Editor, "顶收口", out var start, out var end))
        {
            return;
        }

        var jig = CreateEdgeJig(start, end, BoardRuntimeSettings.TrimFaceWidth, true, "顶收口");
        if (!TryDragPlacement(document.Editor, jig, out var layout))
        {
            return;
        }

        WritePlacement(document.Database, layout, "顶收口", -BoardRuntimeSettings.Thickness, -1);
        document.Editor.WriteMessage("\n[一只猫dc] 顶收口已生成，默认预览方向为柜体上方。");
    }

    private static void DrawVisiblePanel(Autodesk.AutoCAD.ApplicationServices.Document document)
    {
        if (!TryPromptPlacementView(document.Editor, out var isPlan))
        {
            return;
        }

        if (!isPlan)
        {
            if (!TryPromptOrthogonalBaseline(document.Editor, "立面见光板", out var start, out var end))
            {
                return;
            }

            var elevationJig = CreateEdgeJig(start, end, BoardRuntimeSettings.Thickness, false, "立面见光板");
            if (!TryDragPlacement(document.Editor, elevationJig, out var elevationLayout))
            {
                return;
            }

            WritePlacement(document.Database, elevationLayout, "见光板", -BoardRuntimeSettings.Depth, -1);
            document.Editor.WriteMessage("\n[一只猫dc] 立面见光板已按全局板厚生成。");
            return;
        }

        var anchorResult = document.Editor.GetPoint("\n请选择平面见光板的前口定位角点: ");
        if (anchorResult.Status != PromptStatus.OK)
        {
            return;
        }

        var directionOptions = new PromptPointOptions("\n请指定见光板18厚前口的生成方向: ")
        {
            BasePoint = anchorResult.Value,
            UseBasePoint = true,
            UseDashedLine = true
        };
        var directionResult = document.Editor.GetPoint(directionOptions);
        if (directionResult.Status != PromptStatus.OK ||
            anchorResult.Value.DistanceTo(directionResult.Value) <= Tolerance)
        {
            return;
        }

        var frontDirection = SnapDirection(anchorResult.Value, directionResult.Value);
        var defaultCursor = MidPoint(anchorResult.Value, frontDirection) +
                            PerpendicularUnit(anchorResult.Value, frontDirection) * BoardRuntimeSettings.Depth;
        var planJig = new BoardPlacementPreviewJig(
            defaultCursor,
            "\n请把鼠标移到见光板深度方向并单击确认: ",
            cursor => BoardPlacementCalculator.CalculatePlanVisiblePanel(
                ToPlacementPoint(anchorResult.Value),
                ToPlacementPoint(frontDirection),
                ToPlacementPoint(cursor),
                BoardRuntimeSettings.Depth,
                BoardRuntimeSettings.Thickness));
        if (!TryDragPlacement(document.Editor, planJig, out var planLayout))
        {
            return;
        }

        WritePlacement(document.Database, planLayout, "见光板", 0, -1);
        document.Editor.WriteMessage("\n[一只猫dc] 平面见光板已按全局板厚和柜体深度生成。");
    }

    private static void DrawPlanTrimSection(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        string propertyType,
        string configuredSectionType,
        bool isTopTrim)
    {
        var editor = document.Editor;
        var click = editor.GetPoint($"\n请选择{propertyType}起点: ");
        if (click.Status != PromptStatus.OK)
        {
            return;
        }

        var anchor = ToPlacementPoint(click.Value);
        var lineDescription = "未识别到起点侧线";
        var defaultHorizontalSign = 1d;
        var segments = CadAxisAlignedSegmentReader.CollectCurrentSpace(document.Database);
        if (TrimStartDetector.TryDetect(
                anchor,
                segments,
                BoardRuntimeSettings.Thickness,
                out var startContext))
        {
            anchor = startContext.Anchor;
            var lineSideSign = startContext.LineSide == TrimStartLineSide.Left ? -1d : 1d;
            defaultHorizontalSign = isTopTrim ? lineSideSign : -lineSideSign;
            lineDescription = $"起点{(startContext.LineSide == TrimStartLineSide.Left ? "左" : "右")}侧{(startContext.LineKind == TrimStartLineKind.Double ? "双线" : "单线")}";
        }

        var sectionType = ParseTrimSectionType(configuredSectionType);
        var anchorPoint = ToPoint3d(anchor);
        var defaultCursor = anchorPoint + new Vector3d(
            defaultHorizontalSign * Math.Max(BoardRuntimeSettings.TrimFaceWidth, BoardRuntimeSettings.Thickness) * 2,
            Math.Max(BoardRuntimeSettings.TrimAuxiliaryDepth, BoardRuntimeSettings.Thickness) * 2,
            0);
        var jig = new BoardPlacementPreviewJig(
            defaultCursor,
            "\n以起点十字线判断左上/右上/左下/右下，按 L/T/I 切换截面，单击确认: ",
            sectionType,
            anchorPoint,
            (cursor, currentSectionType) => isTopTrim
                ? BoardPlacementCalculator.CalculatePlanTopTrim(
                    anchor,
                    ToPlacementPoint(cursor),
                    BoardRuntimeSettings.TrimFaceWidth,
                    BoardRuntimeSettings.TrimAuxiliaryDepth,
                    BoardRuntimeSettings.Thickness,
                    currentSectionType)
                : BoardPlacementCalculator.CalculatePlanSideTrim(
                    anchor,
                    ToPlacementPoint(cursor),
                    BoardRuntimeSettings.TrimFaceWidth,
                    BoardRuntimeSettings.TrimAuxiliaryDepth,
                    BoardRuntimeSettings.Thickness,
                    currentSectionType));
        if (!TryDragTrimPlacement(editor, jig, out var layout))
        {
            return;
        }

        WritePlacement(document.Database, layout, propertyType, 0, -1);
        editor.WriteMessage(
            $"\n[一只猫dc] {propertyType}已按设置宽度 {BoardRuntimeSettings.TrimFaceWidth:0.###}、{jig.SectionType} 型生成；定位：{lineDescription}。");
    }

    private static bool TryPromptTopTrimView(Editor editor, out bool isSideView)
    {
        isSideView = false;
        var options = new PromptKeywordOptions("\n请选择顶封板视图 [正立面(E)/侧视图(S)] <正立面>: ")
        {
            AllowNone = true
        };
        options.Keywords.Add("Elevation", "E", "正立面(E)");
        options.Keywords.Add("Side", "S", "侧视图(S)");
        var result = editor.GetKeywords(options);
        if (result.Status == PromptStatus.Cancel)
        {
            return false;
        }

        if (result.Status is PromptStatus.OK or PromptStatus.Keyword)
        {
            isSideView = result.StringResult is "Side" or "S";
        }

        return result.Status is PromptStatus.OK or PromptStatus.Keyword or PromptStatus.None;
    }

    private static BoardTrimSectionType ParseTrimSectionType(string value) =>
        Enum.TryParse<BoardTrimSectionType>(value, true, out var sectionType)
            ? sectionType
            : BoardTrimSectionType.L;

    private static Point3d ToPoint3d(BoardPlacementPoint point) => new(point.X, point.Y, 0);

    private static bool TryPromptPlacementView(Editor editor, out bool isPlan)
    {
        isPlan = BoardRuntimeSettings.PlacementView == "平面";
        var options = new PromptKeywordOptions(
            $"\n请选择绘制视图 [立面(E)/平面(P)] <{(isPlan ? "平面" : "立面")}>: ")
        {
            AllowNone = true
        };
        options.Keywords.Add("Elevation", "E", "立面(E)");
        options.Keywords.Add("Plan", "P", "平面(P)");
        var result = editor.GetKeywords(options);
        if (result.Status == PromptStatus.Cancel)
        {
            return false;
        }

        if (result.Status is PromptStatus.OK or PromptStatus.Keyword)
        {
            isPlan = result.StringResult is "Plan" or "P";
            BoardRuntimeSettings.Save(BoardRuntimeSettings.Current with
            {
                PlacementView = isPlan ? "平面" : "立面"
            });
        }

        return result.Status is PromptStatus.OK or PromptStatus.Keyword or PromptStatus.None;
    }

    private static bool TryPromptOrthogonalBaseline(
        Editor editor,
        string purpose,
        out Point3d start,
        out Point3d end)
    {
        start = default;
        end = default;
        var first = editor.GetPoint($"\n请选择{purpose}基准边第一点: ");
        if (first.Status != PromptStatus.OK)
        {
            return false;
        }

        var second = editor.GetPoint(new PromptPointOptions($"\n请选择{purpose}基准边第二点: ")
        {
            BasePoint = first.Value,
            UseBasePoint = true,
            UseDashedLine = true
        });
        if (second.Status != PromptStatus.OK || first.Value.DistanceTo(second.Value) <= Tolerance)
        {
            return false;
        }

        start = first.Value;
        end = SnapDirection(first.Value, second.Value);
        return start.DistanceTo(end) > Tolerance;
    }

    private static BoardPlacementPreviewJig CreateEdgeJig(
        Point3d start,
        Point3d end,
        double width,
        bool preferWorldUp,
        string purpose)
    {
        var midpoint = MidPoint(start, end);
        var normal = preferWorldUp
            ? Vector3d.YAxis
            : PerpendicularUnit(start, end);
        var defaultCursor = midpoint + normal * width * 2;
        return new BoardPlacementPreviewJig(
            defaultCursor,
            $"\n请把鼠标移到{purpose}需要生成的一侧并单击确认: ",
            cursor => BoardPlacementCalculator.CalculateEdgeBoard(
                ToPlacementPoint(start),
                ToPlacementPoint(end),
                ToPlacementPoint(cursor),
                width));
    }

    private static bool TryDragPlacement(
        Editor editor,
        BoardPlacementPreviewJig jig,
        out BoardPlacementLayout layout)
    {
        layout = null!;
        var result = editor.Drag(jig);
        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        if (jig.TryGetLayout(out layout))
        {
            return true;
        }

        editor.WriteMessage($"\n[一只猫dc] 预览计算失败：{jig.LastErrorMessage ?? "参数或定位点无效"}");
        return false;
    }

    private static bool TryDragTrimPlacement(
        Editor editor,
        BoardPlacementPreviewJig jig,
        out BoardPlacementLayout layout)
    {
        layout = null!;
        using var hotkeys = new TrimSectionHotkeyScope(jig);
        var result = editor.Drag(jig);
        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        if (jig.TryGetLayout(out layout))
        {
            return true;
        }

        editor.WriteMessage($"\n[一只猫dc] 收口预览计算失败：{jig.LastErrorMessage ?? "参数或定位点无效"}");
        return false;
    }

    [CommandMethod("DC_CMD_BOARD_CENTER", CommandFlags.Modal)]
    [ShortcutCommand("板件", "中分板件", "DCZC")]
    public static void DrawCenterBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        document.Editor.WriteMessage(
            "\n[一只猫dc] 移动鼠标选择横向或竖向，按 H/V 可强制切换。" );

        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = bounds.Right - bounds.Left >= bounds.Top - bounds.Bottom
            ? center + Vector3d.XAxis * Math.Max(bounds.Right - bounds.Left, BoardRuntimeSettings.Thickness)
            : center + Vector3d.YAxis * Math.Max(bounds.Top - bounds.Bottom, BoardRuntimeSettings.Thickness);
        var jig = new CenterBoardPreviewJig(
            bounds,
            defaultCursor,
            BoardRuntimeSettings.Thickness);
        using var hotkeys = new CenterBoardHotkeyScope(jig);
        var result = document.Editor.Drag(jig);
        if (result.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetLayout(out var layout))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 中分板预览计算失败：{jig.LastErrorMessage ?? "参数或定位点无效"}");
            return;
        }

        WritePlacement(
            document.Database,
            layout,
            BoardRuntimeSettings.PropertyType,
            -BoardRuntimeSettings.CenterDepth,
            0);
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var direction = jig.Orientation == BoardCenterOrientation.Horizontal ? "横向" : "竖向";
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 中分板件已按{source}生成，方向：{direction}。" );
    }

    internal static bool TryGetBoardSpaceBounds(
        Autodesk.AutoCAD.ApplicationServices.Document document,
        out BoardSpaceBounds bounds,
        out bool automaticallyDetected)
    {
        bounds = default;
        automaticallyDetected = false;
        var options = new PromptPointOptions("\n请在柜体内空中单击 [手动框选(M)] <智能识别>: ");
        options.Keywords.Add("Manual", "M", "手动框选(M)");
        var result = document.Editor.GetPoint(options);
        if (result.Status == PromptStatus.Keyword &&
            result.StringResult is "Manual" or "M")
        {
            return TryGetManualCenterBoardBounds(document.Editor, out bounds);
        }

        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        var segments = CadAxisAlignedSegmentReader.CollectCurrentSpace(document.Database);
        var openingSegments = segments
            .Select(segment => new DoorOpeningSegment(
                segment.StartX,
                segment.StartY,
                segment.EndX,
                segment.EndY))
            .ToArray();
        if (DoorOpeningDetector.TryDetect(
                result.Value.X,
                result.Value.Y,
                openingSegments,
                out var opening))
        {
            bounds = new BoardSpaceBounds(opening.Left, opening.Right, opening.Bottom, opening.Top);
            automaticallyDetected = true;
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 已识别内空：{opening.Width:0.###} × {opening.Height:0.###}。" );
            return true;
        }

        document.Editor.WriteMessage("\n[一只猫dc] 点击位置未识别到完整内空，已切换为两点手动框选。" );
        return TryGetManualCenterBoardBounds(document.Editor, out bounds);
    }

    private static bool TryGetManualCenterBoardBounds(Editor editor, out BoardSpaceBounds bounds)
    {
        bounds = default;
        if (!TryGetRectangle(editor, "柜体内空", out var min, out var max))
        {
            return false;
        }

        bounds = new BoardSpaceBounds(min.X, max.X, min.Y, max.Y);
        return true;
    }

    [CommandMethod("DC_CMD_LINE_SINGLE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "单线偏移", "DC1")]
    public static void OffsetSingleLine() => OffsetSelectedLine(1, false);

    [CommandMethod("DC_CMD_LINE_DOUBLE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "双线偏移", "DC2")]
    public static void OffsetDoubleLine() => OffsetSelectedLine(2, BoardRuntimeSettings.DoubleAsRectangle);

    [CommandMethod("DC_CMD_LINE_TRIPLE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "三线偏移", "DC3")]
    public static void OffsetTripleLine() => OffsetSelectedLine(3, BoardRuntimeSettings.TripleAsRectangle);

    [CommandMethod("DC_CMD_LINE_TO_DOUBLE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "单线变双", "DC22")]
    public static void ConvertLineToDouble() => ConvertSelectedLines(2, BoardRuntimeSettings.ConvertDoubleAsRectangle);

    [CommandMethod("DC_CMD_LINE_TO_TRIPLE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "单线变三", "DC33")]
    public static void ConvertLineToTriple() => ConvertSelectedLines(3, BoardRuntimeSettings.ConvertTripleAsRectangle);

    [CommandMethod("DC_CMD_DOOR_PARTITION_GUIDE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "门板分区辅助", "FQ")]
    public static void DrawDoorPartitionGuides()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var first = document.Editor.GetPoint("\n请在横向基准线上点取分区起点: ");
        if (first.Status != PromptStatus.OK)
        {
            return;
        }

        var topExtension = BoardRuntimeSettings.DoorPartitionTopExtension;
        var bottomExtension = BoardRuntimeSettings.DoorPartitionBottomExtension;
        var initialDoorCount = BoardRuntimeSettings.DoorPartitionDefaultCount;
        if (!BoardRuntimeSettings.DoorPartitionSmartMode)
        {
            var countOptions = new PromptIntegerOptions(
                $"\n请输入门扇/分区数量 <{initialDoorCount}>: ")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = initialDoorCount,
                UseDefaultValue = true,
                LowerLimit = 1
            };
            var countResult = document.Editor.GetInteger(countOptions);
            if (countResult.Status != PromptStatus.OK)
            {
                return;
            }

            initialDoorCount = countResult.Value;
        }

        var defaultCursor = first.Value + Vector3d.XAxis *
            BoardRuntimeSettings.DoorPartitionMaximumWidth * initialDoorCount;
        var jig = new DoorPartitionPreviewJig(
            first.Value,
            defaultCursor,
            topExtension,
            bottomExtension,
            BoardRuntimeSettings.DoorPartitionSmartMode,
            BoardRuntimeSettings.DoorPartitionMaximumWidth,
            initialDoorCount);
        using var hotkeys = new DoorPartitionHotkeyScope(jig);
        var dragResult = document.Editor.Drag(jig);
        if (dragResult.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetLayout(out var layout, out var left))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 门板分区计算失败：{jig.LastErrorMessage ?? "起点与终点不能重合"}");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (var offset in layout.BoundaryOffsets)
        {
            var x = left + offset;
            AppendLine(
                space,
                transaction,
                new Point3d(x, first.Value.Y - bottomExtension, first.Value.Z),
                new Point3d(x, first.Value.Y + topExtension, first.Value.Z));
        }

        transaction.Commit();
        var mode = BoardRuntimeSettings.DoorPartitionSmartMode ? "智能分区" : "按格数分";
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 已按{mode}生成 {layout.DoorCount} 扇、{layout.BoundaryOffsets.Count} 条竖向辅助线。");
    }

    [CommandMethod("DC_CMD_SHOE_SHELVES", CommandFlags.Modal)]
    [ShortcutCommand("板件", "鞋柜斜层板", "DCXB")]
    public static void DrawShoeShelves()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var options = new ShoeShelfLayoutOptions(
            BoardRuntimeSettings.Thickness,
            BoardRuntimeSettings.ShoeShelfDepth,
            BoardRuntimeSettings.ShoeShelfSmartAngle,
            BoardRuntimeSettings.ShoeShelfAngleDegrees,
            BoardRuntimeSettings.ShoeShelfTargetSpacing,
            BoardRuntimeSettings.ShoeShelfTopReserve,
            BoardRuntimeSettings.ShoeShelfBottomReserve,
            BoardRuntimeSettings.ShoeShelfFrontClearance,
            BoardRuntimeSettings.ShoeShelfRearClearance,
            BoardRuntimeSettings.ShoeShelfStopperHeight,
            BoardRuntimeSettings.ShoeShelfGenerateStopper,
            0);
        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = new Point3d(bounds.Right + (bounds.Right - bounds.Left), center.Y, 0);
        ShoeShelfPreviewResolution initialPreview;
        try
        {
            initialPreview = ShoeShelfLayoutCalculator.ResolvePreview(
                bounds,
                new BoardPlacementPoint(defaultCursor.X, defaultCursor.Y),
                options);
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 当前参数无法生成鞋柜斜层板：{exception.Message}");
            return;
        }

        options = initialPreview.EffectiveOptions;
        if (initialPreview.UsedSmartFallback)
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 固定 {BoardRuntimeSettings.ShoeShelfAngleDegrees:0.##}° 无法放入当前柜深，已自动切换智能角度。" );
        }

        var jig = new ShoeShelfPreviewJig(bounds, defaultCursor, options);
        document.Editor.WriteMessage(
            "\n[一只猫dc] 鼠标向左/右选择柜前方向；W/+增加一层，S/-减少一层。" );
        PromptResult dragResult;
        try
        {
            using var hotkeys = new ShoeShelfHotkeyScope(jig);
            dragResult = document.Editor.Drag(jig);
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 鞋柜斜层板预览已安全取消：{exception.Message}");
            return;
        }

        if (dragResult.Status != PromptStatus.OK)
        {
            return;
        }

        if (!jig.TryGetLayout(out var layout))
        {
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 鞋柜斜层板计算失败：{jig.LastErrorMessage ?? "参数或柜体内空无效"}");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (var layer in layout.Layers)
        {
            var shelf = AppendShoeShelfPart(space, transaction, layer.Shelf, BoardRuntimeSettings.Depth);
            SetBoardProperty(document.Database, transaction, shelf, "普通板件", "鞋柜斜层板");
            if (layer.Stopper is not null)
            {
                var stopper = AppendShoeShelfPart(space, transaction, layer.Stopper, BoardRuntimeSettings.Depth);
                SetBoardProperty(document.Database, transaction, stopper, "普通板件", "鞋柜前挡条");
            }
        }

        transaction.Commit();
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var front = layout.FrontSide == ShoeCabinetFrontSide.Left ? "左前方" : "右前方";
        var stopperText = BoardRuntimeSettings.ShoeShelfGenerateStopper ? "，已生成前挡条" : string.Empty;
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 已按{source}生成 {layout.Layers.Count} 层鞋柜斜层板，{front}、角度 {layout.AngleDegrees:0.##}°{stopperText}。" );
    }

    [CommandMethod("DC_CMD_WINE_RACK_CROSS", CommandFlags.Modal)]
    public static void DrawCrossWineRack()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TryGetRectangle(document.Editor, "十字酒架范围", out var min, out var max))
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        for (var row = 1; row <= BoardRuntimeSettings.WineRows; row++)
        {
            var y = min.Y + (max.Y - min.Y) * row / (BoardRuntimeSettings.WineRows + 1d);
            AppendPlate(space, transaction,
                new Point3d(min.X, y, 0), new Point3d(max.X, y, 0),
                BoardRuntimeSettings.CrossThickness, BoardRuntimeSettings.WineRackDepth);
        }

        for (var column = 1; column <= BoardRuntimeSettings.WineColumns; column++)
        {
            var x = min.X + (max.X - min.X) * column / (BoardRuntimeSettings.WineColumns + 1d);
            AppendPlate(space, transaction,
                new Point3d(x, min.Y, 0), new Point3d(x, max.Y, 0),
                BoardRuntimeSettings.CrossThickness, BoardRuntimeSettings.WineRackDepth);
        }

        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] 十字酒架已生成。" );
    }

    [CommandMethod("DC_CMD_WINE_RACK_DIAGONAL", CommandFlags.Modal)]
    public static void DrawDiagonalWineRack()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var origin = document.Editor.GetPoint("\n请选择斜酒架左下起点: ");
        if (origin.Status != PromptStatus.OK)
        {
            return;
        }

        var pitch = (BoardRuntimeSettings.WineCellWidth + BoardRuntimeSettings.DiagonalThickness) * Math.Sqrt(2);
        var width = Math.Max(1, BoardRuntimeSettings.WineColumns) * pitch;
        var height = Math.Max(1, BoardRuntimeSettings.WineRows) * pitch;
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        for (var index = 0; index <= BoardRuntimeSettings.WineColumns + BoardRuntimeSettings.WineRows; index++)
        {
            var offset = (index - BoardRuntimeSettings.WineRows) * pitch;
            if (TryClipDiagonal(origin.Value.X, origin.Value.Y, width, height, offset, true, out var start, out var end))
            {
                AppendPlate(space, transaction, start, end, BoardRuntimeSettings.DiagonalThickness, BoardRuntimeSettings.WineRackDepth);
            }

            if (TryClipDiagonal(origin.Value.X, origin.Value.Y, width, height, offset, false, out start, out end))
            {
                AppendPlate(space, transaction, start, end, BoardRuntimeSettings.DiagonalThickness, BoardRuntimeSettings.WineRackDepth);
            }
        }

        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] 45°斜酒架已生成。" );
    }

    private static Polyline AppendShoeShelfPart(
        BlockTableRecord space,
        Transaction transaction,
        ShoeShelfPartLayout part,
        double depth) =>
        AppendClosedPolyline(
            space,
            transaction,
            part.Polygon.Points
                .Select(point => new Point3d(point.X, point.Y, 0))
                .ToArray(),
            -Math.Abs(depth));

    private static void OffsetSelectedLine(int lineCount, bool asRectangle)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null ||
            !TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var distanceResult = document.Editor.GetDouble(new PromptDoubleOptions("\n请输入偏移距离: ")
        {
            AllowNegative = false,
            AllowZero = false
        });
        if (distanceResult.Status != PromptStatus.OK)
        {
            return;
        }

        var center = new Point3d(
            (bounds.Left + bounds.Right) / 2,
            (bounds.Bottom + bounds.Top) / 2,
            0);
        var defaultCursor = center - Vector3d.YAxis *
            Math.Max(bounds.Top - bounds.Bottom, BoardRuntimeSettings.LineThickness);
        var jig = new LineOffsetPreviewJig(
            bounds,
            defaultCursor,
            lineCount,
            distanceResult.Value,
            BoardRuntimeSettings.LineThickness,
            asRectangle);
        if (document.Editor.Drag(jig).Status != PromptStatus.OK || !jig.TryGetPlacement(out var placement))
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        AppendOffsetLinePlacement(space, transaction, bounds, placement, asRectangle);
        transaction.Commit();

        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        var side = placement.Side switch
        {
            BoardOffsetSide.Left => "左侧",
            BoardOffsetSide.Right => "右侧",
            BoardOffsetSide.Bottom => "下侧",
            _ => "上侧"
        };
        document.Editor.WriteMessage(
            $"\n[一只猫dc] 已按{source}从{side}净距 {distanceResult.Value:0.###} 生成{lineCount}线。" );
    }

    private static void ConvertSelectedLines(int lineCount, bool asRectangle)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var filter = new SelectionFilter([new TypedValue((int)DxfCode.Start, "LINE")]);
        var selection = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择要转换的单线: "
        }, filter);
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (SelectedObject selected in selection.Value)
        {
            if (transaction.GetObject(selected.ObjectId, OpenMode.ForWrite) is not Line line)
            {
                continue;
            }

            var extended = BoardGeometryCalculator.ExtendLineByDrawingAxes(
                new BoardPlacementPoint(line.StartPoint.X, line.StartPoint.Y),
                new BoardPlacementPoint(line.EndPoint.X, line.EndPoint.Y),
                BoardRuntimeSettings.ExtendStart,
                BoardRuntimeSettings.ExtendEnd);
            var start = new Point3d(extended.Start.X, extended.Start.Y, line.StartPoint.Z);
            var end = new Point3d(extended.End.X, extended.End.Y, line.EndPoint.Z);
            AppendParallelGeometry(
                space,
                transaction,
                start,
                end,
                BoardGeometryCalculator.GetParallelLineOffsets(lineCount, BoardRuntimeSettings.ConvertLineThickness),
                asRectangle);
            line.Erase();
        }

        transaction.Commit();
    }

    private static void AppendOffsetLinePlacement(
        BlockTableRecord space,
        Transaction transaction,
        BoardSpaceBounds bounds,
        BoardOffsetLinePlacement placement,
        bool asRectangle)
    {
        if (asRectangle && placement.Coordinates.Count > 1)
        {
            var first = GetOffsetLineEndpoints(bounds, placement.Orientation, placement.Coordinates[0]);
            var last = GetOffsetLineEndpoints(
                bounds,
                placement.Orientation,
                placement.Coordinates[placement.Coordinates.Count - 1]);
            AppendClosedPolyline(space, transaction, [first.Start, first.End, last.End, last.Start], 0);
            if (placement.Coordinates.Count == 3)
            {
                var middle = GetOffsetLineEndpoints(bounds, placement.Orientation, placement.Coordinates[1]);
                AppendLine(space, transaction, middle.Start, middle.End);
            }

            return;
        }

        foreach (var coordinate in placement.Coordinates)
        {
            var line = GetOffsetLineEndpoints(bounds, placement.Orientation, coordinate);
            AppendLine(space, transaction, line.Start, line.End);
        }
    }

    private static (Point3d Start, Point3d End) GetOffsetLineEndpoints(
        BoardSpaceBounds bounds,
        BoardCenterOrientation orientation,
        double coordinate) =>
        orientation == BoardCenterOrientation.Horizontal
            ? (new Point3d(bounds.Left, coordinate, 0), new Point3d(bounds.Right, coordinate, 0))
            : (new Point3d(coordinate, bounds.Bottom, 0), new Point3d(coordinate, bounds.Top, 0));

    private static void AppendParallelGeometry(
        BlockTableRecord space,
        Transaction transaction,
        Point3d start,
        Point3d end,
        IReadOnlyList<double> offsets,
        bool asRectangle)
    {
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);
        if (offsets.Count == 1)
        {
            AppendLine(space, transaction, start + normal * offsets[0], end + normal * offsets[0]);
            return;
        }

        if (asRectangle)
        {
            var first = start + normal * offsets[0];
            var second = start + normal * offsets[offsets.Count - 1];
            var firstEnd = end + normal * offsets[0];
            var secondEnd = end + normal * offsets[offsets.Count - 1];
            AppendClosedPolyline(space, transaction, [first, firstEnd, secondEnd, second], 0);
            if (offsets.Count == 3)
            {
                AppendLine(space, transaction, start + normal * offsets[1], end + normal * offsets[1]);
            }
        }
        else
        {
            foreach (var offset in offsets)
            {
                AppendLine(space, transaction, start + normal * offset, end + normal * offset);
            }
        }
    }

    private static void WriteBoard(
        Database database,
        Point3d start,
        Point3d end,
        double thickness,
        double depth,
        bool addTag)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(database, transaction, StructureLayerName, 7);
        var plate = AppendPlate(space, transaction, start, end, thickness, depth);
        SetBoardProperty(database, transaction, plate, BoardRuntimeSettings.PropertyType);
        if (addTag)
        {
            AppendBoardTag(database, space, transaction, plate.GeometricExtents);
        }

        transaction.Commit();
    }

    private static Polyline AppendPlate(
        BlockTableRecord space,
        Transaction transaction,
        Point3d start,
        Point3d end,
        double thickness,
        double depth)
    {
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis) * (thickness / 2);
        return AppendClosedPolyline(space, transaction,
            [start + normal, end + normal, end - normal, start - normal], -Math.Abs(depth));
    }

    private static Polyline AppendClosedPolyline(
        BlockTableRecord space,
        Transaction transaction,
        IReadOnlyList<Point3d> points,
        double thickness)
    {
        var polyline = new Polyline(points.Count)
        {
            Closed = true,
            Layer = StructureLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256),
            Thickness = thickness
        };
        for (var index = 0; index < points.Count; index++)
        {
            polyline.AddVertexAt(index, new Point2d(points[index].X, points[index].Y), 0, 0, 0);
        }

        space.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, true);
        return polyline;
    }

    internal static void SetBoardProperty(
        Database database,
        Transaction transaction,
        Polyline polyline,
        string propertyType,
        string? partName = null,
        double? cutWidth = null,
        double? cutHeight = null,
        double? cutThickness = null)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (!table.Has(BoardPropertyAppName))
        {
            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = BoardPropertyAppName };
            table.Add(record);
            transaction.AddNewlyCreatedDBObject(record, true);
        }

        var values = new List<TypedValue>
        {
            new((int)DxfCode.ExtendedDataRegAppName, BoardPropertyAppName),
            new((int)DxfCode.ExtendedDataAsciiString, propertyType)
        };
        if (!string.IsNullOrWhiteSpace(partName))
        {
            values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, partName));
        }

        if (cutWidth.HasValue && cutHeight.HasValue && cutThickness.HasValue)
        {
            values.Add(new TypedValue(
                (int)DxfCode.ExtendedDataAsciiString,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "SIZE|{0:0.###}|{1:0.###}|{2:0.###}",
                    cutWidth.Value,
                    cutHeight.Value,
                    cutThickness.Value)));
        }

        using var xdata = new ResultBuffer(values.ToArray());
        polyline.XData = xdata;
    }

    private static void AppendLine(BlockTableRecord space, Transaction transaction, Point3d start, Point3d end)
    {
        var line = new Line(start, end)
        {
            Layer = StructureLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        space.AppendEntity(line);
        transaction.AddNewlyCreatedDBObject(line, true);
    }

    private static void AppendBoardTag(
        Database database,
        BlockTableRecord space,
        Transaction transaction,
        Extents3d extents)
    {
        if (string.Equals(BoardRuntimeSettings.TagType, "固定板件", StringComparison.Ordinal))
        {
            return;
        }

        EnsureLayer(database, transaction, TagLayerName, 6);
        var center = new Point3d(
            (extents.MinPoint.X + extents.MaxPoint.X) / 2,
            (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
            0);
        var scale = Compatibility.Clamp(BoardRuntimeSettings.TagScale, 0.1, 10);
        var radius = BoardRuntimeSettings.Thickness * scale;
        var markerBottomY = center.Y - radius;
        if (Compatibility.Contains(BoardRuntimeSettings.TagType, "玻璃层板", StringComparison.Ordinal))
        {
            var halfWidth = radius * Math.Sqrt(3) / 2;
            markerBottomY = center.Y - radius / 2;
            var triangle = new Polyline(3)
            {
                Layer = TagLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByLayer, 256),
                Closed = true
            };
            triangle.AddVertexAt(0, new Point2d(center.X, center.Y + radius), 0, 0, 0);
            triangle.AddVertexAt(1, new Point2d(center.X - halfWidth, markerBottomY), 0, 0, 0);
            triangle.AddVertexAt(2, new Point2d(center.X + halfWidth, markerBottomY), 0, 0, 0);
            space.AppendEntity(triangle);
            transaction.AddNewlyCreatedDBObject(triangle, true);
        }
        else
        {
            var circle = new Circle(center, Vector3d.ZAxis, radius)
            {
                Layer = TagLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
            };
            space.AppendEntity(circle);
            transaction.AddNewlyCreatedDBObject(circle, true);
        }

        if (!Compatibility.Contains(BoardRuntimeSettings.TagType, "文字", StringComparison.Ordinal))
        {
            return;
        }

        var textHeight = radius * 35 / 36;
        var textGap = radius / 3;
        var textAnchor = new Point3d(center.X, markerBottomY - textGap, 0);
        var text = new DBText
        {
            Position = textAnchor,
            HorizontalMode = TextHorizontalMode.TextCenter,
            VerticalMode = TextVerticalMode.TextTop,
            Height = textHeight,
            TextString = BoardRuntimeSettings.TagType.Replace("带文字", string.Empty),
            Layer = TagLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        text.AlignmentPoint = textAnchor;
        space.AppendEntity(text);
        transaction.AddNewlyCreatedDBObject(text, true);
        text.AdjustAlignment(database);
    }

    private static bool TryGetRectangle(Editor editor, string purpose, out Point3d min, out Point3d max)
    {
        min = default;
        max = default;
        var first = editor.GetPoint($"\n请选择{purpose}第一个角点: ");
        if (first.Status != PromptStatus.OK)
        {
            return false;
        }

        var second = editor.GetCorner(new PromptCornerOptions($"\n请选择{purpose}对角点: ", first.Value)
        {
            UseDashedLine = true
        });
        if (second.Status != PromptStatus.OK)
        {
            return false;
        }

        min = new Point3d(Math.Min(first.Value.X, second.Value.X), Math.Min(first.Value.Y, second.Value.Y), 0);
        max = new Point3d(Math.Max(first.Value.X, second.Value.X), Math.Max(first.Value.Y, second.Value.Y), 0);
        return max.X - min.X > Tolerance && max.Y - min.Y > Tolerance;
    }

    internal static void WritePlacement(
        Database database,
        BoardPlacementLayout layout,
        string propertyType,
        double entityThickness,
        int tagPolygonIndex)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(database, transaction, StructureLayerName, 7);
        for (var index = 0; index < layout.Polygons.Count; index++)
        {
            var polygon = layout.Polygons[index];
            var points = polygon.Points
                .Select(point => new Point3d(point.X, point.Y, 0))
                .ToArray();
            var polyline = AppendClosedPolyline(space, transaction, points, entityThickness);
            SetBoardProperty(database, transaction, polyline, propertyType);
            if (index == tagPolygonIndex)
            {
                AppendBoardTag(database, space, transaction, polyline.GeometricExtents);
            }
        }

        transaction.Commit();
    }

    private static Point3d SnapDirection(Point3d start, Point3d point)
    {
        var deltaX = point.X - start.X;
        var deltaY = point.Y - start.Y;
        return Math.Abs(deltaX) >= Math.Abs(deltaY)
            ? new Point3d(point.X, start.Y, start.Z)
            : new Point3d(start.X, point.Y, start.Z);
    }

    private static Point3d MidPoint(Point3d start, Point3d end) =>
        new((start.X + end.X) / 2, (start.Y + end.Y) / 2, (start.Z + end.Z) / 2);

    private static Vector3d PerpendicularUnit(Point3d start, Point3d end)
    {
        var direction = (end - start).GetNormal();
        return direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);
    }

    private static BoardPlacementPoint ToPlacementPoint(Point3d point) => new(point.X, point.Y);

    private sealed class ShoeShelfPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly ShoeShelfLayoutOptions options;
        private Point3d cursorPoint;
        private int countAdjustment;
        private int revision;
        private int sampledRevision = -1;

        public ShoeShelfPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            ShoeShelfLayoutOptions options)
        {
            this.bounds = bounds;
            this.options = options;
            cursorPoint = defaultCursor;
        }

        public string? LastErrorMessage { get; private set; }

        public bool AdjustLayerCount(int delta)
        {
            if (delta is not (-1 or 1) ||
                !TryGetLayout(out var layout) ||
                delta < 0 && layout.Layers.Count <= 1)
            {
                return false;
            }

            countAdjustment += delta;
            revision++;
            return true;
        }

        public bool TryGetLayout(out ShoeShelfLayout layout)
        {
            try
            {
                layout = ShoeShelfLayoutCalculator.Calculate(
                    bounds,
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y),
                    options with { CountAdjustment = countAdjustment });
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
            var count = TryGetLayout(out var layout) ? layout.Layers.Count : 0;
            var angle = TryGetLayout(out layout) ? $"，角度 {layout.AngleDegrees:0.##}°" : string.Empty;
            var center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
            var prompt = new JigPromptPointOptions(
                $"\n移动鼠标选择柜前方向，当前 {count} 层{angle}；W/+增加，S/-减少，单击确认: ")
            {
                BasePoint = center,
                UseBasePoint = true,
                Cursor = CursorType.RubberBand,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(prompt);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance && sampledRevision == revision)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            sampledRevision = revision;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            foreach (var layer in layout.Layers)
            {
                DrawPart(draw, layer.Shelf);
                if (layer.Stopper is not null)
                {
                    DrawPart(draw, layer.Stopper);
                }
            }

            return true;
        }

        private static void DrawPart(
            Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw,
            ShoeShelfPartLayout part)
        {
            using var polyline = new Polyline(part.Polygon.Points.Count)
            {
                Closed = true,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 3)
            };
            for (var index = 0; index < part.Polygon.Points.Count; index++)
            {
                var point = part.Polygon.Points[index];
                polyline.AddVertexAt(index, new Point2d(point.X, point.Y), 0, 0, 0);
            }

            draw.Geometry.Draw(polyline);
        }
    }

    private sealed class ShoeShelfHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyW = 0x57;
        private const int VirtualKeyS = 0x53;
        private const int VirtualKeyAdd = 0x6B;
        private const int VirtualKeySubtract = 0x6D;
        private const int VirtualKeyOemPlus = 0xBB;
        private const int VirtualKeyOemMinus = 0xBD;

        private readonly ShoeShelfPreviewJig jig;
        private bool disposed;

        public ShoeShelfHotkeyScope(ShoeShelfPreviewJig jig)
        {
            this.jig = jig;
            AcadApplication.PreTranslateMessage += OnPreTranslateMessage;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            AcadApplication.PreTranslateMessage -= OnPreTranslateMessage;
            disposed = true;
        }

        private void OnPreTranslateMessage(object? sender, PreTranslateMessageEventArgs eventArgs)
        {
            var message = eventArgs.Message;
            if (message.message is not WmKeyDown and not WmSystemKeyDown)
            {
                return;
            }

            var delta = message.wParam.ToInt32() switch
            {
                VirtualKeyW or VirtualKeyAdd or VirtualKeyOemPlus => 1,
                VirtualKeyS or VirtualKeySubtract or VirtualKeyOemMinus => -1,
                _ => 0
            };
            if (delta == 0)
            {
                return;
            }

            eventArgs.Handled = true;
            if (jig.AdjustLayerCount(delta))
            {
                WakeUpJig();
            }
        }

        private static void WakeUpJig()
        {
            if (!GetCursorPos(out var point))
            {
                return;
            }

            SetCursorPos(point.X + 1, point.Y);
            SetCursorPos(point.X, point.Y);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }

    private sealed class DoorPartitionPreviewJig : DrawJig
    {
        private readonly Point3d startPoint;
        private readonly double topExtension;
        private readonly double bottomExtension;
        private readonly bool smartMode;
        private readonly double maximumDoorWidth;
        private readonly int defaultDoorCount;
        private Point3d cursorPoint;
        private int countAdjustment;
        private int revision;
        private int sampledRevision = -1;

        public DoorPartitionPreviewJig(
            Point3d startPoint,
            Point3d defaultCursor,
            double topExtension,
            double bottomExtension,
            bool smartMode,
            double maximumDoorWidth,
            int defaultDoorCount)
        {
            this.startPoint = startPoint;
            this.topExtension = topExtension;
            this.bottomExtension = bottomExtension;
            this.smartMode = smartMode;
            this.maximumDoorWidth = maximumDoorWidth;
            this.defaultDoorCount = defaultDoorCount;
            cursorPoint = defaultCursor;
        }

        public string? LastErrorMessage { get; private set; }

        public bool AdjustDoorCount(int delta)
        {
            if (delta is not (-1 or 1) ||
                !TryGetLayout(out var layout, out _) ||
                delta < 0 && layout.DoorCount <= 1)
            {
                return false;
            }

            countAdjustment += delta;
            revision++;
            return true;
        }

        public bool TryGetLayout(out DoorPartitionGuideLayout layout, out double left)
        {
            left = Math.Min(startPoint.X, cursorPoint.X);
            try
            {
                layout = BoardDivisionCalculator.CalculateDoorGuideLayout(
                    Math.Abs(cursorPoint.X - startPoint.X),
                    smartMode,
                    maximumDoorWidth,
                    defaultDoorCount,
                    countAdjustment);
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
            var currentCount = TryGetLayout(out var layout, out _) ? layout.DoorCount : defaultDoorCount;
            var options = new JigPromptPointOptions(
                $"\n沿横向移动终点，当前 {currentCount} 扇；W/+增加，S/-减少，单击确认: ")
            {
                BasePoint = startPoint,
                UseBasePoint = true,
                Cursor = CursorType.RubberBand,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            var horizontalPoint = new Point3d(result.Value.X, startPoint.Y, startPoint.Z);
            if (horizontalPoint.DistanceTo(cursorPoint) <= Tolerance && sampledRevision == revision)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = horizontalPoint;
            sampledRevision = revision;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetLayout(out var layout, out var left))
            {
                return true;
            }

            foreach (var offset in layout.BoundaryOffsets)
            {
                var x = left + offset;
                using var line = new Line(
                    new Point3d(x, startPoint.Y - bottomExtension, startPoint.Z),
                    new Point3d(x, startPoint.Y + topExtension, startPoint.Z))
                {
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 3)
                };
                draw.Geometry.Draw(line);
            }

            return true;
        }
    }

    private sealed class DoorPartitionHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyW = 0x57;
        private const int VirtualKeyS = 0x53;
        private const int VirtualKeyAdd = 0x6B;
        private const int VirtualKeySubtract = 0x6D;
        private const int VirtualKeyOemPlus = 0xBB;
        private const int VirtualKeyOemMinus = 0xBD;

        private readonly DoorPartitionPreviewJig jig;
        private bool disposed;

        public DoorPartitionHotkeyScope(DoorPartitionPreviewJig jig)
        {
            this.jig = jig;
            AcadApplication.PreTranslateMessage += OnPreTranslateMessage;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            AcadApplication.PreTranslateMessage -= OnPreTranslateMessage;
            disposed = true;
        }

        private void OnPreTranslateMessage(object? sender, PreTranslateMessageEventArgs eventArgs)
        {
            var message = eventArgs.Message;
            if (message.message is not WmKeyDown and not WmSystemKeyDown)
            {
                return;
            }

            var delta = message.wParam.ToInt32() switch
            {
                VirtualKeyW or VirtualKeyAdd or VirtualKeyOemPlus => 1,
                VirtualKeyS or VirtualKeySubtract or VirtualKeyOemMinus => -1,
                _ => 0
            };
            if (delta == 0)
            {
                return;
            }

            eventArgs.Handled = true;
            if (jig.AdjustDoorCount(delta))
            {
                WakeUpJig();
            }
        }

        private static void WakeUpJig()
        {
            if (!GetCursorPos(out var point))
            {
                return;
            }

            SetCursorPos(point.X + 1, point.Y);
            SetCursorPos(point.X, point.Y);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }

    private sealed class LineOffsetPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly int lineCount;
        private readonly double distance;
        private readonly double thickness;
        private readonly bool asRectangle;
        private readonly Point3d center;
        private Point3d cursorPoint;

        public LineOffsetPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            int lineCount,
            double distance,
            double thickness,
            bool asRectangle)
        {
            this.bounds = bounds;
            this.lineCount = lineCount;
            this.distance = distance;
            this.thickness = thickness;
            this.asRectangle = asRectangle;
            center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
            cursorPoint = defaultCursor;
        }

        public bool TryGetPlacement(out BoardOffsetLinePlacement placement)
        {
            try
            {
                placement = BoardPlacementCalculator.CalculateOffsetLines(
                    bounds,
                    new BoardPlacementPoint(cursorPoint.X, cursorPoint.Y),
                    distance,
                    thickness,
                    lineCount);
                return true;
            }
            catch
            {
                placement = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions(
                $"\n鼠标向上/下生成横线，向左/右生成竖线，单击确认（净距 {distance:0.###} mm）: ")
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

            if (asRectangle && placement.Coordinates.Count > 1)
            {
                var first = GetOffsetLineEndpoints(bounds, placement.Orientation, placement.Coordinates[0]);
                var last = GetOffsetLineEndpoints(
                    bounds,
                    placement.Orientation,
                    placement.Coordinates[placement.Coordinates.Count - 1]);
                using var outline = new Polyline(4)
                {
                    Closed = true,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
                };
                outline.AddVertexAt(0, new Point2d(first.Start.X, first.Start.Y), 0, 0, 0);
                outline.AddVertexAt(1, new Point2d(first.End.X, first.End.Y), 0, 0, 0);
                outline.AddVertexAt(2, new Point2d(last.End.X, last.End.Y), 0, 0, 0);
                outline.AddVertexAt(3, new Point2d(last.Start.X, last.Start.Y), 0, 0, 0);
                draw.Geometry.Draw(outline);
                if (placement.Coordinates.Count == 3)
                {
                    var middle = GetOffsetLineEndpoints(bounds, placement.Orientation, placement.Coordinates[1]);
                    using var centerLine = new Line(middle.Start, middle.End)
                    {
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
                    };
                    draw.Geometry.Draw(centerLine);
                }

                return true;
            }

            foreach (var coordinate in placement.Coordinates)
            {
                var endpoints = GetOffsetLineEndpoints(bounds, placement.Orientation, coordinate);
                using var line = new Line(endpoints.Start, endpoints.End)
                {
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
                };
                draw.Geometry.Draw(line);
            }

            return true;
        }
    }

    private sealed class CenterBoardPreviewJig : DrawJig
    {
        private readonly BoardSpaceBounds bounds;
        private readonly double thickness;
        private readonly Point3d center;
        private Point3d cursorPoint;
        private BoardCenterOrientation? orientationOverride;
        private int revision;
        private int sampledRevision = -1;

        public CenterBoardPreviewJig(
            BoardSpaceBounds bounds,
            Point3d defaultCursor,
            double thickness)
        {
            this.bounds = bounds;
            this.thickness = thickness;
            cursorPoint = defaultCursor;
            center = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
        }

        public BoardCenterOrientation Orientation { get; private set; }

        public string? LastErrorMessage { get; private set; }

        public bool SetOrientation(BoardCenterOrientation orientation)
        {
            if (orientationOverride == orientation)
            {
                return false;
            }

            orientationOverride = orientation;
            revision++;
            return true;
        }

        public bool TryGetLayout(out BoardPlacementLayout layout)
        {
            try
            {
                Orientation = orientationOverride ?? ResolveOrientation(cursorPoint);
                layout = BoardPlacementCalculator.CalculateCenterBoard(
                    bounds,
                    ToPlacementPoint(cursorPoint),
                    thickness,
                    Orientation);
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
            var options = new JigPromptPointOptions(
                "\n移动鼠标选择横向或竖向，按 H/V 强制切换，单击确认: ")
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

            if (result.Value.DistanceTo(cursorPoint) <= Tolerance && sampledRevision == revision)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = result.Value;
            sampledRevision = revision;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            foreach (var polygon in layout.Polygons)
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

        private BoardCenterOrientation ResolveOrientation(Point3d cursor)
        {
            var deltaX = Math.Abs(cursor.X - center.X);
            var deltaY = Math.Abs(cursor.Y - center.Y);
            return deltaX >= deltaY
                ? BoardCenterOrientation.Horizontal
                : BoardCenterOrientation.Vertical;
        }
    }

    private sealed class CenterBoardHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyH = 0x48;
        private const int VirtualKeyV = 0x56;

        private readonly CenterBoardPreviewJig jig;
        private bool disposed;

        public CenterBoardHotkeyScope(CenterBoardPreviewJig jig)
        {
            this.jig = jig;
            AcadApplication.PreTranslateMessage += OnPreTranslateMessage;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            AcadApplication.PreTranslateMessage -= OnPreTranslateMessage;
            disposed = true;
        }

        private void OnPreTranslateMessage(object? sender, PreTranslateMessageEventArgs eventArgs)
        {
            var message = eventArgs.Message;
            if (message.message is not WmKeyDown and not WmSystemKeyDown)
            {
                return;
            }

            BoardCenterOrientation? orientation = message.wParam.ToInt32() switch
            {
                VirtualKeyH => BoardCenterOrientation.Horizontal,
                VirtualKeyV => BoardCenterOrientation.Vertical,
                _ => null
            };
            if (orientation is null)
            {
                return;
            }

            eventArgs.Handled = true;
            if (jig.SetOrientation(orientation.Value))
            {
                WakeUpJig();
            }
        }

        private static void WakeUpJig()
        {
            if (!GetCursorPos(out var point))
            {
                return;
            }

            SetCursorPos(point.X + 1, point.Y);
            SetCursorPos(point.X, point.Y);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }

    private sealed class BoardPlacementPreviewJig : DrawJig
    {
        private const double DirectionDeadZone = 1;

        private readonly string message;
        private readonly Func<Point3d, BoardPlacementLayout>? calculate;
        private readonly Func<Point3d, BoardTrimSectionType, BoardPlacementLayout>? calculateTrimSection;
        private readonly Point3d? trimAnchor;
        private Point3d cursorPoint;

        public BoardPlacementPreviewJig(
            Point3d defaultCursor,
            string message,
            Func<Point3d, BoardPlacementLayout> calculate)
        {
            cursorPoint = defaultCursor;
            this.message = message;
            this.calculate = calculate;
        }

        public BoardPlacementPreviewJig(
            Point3d defaultCursor,
            string message,
            BoardTrimSectionType sectionType,
            Point3d trimAnchor,
            Func<Point3d, BoardTrimSectionType, BoardPlacementLayout> calculateTrimSection)
        {
            cursorPoint = defaultCursor;
            this.message = message;
            SectionType = sectionType;
            this.trimAnchor = trimAnchor;
            this.calculateTrimSection = calculateTrimSection;
        }

        public BoardTrimSectionType SectionType { get; private set; } = BoardTrimSectionType.L;

        public string? LastErrorMessage { get; private set; }

        public bool SetSectionType(BoardTrimSectionType sectionType)
        {
            if (SectionType == sectionType)
            {
                return false;
            }

            SectionType = sectionType;
            return true;
        }

        public bool TryGetLayout(out BoardPlacementLayout layout)
        {
            try
            {
                layout = calculateTrimSection is null
                    ? calculate!(cursorPoint)
                    : calculateTrimSection(cursorPoint, SectionType);
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
            var controls = UserInputControls.Accept3dCoordinates |
                           UserInputControls.GovernedByUCSDetect;
            if (calculateTrimSection is null)
            {
                controls |= UserInputControls.GovernedByOrthoMode;
            }

            var options = new JigPromptPointOptions(message)
            {
                Cursor = CursorType.Crosshair,
                UserInputControls = controls
            };
            if (trimAnchor is Point3d anchor)
            {
                options.BasePoint = anchor;
                options.UseBasePoint = true;
            }

            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            var nextCursor = result.Value;
            if (trimAnchor is Point3d trimBasePoint)
            {
                nextCursor = new Point3d(
                    Math.Abs(nextCursor.X - trimBasePoint.X) <= DirectionDeadZone
                        ? cursorPoint.X
                        : nextCursor.X,
                    Math.Abs(nextCursor.Y - trimBasePoint.Y) <= DirectionDeadZone
                        ? cursorPoint.Y
                        : nextCursor.Y,
                    nextCursor.Z);
            }

            if (nextCursor.DistanceTo(cursorPoint) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            cursorPoint = nextCursor;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            foreach (var polygon in layout.Polygons)
            {
                using var outline = new Polyline(polygon.Points.Count)
                {
                    Closed = true,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
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

    private sealed class TrimSectionHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyL = 0x4C;
        private const int VirtualKeyT = 0x54;
        private const int VirtualKeyI = 0x49;

        private readonly BoardPlacementPreviewJig jig;
        private bool disposed;

        public TrimSectionHotkeyScope(BoardPlacementPreviewJig jig)
        {
            this.jig = jig;
            AcadApplication.PreTranslateMessage += OnPreTranslateMessage;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            AcadApplication.PreTranslateMessage -= OnPreTranslateMessage;
            disposed = true;
        }

        private void OnPreTranslateMessage(object? sender, PreTranslateMessageEventArgs eventArgs)
        {
            var message = eventArgs.Message;
            if (message.message is not WmKeyDown and not WmSystemKeyDown)
            {
                return;
            }

            BoardTrimSectionType? sectionType = message.wParam.ToInt32() switch
            {
                VirtualKeyL => BoardTrimSectionType.L,
                VirtualKeyT => BoardTrimSectionType.T,
                VirtualKeyI => BoardTrimSectionType.I,
                _ => null
            };
            if (sectionType is null)
            {
                return;
            }

            eventArgs.Handled = true;
            if (jig.SetSectionType(sectionType.Value))
            {
                WakeUpJig();
            }
        }

        private static void WakeUpJig()
        {
            if (!GetCursorPos(out var point))
            {
                return;
            }

            SetCursorPos(point.X + 1, point.Y);
            SetCursorPos(point.X, point.Y);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out NativePoint point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }

    private static bool TryClipDiagonal(
        double x,
        double y,
        double width,
        double height,
        double offset,
        bool rising,
        out Point3d start,
        out Point3d end)
    {
        var hits = new List<Point3d>();
        var slope = rising ? 1d : -1d;
        AddIfInside(x, y + offset, x, y, width, height, hits);
        AddIfInside(x + width, y + slope * width + offset, x, y, width, height, hits);
        AddIfInside(x + (y - offset - y) / slope, y, x, y, width, height, hits);
        AddIfInside(x + (y + height - offset - y) / slope, y + height, x, y, width, height, hits);
        hits = hits.GroupBy(point => (Math.Round(point.X, 6), Math.Round(point.Y, 6))).Select(group => group.First()).ToList();
        start = hits.Count > 0 ? hits[0] : default;
        end = hits.Count > 1 ? hits[1] : default;
        return hits.Count >= 2 && start.DistanceTo(end) > Tolerance;
    }

    private static void AddIfInside(
        double pointX,
        double pointY,
        double minX,
        double minY,
        double width,
        double height,
        ICollection<Point3d> hits)
    {
        if (pointX >= minX - Tolerance && pointX <= minX + width + Tolerance &&
            pointY >= minY - Tolerance && pointY <= minY + height + Tolerance)
        {
            hits.Add(new Point3d(pointX, pointY, 0));
        }
    }

    private static void EnsureLayer(
        Database database,
        Transaction transaction,
        string layerName,
        short colorIndex)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layers.Has(layerName))
        {
            return;
        }

        layers.UpgradeOpen();
        var layer = new LayerTableRecord
        {
            Name = layerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
        };
        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
    }
}

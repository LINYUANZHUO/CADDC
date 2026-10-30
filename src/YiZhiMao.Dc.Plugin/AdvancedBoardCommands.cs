using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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

    public static double AbaOuterThickness => Current.AbaOuterThickness;
    public static double AbaCoreThickness => Current.AbaCoreThickness;
    public static double AbaDepth => Current.AbaDepth;
    public static int DivideCount => Current.DivideCount;
    public static double DivideThickness => Current.DivideThickness;
    public static double BackboardThickness => Current.BackboardThickness;
    public static double BackboardInset => Current.BackboardInset;
    public static double BackboardLeftExtension => Current.BackboardLeftExtension;
    public static double BackboardRightExtension => Current.BackboardRightExtension;
    public static double BackboardTopExtension => Current.BackboardTopExtension;
    public static double BackboardBottomExtension => Current.BackboardBottomExtension;
}

public static class AdvancedBoardCommands
{
    private const string StructureLayerName = "0-柜体结构";
    private const string BackboardLayerName = "0-背板";
    private const string ProjectionLayerName = "0-层板投影";
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_BOARD_DISPLACE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "位移板件", "DCYC")]
    public static void DisplaceBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var selection = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择需要复制位移的板件: "
        });
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        var from = document.Editor.GetPoint("\n请指定基点: ");
        if (from.Status != PromptStatus.OK)
        {
            return;
        }

        var to = document.Editor.GetPoint(new PromptPointOptions("\n请指定目标点: ")
        {
            BasePoint = from.Value,
            UseBasePoint = true,
            UseDashedLine = true
        });
        if (to.Status != PromptStatus.OK)
        {
            return;
        }

        CloneSelection(document.Database, selection.Value, Matrix3d.Displacement(to.Value - from.Value), null);
        document.Editor.WriteMessage($"\n[一只猫dc] 已位移复制 {selection.Value.Count} 个对象。");
    }

    [CommandMethod("DC_CMD_BOARD_ABA", CommandFlags.Modal)]
    [ShortcutCommand("板件", "生成ABA夹层", "DCABA")]
    public static void DrawAbaBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TryGetTwoPoints(document.Editor, "ABA夹层", out var start, out var end))
        {
            return;
        }

        var values = new[]
        {
            AdvancedBoardRuntimeSettings.AbaOuterThickness,
            AdvancedBoardRuntimeSettings.AbaCoreThickness,
            AdvancedBoardRuntimeSettings.AbaOuterThickness
        };
        var total = values.Sum();
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);
        var offset = -total / 2;

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (var thickness in values)
        {
            var centerOffset = offset + thickness / 2;
            AppendPlate(space, transaction, start + normal * centerOffset, end + normal * centerOffset,
                thickness, AdvancedBoardRuntimeSettings.AbaDepth, StructureLayerName);
            offset += thickness;
        }

        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] ABA夹层已生成。");
    }

    [CommandMethod("DC_CMD_BOARD_DIVIDE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "均分板件", "DCFC")]
    public static void DivideBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TryGetRectangle(document.Editor, "均分范围", out var min, out var max))
        {
            return;
        }

        var options = new PromptKeywordOptions("\n请选择均分方向 [竖向(V)/横向(H)] <V>: ");
        options.Keywords.Add("V");
        options.Keywords.Add("H");
        options.AllowNone = true;
        var direction = document.Editor.GetKeywords(options);
        if (direction.Status == PromptStatus.Cancel)
        {
            return;
        }

        var vertical = direction.Status != PromptStatus.OK ||
                       string.Equals(direction.StringResult, "V", StringComparison.OrdinalIgnoreCase);
        var count = Math.Max(2, AdvancedBoardRuntimeSettings.DivideCount);
        IReadOnlyList<double> centers;
        try
        {
            centers = BoardGeometryCalculator.GetDividerCenters(
                vertical ? max.X - min.X : max.Y - min.Y,
                count,
                AdvancedBoardRuntimeSettings.DivideThickness);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 无法均分板件：{exception.Message}");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        foreach (var offset in centers)
        {
            var start = vertical
                ? new Point3d(min.X + offset, min.Y, 0)
                : new Point3d(min.X, min.Y + offset, 0);
            var end = vertical
                ? new Point3d(start.X, max.Y, 0)
                : new Point3d(max.X, start.Y, 0);
            AppendPlate(space, transaction, start, end, AdvancedBoardRuntimeSettings.DivideThickness,
                BoardRuntimeSettings.Depth, StructureLayerName);
        }

        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已按 {count} 格生成 {count - 1} 块均分板件。");
    }

    [CommandMethod("DC_CMD_BOARD_BACKBOARD", CommandFlags.Modal)]
    [ShortcutCommand("板件", "生成背板", "DCBB")]
    public static void DrawBackboard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TryGetRectangle(document.Editor, "背板内空", out var min, out var max))
        {
            return;
        }

        var inset = AdvancedBoardRuntimeSettings.BackboardInset;
        var left = min.X + inset - AdvancedBoardRuntimeSettings.BackboardLeftExtension;
        var right = max.X - inset + AdvancedBoardRuntimeSettings.BackboardRightExtension;
        var bottom = min.Y + inset - AdvancedBoardRuntimeSettings.BackboardBottomExtension;
        var top = max.Y - inset + AdvancedBoardRuntimeSettings.BackboardTopExtension;
        if (right - left <= Tolerance || top - bottom <= Tolerance)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 背板缩进与延伸参数超过内空尺寸。");
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, BackboardLayerName, 3);
        AppendRectangle(space, transaction, left, bottom, right, top,
            -Math.Abs(AdvancedBoardRuntimeSettings.BackboardThickness), BackboardLayerName);
        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] 背板已生成。");
    }

    [CommandMethod("DC_CMD_BOARD_SPLIT", CommandFlags.Modal)]
    [ShortcutCommand("板件", "拆分板件", "DCFG")]
    public static void SplitBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TrySelectRectangle(document.Editor, "请选择需要拆分的矩形板件", out var objectId))
        {
            return;
        }

        var point = document.Editor.GetPoint("\n请在拆分位置点取一点: ");
        if (point.Status != PromptStatus.OK)
        {
            return;
        }

        var directionOptions = new PromptKeywordOptions("\n请选择拆分方向 [竖向(V)/横向(H)] <自动>: ");
        directionOptions.Keywords.Add("V");
        directionOptions.Keywords.Add("H");
        directionOptions.AllowNone = true;
        var direction = document.Editor.GetKeywords(directionOptions);
        if (direction.Status == PromptStatus.Cancel)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var source = (Polyline)transaction.GetObject(objectId, OpenMode.ForWrite);
        if (!TryGetAxisAlignedBounds(source, out var min, out var max))
        {
            document.Editor.WriteMessage("\n[一只猫dc] 仅支持由四条水平/竖直边组成的闭合矩形。");
            return;
        }

        var vertical = direction.Status == PromptStatus.OK
            ? string.Equals(direction.StringResult, "V", StringComparison.OrdinalIgnoreCase)
            : (max.X - min.X) >= (max.Y - min.Y);
        var coordinate = vertical ? point.Value.X : point.Value.Y;
        if (!BoardGeometryCalculator.IsInteriorSplitCoordinate(
                vertical ? min.X : min.Y,
                vertical ? max.X : max.Y,
                coordinate))
        {
            document.Editor.WriteMessage("\n[一只猫dc] 拆分点必须位于板件内部，未修改原板件。");
            return;
        }

        var space = (BlockTableRecord)transaction.GetObject(source.OwnerId, OpenMode.ForWrite);
        Polyline firstPart;
        Polyline secondPart;
        if (vertical)
        {
            firstPart = AppendRectangle(space, transaction, min.X, min.Y, coordinate, max.Y, source.Thickness, source.Layer);
            secondPart = AppendRectangle(space, transaction, coordinate, min.Y, max.X, max.Y, source.Thickness, source.Layer);
        }
        else
        {
            firstPart = AppendRectangle(space, transaction, min.X, min.Y, max.X, coordinate, source.Thickness, source.Layer);
            secondPart = AppendRectangle(space, transaction, min.X, coordinate, max.X, max.Y, source.Thickness, source.Layer);
        }

        CopyEntityProperties(source, firstPart);
        CopyEntityProperties(source, secondPart);
        source.Erase();
        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] 板件已拆分为两块。");
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
            !CanMerge(firstMin, firstMax, secondMin, secondMax) ||
            first.OwnerId != second.OwnerId ||
            first.Normal.GetAngleTo(second.Normal) > Tolerance ||
            !BoardGeometryCalculator.HaveCompatibleMergeProperties(
                first.Layer,
                second.Layer,
                first.Thickness,
                second.Thickness,
                first.Elevation,
                second.Elevation))
        {
            document.Editor.WriteMessage("\n[一只猫dc] 只能合并边界对齐且图层、深度、高程一致的相邻矩形板件。");
            return;
        }

        var min = new Point3d(Math.Min(firstMin.X, secondMin.X), Math.Min(firstMin.Y, secondMin.Y), 0);
        var max = new Point3d(Math.Max(firstMax.X, secondMax.X), Math.Max(firstMax.Y, secondMax.Y), 0);
        var space = (BlockTableRecord)transaction.GetObject(first.OwnerId, OpenMode.ForWrite);
        var merged = AppendRectangle(space, transaction, min.X, min.Y, max.X, max.Y, first.Thickness, first.Layer);
        CopyEntityProperties(first, merged);
        first.Erase();
        second.Erase();
        transaction.Commit();
        document.Editor.WriteMessage("\n[一只猫dc] 两块板件已合并。");
    }

    [CommandMethod("DC_CMD_BOARD_EXTEND", CommandFlags.Modal)]
    [ShortcutCommand("板件", "延伸线或矩形", "DCEXX")]
    public static void ExtendBoard() => EditToBoundary(isExtend: true);

    [CommandMethod("DC_CMD_BOARD_TRIM", CommandFlags.Modal)]
    [ShortcutCommand("板件", "修剪线或矩形", "DCTRR")]
    public static void TrimBoard() => EditToBoundary(isExtend: false);

    [CommandMethod("DC_CMD_BOARD_SHELF_PROJECT", CommandFlags.Modal)]
    [ShortcutCommand("板件", "层板投影", "DCFS")]
    public static void ProjectShelf()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var selection = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择需要投影的层板对象: "
        });
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        var from = document.Editor.GetPoint("\n请指定投影基点: ");
        if (from.Status != PromptStatus.OK)
        {
            return;
        }

        var to = document.Editor.GetPoint(new PromptPointOptions("\n请指定投影位置: ")
        {
            BasePoint = from.Value,
            UseBasePoint = true,
            UseDashedLine = true
        });
        if (to.Status != PromptStatus.OK)
        {
            return;
        }

        CloneSelection(document.Database, selection.Value, Matrix3d.Displacement(to.Value - from.Value), ProjectionLayerName);
        document.Editor.WriteMessage($"\n[一只猫dc] 已生成 {selection.Value.Count} 个层板投影对象。");
    }

    private static void EditToBoundary(bool isExtend)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var sourceOptions = new PromptEntityOptions(isExtend
            ? "\n请选择需要延伸的直线或矩形边: "
            : "\n请选择需要修剪的直线或矩形边（点取保留侧）: ");
        sourceOptions.SetRejectMessage("\n请选择直线或闭合矩形多段线。");
        sourceOptions.AddAllowedClass(typeof(Line), true);
        sourceOptions.AddAllowedClass(typeof(Polyline), true);
        var sourceResult = document.Editor.GetEntity(sourceOptions);
        if (sourceResult.Status != PromptStatus.OK)
        {
            return;
        }

        var boundaryOptions = new PromptEntityOptions("\n请选择边界直线: ");
        boundaryOptions.SetRejectMessage("\n请选择直线。");
        boundaryOptions.AddAllowedClass(typeof(Line), true);
        var boundaryResult = document.Editor.GetEntity(boundaryOptions);
        if (boundaryResult.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var boundary = (Line)transaction.GetObject(boundaryResult.ObjectId, OpenMode.ForRead);
        var source = (Entity)transaction.GetObject(sourceResult.ObjectId, OpenMode.ForWrite);
        var changed = source switch
        {
            Line line => EditLineToBoundary(line, boundary, sourceResult.PickedPoint, isExtend),
            Polyline polyline => EditRectangleToBoundary(polyline, boundary, sourceResult.PickedPoint, isExtend),
            _ => false
        };
        if (!changed)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 边界方向或位置不符合本次延伸/修剪条件。");
            return;
        }

        transaction.Commit();
        document.Editor.WriteMessage(isExtend ? "\n[一只猫dc] 延伸完成。" : "\n[一只猫dc] 修剪完成。");
    }

    private static bool EditLineToBoundary(Line source, Line boundary, Point3d pickedPoint, bool isExtend)
    {
        var intersections = new Point3dCollection();
        source.IntersectWith(boundary, Intersect.ExtendBoth, intersections, IntPtr.Zero, IntPtr.Zero);
        if (intersections.Count == 0)
        {
            return false;
        }

        var hit = intersections[0];
        var pickedNearStart = pickedPoint.DistanceTo(source.StartPoint) <= pickedPoint.DistanceTo(source.EndPoint);
        var sourceVector = source.EndPoint - source.StartPoint;
        var lengthSquared = sourceVector.DotProduct(sourceVector);
        if (lengthSquared <= Tolerance)
        {
            return false;
        }

        var intersectionParameter = (hit - source.StartPoint).DotProduct(sourceVector) / lengthSquared;
        if (!BoardGeometryCalculator.CanEditLineToIntersection(intersectionParameter, pickedNearStart, isExtend))
        {
            return false;
        }

        if (isExtend)
        {
            if (pickedNearStart)
            {
                source.StartPoint = hit;
            }
            else
            {
                source.EndPoint = hit;
            }
        }
        else if (pickedNearStart)
        {
            source.EndPoint = hit;
        }
        else
        {
            source.StartPoint = hit;
        }

        return source.Length > Tolerance;
    }

    private static bool EditRectangleToBoundary(Polyline source, Line boundary, Point3d pickedPoint, bool isExtend)
    {
        if (!TryGetAxisAlignedBounds(source, out var min, out var max))
        {
            return false;
        }

        var boundaryVector = boundary.EndPoint - boundary.StartPoint;
        var verticalBoundary = Math.Abs(boundaryVector.Y) >= Math.Abs(boundaryVector.X);
        var distances = new[]
        {
            (Side: 0, Value: Math.Abs(pickedPoint.X - min.X)),
            (Side: 1, Value: Math.Abs(pickedPoint.X - max.X)),
            (Side: 2, Value: Math.Abs(pickedPoint.Y - min.Y)),
            (Side: 3, Value: Math.Abs(pickedPoint.Y - max.Y))
        };
        var side = distances.OrderBy(item => item.Value).First().Side;
        if ((side <= 1) != verticalBoundary)
        {
            return false;
        }

        var coordinate = verticalBoundary ? boundary.StartPoint.X : boundary.StartPoint.Y;
        var outside = side switch
        {
            0 => coordinate < min.X,
            1 => coordinate > max.X,
            2 => coordinate < min.Y,
            _ => coordinate > max.Y
        };
        if (isExtend != outside)
        {
            return false;
        }

        for (var index = 0; index < source.NumberOfVertices; index++)
        {
            var point = source.GetPoint2dAt(index);
            var onSide = side switch
            {
                0 => Math.Abs(point.X - min.X) <= Tolerance,
                1 => Math.Abs(point.X - max.X) <= Tolerance,
                2 => Math.Abs(point.Y - min.Y) <= Tolerance,
                _ => Math.Abs(point.Y - max.Y) <= Tolerance
            };
            if (onSide)
            {
                source.SetPointAt(index, verticalBoundary
                    ? new Point2d(coordinate, point.Y)
                    : new Point2d(point.X, coordinate));
            }
        }

        return true;
    }

    private static void CloneSelection(Database database, SelectionSet selection, Matrix3d transform, string? targetLayer)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        if (targetLayer is not null)
        {
            EnsureLayer(database, transaction, targetLayer, 8);
        }

        foreach (SelectedObject selected in selection)
        {
            if (transaction.GetObject(selected.ObjectId, OpenMode.ForRead) is not Entity source ||
                source.Clone() is not Entity clone)
            {
                continue;
            }

            clone.TransformBy(transform);
            if (targetLayer is not null)
            {
                clone.Layer = targetLayer;
            }

            space.AppendEntity(clone);
            transaction.AddNewlyCreatedDBObject(clone, true);
        }

        transaction.Commit();
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

    private static bool TryGetTwoPoints(Editor editor, string purpose, out Point3d start, out Point3d end)
    {
        start = default;
        end = default;
        var first = editor.GetPoint($"\n请选择{purpose}起点: ");
        if (first.Status != PromptStatus.OK)
        {
            return false;
        }

        var second = editor.GetPoint(new PromptPointOptions($"\n请选择{purpose}终点: ")
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

    private static bool CanMerge(Point3d firstMin, Point3d firstMax, Point3d secondMin, Point3d secondMax)
    {
        var sameVerticalSpan = Math.Abs(firstMin.Y - secondMin.Y) <= Tolerance &&
                               Math.Abs(firstMax.Y - secondMax.Y) <= Tolerance;
        var xTouches = firstMax.X >= secondMin.X - Tolerance && secondMax.X >= firstMin.X - Tolerance;
        var sameHorizontalSpan = Math.Abs(firstMin.X - secondMin.X) <= Tolerance &&
                                 Math.Abs(firstMax.X - secondMax.X) <= Tolerance;
        var yTouches = firstMax.Y >= secondMin.Y - Tolerance && secondMax.Y >= firstMin.Y - Tolerance;
        return (sameVerticalSpan && xTouches) || (sameHorizontalSpan && yTouches);
    }

    private static Polyline AppendPlate(
        BlockTableRecord space,
        Transaction transaction,
        Point3d start,
        Point3d end,
        double thickness,
        double depth,
        string layer)
    {
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis) * (thickness / 2);
        return AppendPolyline(space, transaction,
            [start + normal, end + normal, end - normal, start - normal], -Math.Abs(depth), layer);
    }

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
}

using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

internal static class BoardRuntimeSettings
{
    public static double Thickness { get; set; } = 18;
    public static double Depth { get; set; } = 500;
    public static double CenterDepth { get; set; } = 500;
    public static double TagScale { get; set; } = 5;
    public static string TagType { get; set; } = "活动层板带文字";
    public static double LineThickness { get; set; } = 18;
    public static bool DoubleAsRectangle { get; set; }
    public static bool TripleAsRectangle { get; set; }
    public static double ExtendStart { get; set; }
    public static double ExtendEnd { get; set; }
    public static double WineRackDepth { get; set; } = 500;
    public static double CrossThickness { get; set; } = 18;
    public static double DiagonalThickness { get; set; } = 12;
    public static double WineCellWidth { get; set; } = 100;
    public static int WineRows { get; set; } = 3;
    public static int WineColumns { get; set; } = 3;
}

public static class BoardCommands
{
    private const string StructureLayerName = "0-柜体结构";
    private const string TagLayerName = "0-板件标识图层";
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

    [CommandMethod("DC_CMD_BOARD_CENTER", CommandFlags.Modal)]
    [ShortcutCommand("板件", "中分板件", "DCZC")]
    public static void DrawCenterBoard()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TryGetRectangle(document.Editor, "柜体内空", out var min, out var max))
        {
            return;
        }

        var keyword = new PromptKeywordOptions("\n请选择中分方向 [横向(H)/竖向(V)] <自动>: ");
        keyword.Keywords.Add("H");
        keyword.Keywords.Add("V");
        keyword.AllowNone = true;
        var result = document.Editor.GetKeywords(keyword);
        if (result.Status == PromptStatus.Cancel)
        {
            return;
        }

        var horizontal = result.Status == PromptStatus.OK
            ? string.Equals(result.StringResult, "H", StringComparison.OrdinalIgnoreCase)
            : max.X - min.X >= max.Y - min.Y;
        var start = horizontal
            ? new Point3d(min.X, (min.Y + max.Y) / 2, 0)
            : new Point3d((min.X + max.X) / 2, min.Y, 0);
        var end = horizontal
            ? new Point3d(max.X, (min.Y + max.Y) / 2, 0)
            : new Point3d((min.X + max.X) / 2, max.Y, 0);
        WriteBoard(document.Database, start, end, BoardRuntimeSettings.Thickness, BoardRuntimeSettings.CenterDepth, true);
        document.Editor.WriteMessage("\n[一只猫dc] 中分板件已生成。" );
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
    [ShortcutCommand("板件", "单线变双", "DCZ2")]
    public static void ConvertLineToDouble() => ConvertSelectedLines(2, BoardRuntimeSettings.DoubleAsRectangle);

    [CommandMethod("DC_CMD_LINE_TO_TRIPLE", CommandFlags.Modal)]
    [ShortcutCommand("板件", "单线变三", "DCZ3")]
    public static void ConvertLineToTriple() => ConvertSelectedLines(3, BoardRuntimeSettings.TripleAsRectangle);

    [CommandMethod("DC_CMD_WINE_RACK_CROSS", CommandFlags.Modal)]
    [ShortcutCommand("板件", "生成十字酒架", "DCJJ")]
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
    [ShortcutCommand("板件", "生成斜酒架", "DCJX")]
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

    private static void OffsetSelectedLine(int lineCount, bool asRectangle)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !TrySelectLine(document.Editor, out var lineId))
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var source = (Line)transaction.GetObject(lineId, OpenMode.ForRead);
        var side = document.Editor.GetPoint("\n请在线段需要生成的一侧点击: ");
        if (side.Status != PromptStatus.OK)
        {
            return;
        }

        var sign = SideSign(source.StartPoint, source.EndPoint, side.Value);
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(document.Database, transaction, StructureLayerName, 7);
        AppendOffsetGeometry(space, transaction, source.StartPoint, source.EndPoint, lineCount, sign, asRectangle);
        transaction.Commit();
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
            if (transaction.GetObject(selected.ObjectId, OpenMode.ForRead) is not Line line)
            {
                continue;
            }

            var direction = (line.EndPoint - line.StartPoint).GetNormal();
            var start = line.StartPoint - direction * BoardRuntimeSettings.ExtendStart;
            var end = line.EndPoint + direction * BoardRuntimeSettings.ExtendEnd;
            AppendOffsetGeometry(space, transaction, start, end, lineCount, 1, asRectangle);
        }

        transaction.Commit();
    }

    private static void AppendOffsetGeometry(
        BlockTableRecord space,
        Transaction transaction,
        Point3d start,
        Point3d end,
        int lineCount,
        double sign,
        bool asRectangle)
    {
        var direction = (end - start).GetNormal();
        var normal = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis) * sign;
        var thickness = BoardRuntimeSettings.LineThickness;
        if (lineCount == 1)
        {
            AppendLine(space, transaction, start + normal * thickness, end + normal * thickness);
            return;
        }

        var first = start + normal * (thickness / 2);
        var second = start - normal * (thickness / 2);
        var firstEnd = end + normal * (thickness / 2);
        var secondEnd = end - normal * (thickness / 2);
        if (asRectangle)
        {
            AppendClosedPolyline(space, transaction, [first, firstEnd, secondEnd, second], 0);
        }
        else
        {
            AppendLine(space, transaction, first, firstEnd);
            AppendLine(space, transaction, second, secondEnd);
        }

        if (lineCount == 3)
        {
            AppendLine(space, transaction, start, end);
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
        var scale = Math.Clamp(BoardRuntimeSettings.TagScale, 0.1, 10);
        var circle = new Circle(center, Vector3d.ZAxis, 3.6 * scale)
        {
            Layer = TagLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        space.AppendEntity(circle);
        transaction.AddNewlyCreatedDBObject(circle, true);
        if (!BoardRuntimeSettings.TagType.Contains("文字", StringComparison.Ordinal))
        {
            return;
        }

        var text = new DBText
        {
            Position = center + new Vector3d(5 * scale, -2 * scale, 0),
            Height = 3.5 * scale,
            TextString = BoardRuntimeSettings.TagType.Replace("带文字", string.Empty, StringComparison.Ordinal),
            Layer = TagLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        space.AppendEntity(text);
        transaction.AddNewlyCreatedDBObject(text, true);
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

    private static bool TrySelectLine(Editor editor, out ObjectId objectId)
    {
        objectId = ObjectId.Null;
        var options = new PromptEntityOptions("\n请选择基准直线: ");
        options.SetRejectMessage("\n请选择直线对象。" );
        options.AddAllowedClass(typeof(Line), true);
        var result = editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        objectId = result.ObjectId;
        return true;
    }

    private static double SideSign(Point3d start, Point3d end, Point3d point)
    {
        var cross = (end.X - start.X) * (point.Y - start.Y) -
                    (end.Y - start.Y) * (point.X - start.X);
        return cross >= 0 ? 1 : -1;
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
        hits = hits.DistinctBy(point => (Math.Round(point.X, 6), Math.Round(point.Y, 6))).ToList();
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

using System.IO;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class FrameCommands
{
    private const string FrameLayerName = "0-图框";
    private const string DetailLayerName = "0-大样图";
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_FRAME_AUTO", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("一键图框", "一键加框", "DCTK")]
    public static void AddFrames()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var options = FrameToolRuntime.Options;
        if (!TryValidateOptions(editor, options) ||
            string.IsNullOrWhiteSpace(options.SelectedTemplatePath) ||
            !File.Exists(options.SelectedTemplatePath))
        {
            editor.WriteMessage("\n[一只猫dc] 请先在一键图框面板选择有效的 DWG 模板。");
            return;
        }

        var selection = editor.SelectImplied();
        if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
        {
            selection = editor.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择需要套图框的图形或块参照: "
            });
        }

        if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
        {
            return;
        }

        editor.SetImpliedSelection([]);
        try
        {
            var blockId = ImportTemplateAsBlock(document.Database, options.SelectedTemplatePath);
            using var transaction = document.Database.TransactionManager.StartTransaction();
            EnsureLayer(document.Database, transaction, FrameLayerName, 7);
            var templateFrame = FindTemplateFrame(blockId, transaction);
            var contentFrames = CollectContentFrames(selection.Value.GetObjectIds(), transaction);
            if (contentFrames.Count == 0)
            {
                editor.WriteMessage("\n[一只猫dc] 选中对象没有可用的几何范围。");
                return;
            }

            var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
            for (var index = 0; index < contentFrames.Count; index++)
            {
                var placement = FramePlacementCalculator.Calculate(templateFrame, contentFrames[index], options.FrameMargin);
                var placedBlockId = options.GeneratePageStatistics
                    ? CreatePageFrameBlock(
                        document.Database,
                        transaction,
                        blockId,
                        templateFrame,
                        index + 1,
                        contentFrames.Count)
                    : blockId;
                var reference = new BlockReference(
                    new Point3d(placement.InsertionX, placement.InsertionY, 0),
                    placedBlockId)
                {
                    ScaleFactors = new Scale3d(placement.Scale),
                    Layer = FrameLayerName
                };
                space.AppendEntity(reference);
                transaction.AddNewlyCreatedDBObject(reference, true);
            }

            transaction.Commit();
            editor.WriteMessage($"\n[一只猫dc] 已生成 {contentFrames.Count} 个图框。");
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\n[一只猫dc] 一键加框失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_FRAME_ALIGN", CommandFlags.Modal)]
    [ShortcutCommand("一键图框", "图框对齐", "DCDQ")]
    public static void AlignFrames()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var selection = editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请选择由一键加框生成的图框块: "
        });
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        var baselineOptions = new PromptEntityOptions(
            FrameToolRuntime.Options.AlignmentMode == FrameAlignmentMode.Left
                ? "\n请选择作为起点的竖线: "
                : "\n请选择作为起点的横线: ");
        baselineOptions.SetRejectMessage("\n请选择直线。");
        baselineOptions.AddAllowedClass(typeof(Line), true);
        var baselineResult = editor.GetEntity(baselineOptions);
        if (baselineResult.Status != PromptStatus.OK)
        {
            return;
        }

        try
        {
            using var transaction = document.Database.TransactionManager.StartTransaction();
            var baseline = (Line)transaction.GetObject(baselineResult.ObjectId, OpenMode.ForRead);
            var references = selection.Value.GetObjectIds()
                .Select(id => transaction.GetObject(id, OpenMode.ForRead, false) as BlockReference)
                .Where(reference => reference is not null && IsFrameBlock(reference, transaction))
                .Cast<BlockReference>()
                .Select(reference => new FrameReference(reference.ObjectId, ToFrame(reference.GeometricExtents)))
                .ToList();
            if (references.Count == 0)
            {
                editor.WriteMessage("\n[一只猫dc] 选区中没有由本插件生成的图框块。");
                return;
            }

            if (FrameToolRuntime.Options.AlignmentMode == FrameAlignmentMode.Left)
            {
                ArrangeFromLeft(references, baseline.StartPoint.X, FrameToolRuntime.Options.HorizontalGap, transaction);
            }
            else
            {
                ArrangeFromBottom(references, baseline.StartPoint.Y, FrameToolRuntime.Options.VerticalGap, transaction);
            }

            transaction.Commit();
            editor.WriteMessage($"\n[一只猫dc] 已重新排列 {references.Count} 个图框。");
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\n[一只猫dc] 图框对齐失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_DETAIL_DRAW", CommandFlags.Modal)]
    [ShortcutCommand("一键图框", "局部放大", "DCFD")]
    public static void DrawDetail() => CreateDetail(isClip: false);

    [CommandMethod("DC_CMD_DETAIL_CLIP", CommandFlags.Modal)]
    [ShortcutCommand("一键图框", "局部截图", "DCJT")]
    public static void ClipDetail() => CreateDetail(isClip: true);

    private static void CreateDetail(bool isClip)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var first = editor.GetPoint(isClip ? "\n请指定截图区域第一角点: " : "\n请指定大样区域第一角点: ");
        if (first.Status != PromptStatus.OK)
        {
            return;
        }

        var secondOptions = new PromptCornerOptions("\n请指定区域对角点: ", first.Value);
        var second = editor.GetCorner(secondOptions);
        if (second.Status != PromptStatus.OK)
        {
            return;
        }

        var region = Normalize(first.Value, second.Value);
        if (region.Width <= Tolerance || region.Height <= Tolerance)
        {
            editor.WriteMessage("\n[一只猫dc] 选择区域必须具有有效宽高。");
            return;
        }

        var selection = editor.SelectCrossingWindow(
            new Point3d(region.MinX, region.MinY, 0),
            new Point3d(region.MaxX, region.MaxY, 0));
        if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
        {
            editor.WriteMessage("\n[一只猫dc] 选择区域内没有可复制的图形。");
            return;
        }

        var target = editor.GetPoint(isClip ? "\n请指定局部截图插入点: " : "\n请指定局部大样插入点: ");
        if (target.Status != PromptStatus.OK)
        {
            return;
        }

        var options = FrameToolRuntime.Options;
        if (!TryValidateOptions(editor, options))
        {
            return;
        }

        try
        {
            using var transaction = document.Database.TransactionManager.StartTransaction();
            EnsureLayer(document.Database, transaction, DetailLayerName, 4);
            var center = new Point3d((region.MinX + region.MaxX) / 2, (region.MinY + region.MaxY) / 2, 0);
            var blockId = CreateDetailBlock(
                document.Database,
                transaction,
                selection.Value.GetObjectIds(),
                region,
                center,
                isClip,
                options);
            var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
            var scale = isClip ? options.ClipScale : options.DetailScale;
            var reference = new BlockReference(target.Value, blockId)
            {
                Layer = DetailLayerName,
                ScaleFactors = new Scale3d(scale)
            };
            space.AppendEntity(reference);
            transaction.AddNewlyCreatedDBObject(reference, true);
            ApplySpatialFilter(
                reference,
                region,
                center,
                isClip ? DetailBoundaryShape.RoundedRectangle : options.DetailShape,
                isClip ? options.ClipCornerRadius : options.DetailCornerRadius,
                transaction);

            if (!isClip)
            {
                var sourceBoundary = CreateBoundary(region, Point3d.Origin, options.DetailShape, options.DetailCornerRadius);
                sourceBoundary.Layer = DetailLayerName;
                space.AppendEntity(sourceBoundary);
                transaction.AddNewlyCreatedDBObject(sourceBoundary, true);
            }

            transaction.Commit();
            editor.WriteMessage(isClip
                ? "\n[一只猫dc] 已生成局部截图块。"
                : "\n[一只猫dc] 已生成局部大样块。");
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\n[一只猫dc] {(isClip ? "局部截图" : "局部放大")}失败：{exception.Message}");
        }
    }

    private static bool TryValidateOptions(Editor editor, FrameToolOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch (InvalidOperationException exception)
        {
            editor.WriteMessage($"\n[一只猫dc] {exception.Message}");
            return false;
        }
    }

    private static ObjectId ImportTemplateAsBlock(Database targetDatabase, string filePath)
    {
        var blockName = CreateTemplateBlockName(filePath);
        using (var transaction = targetDatabase.TransactionManager.StartOpenCloseTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(targetDatabase.BlockTableId, OpenMode.ForRead);
            if (blockTable.Has(blockName))
            {
                return blockTable[blockName];
            }
        }

        using var sourceDatabase = new Database(false, true);
        sourceDatabase.ReadDwgFile(filePath, FileOpenMode.OpenForReadAndAllShare, true, null);
        sourceDatabase.CloseInput(true);
        return targetDatabase.Insert(blockName, sourceDatabase, true);
    }

    private static string CreateTemplateBlockName(string filePath)
    {
        var sourceName = Path.GetFileNameWithoutExtension(filePath);
        var safeName = new string(sourceName
            .Select(character => char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_')
            .Take(48)
            .ToArray());
        return $"DC_FRAME_{(string.IsNullOrEmpty(safeName) ? "TEMPLATE" : safeName)}_{File.GetLastWriteTimeUtc(filePath).Ticks:X}";
    }

    private static PrintFrame FindTemplateFrame(ObjectId blockId, Transaction transaction)
    {
        var definition = (BlockTableRecord)transaction.GetObject(blockId, OpenMode.ForRead);
        var rectangles = new List<PrintFrame>();
        PrintFrame? total = null;
        foreach (ObjectId objectId in definition)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity ||
                !TryGetFrame(entity, out var frame))
            {
                continue;
            }

            total = total is null ? frame : Union(total.Value, frame);
            if (entity is Polyline polyline && IsRectangle(polyline))
            {
                rectangles.Add(frame);
            }
        }

        if (rectangles.Count > 0)
        {
            return rectangles.MaxBy(frame => frame.Width * frame.Height);
        }

        return total ?? throw new InvalidOperationException("DWG 模板中没有可用的矩形或几何范围。");
    }

    private static IReadOnlyList<PrintFrame> CollectContentFrames(IEnumerable<ObjectId> objectIds, Transaction transaction)
    {
        var blockFrames = new List<PrintFrame>();
        PrintFrame? otherFrame = null;
        foreach (var objectId in objectIds)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity ||
                !TryGetFrame(entity, out var frame))
            {
                continue;
            }

            if (entity is BlockReference)
            {
                blockFrames.Add(frame);
            }
            else
            {
                otherFrame = otherFrame is null ? frame : Union(otherFrame.Value, frame);
            }
        }

        if (otherFrame is not null)
        {
            blockFrames.Add(otherFrame.Value);
        }

        return PrintFrameProcessor.Prepare(blockFrames);
    }

    private static ObjectId CreatePageFrameBlock(
        Database database,
        Transaction transaction,
        ObjectId templateBlockId,
        PrintFrame templateFrame,
        int page,
        int total)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForWrite);
        var definition = new BlockTableRecord
        {
            Name = $"DC_FRAME_PAGE_{DateTime.UtcNow.Ticks:X}_{page}",
            Origin = Point3d.Origin
        };
        var blockId = blockTable.Add(definition);
        transaction.AddNewlyCreatedDBObject(definition, true);

        var nestedFrame = new BlockReference(Point3d.Origin, templateBlockId);
        definition.AppendEntity(nestedFrame);
        transaction.AddNewlyCreatedDBObject(nestedFrame, true);

        var text = new MText
        {
            Contents = $"第 {page} 页 / 共 {total} 页",
            TextHeight = 25,
            Attachment = AttachmentPoint.BottomRight,
            Location = new Point3d(
                templateFrame.MaxX - 20,
                templateFrame.MinY + 20,
                0)
        };
        definition.AppendEntity(text);
        transaction.AddNewlyCreatedDBObject(text, true);
        return blockId;
    }

    private static void ArrangeFromLeft(
        IReadOnlyList<FrameReference> references,
        double baselineX,
        double gap,
        Transaction transaction)
    {
        var ordered = references.OrderBy(item => item.Bounds.MinX).ToList();
        var bottom = ordered.Min(item => item.Bounds.MinY);
        var currentX = baselineX;
        foreach (var item in ordered)
        {
            var reference = (BlockReference)transaction.GetObject(item.Id, OpenMode.ForWrite);
            reference.TransformBy(Matrix3d.Displacement(new Vector3d(
                currentX - item.Bounds.MinX,
                bottom - item.Bounds.MinY,
                0)));
            currentX += item.Bounds.Width + gap;
        }
    }

    private static void ArrangeFromBottom(
        IReadOnlyList<FrameReference> references,
        double baselineY,
        double gap,
        Transaction transaction)
    {
        var ordered = references.OrderBy(item => item.Bounds.MinY).ToList();
        var left = ordered.Min(item => item.Bounds.MinX);
        var currentY = baselineY;
        foreach (var item in ordered)
        {
            var reference = (BlockReference)transaction.GetObject(item.Id, OpenMode.ForWrite);
            reference.TransformBy(Matrix3d.Displacement(new Vector3d(
                left - item.Bounds.MinX,
                currentY - item.Bounds.MinY,
                0)));
            currentY += item.Bounds.Height + gap;
        }
    }

    private static bool IsFrameBlock(BlockReference reference, Transaction transaction)
    {
        return transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead) is BlockTableRecord definition &&
               definition.Name.StartsWith("DC_FRAME_", StringComparison.OrdinalIgnoreCase);
    }

    private static ObjectId CreateDetailBlock(
        Database database,
        Transaction transaction,
        IReadOnlyCollection<ObjectId> sourceIds,
        PrintFrame region,
        Point3d center,
        bool isClip,
        FrameToolOptions options)
    {
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForWrite);
        var definition = new BlockTableRecord
        {
            Name = $"DC_{(isClip ? "CLIP" : "DETAIL")}_{DateTime.UtcNow.Ticks:X}",
            Origin = Point3d.Origin
        };
        var blockId = blockTable.Add(definition);
        transaction.AddNewlyCreatedDBObject(definition, true);
        var displacement = Matrix3d.Displacement(center.GetVectorTo(Point3d.Origin));
        foreach (var objectId in sourceIds)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity ||
                entity is Viewport)
            {
                continue;
            }

            if (entity.Clone() is not Entity clone)
            {
                continue;
            }

            clone.TransformBy(displacement);
            definition.AppendEntity(clone);
            transaction.AddNewlyCreatedDBObject(clone, true);
        }

        var localRegion = new PrintFrame(
            region.MinX - center.X,
            region.MinY - center.Y,
            region.MaxX - center.X,
            region.MaxY - center.Y);
        var shape = isClip ? DetailBoundaryShape.RoundedRectangle : options.DetailShape;
        var radius = isClip ? options.ClipCornerRadius : options.DetailCornerRadius;
        var boundary = CreateBoundary(localRegion, Point3d.Origin, shape, radius);
        boundary.Layer = DetailLayerName;
        definition.AppendEntity(boundary);
        transaction.AddNewlyCreatedDBObject(boundary, true);
        return blockId;
    }

    private static void ApplySpatialFilter(
        BlockReference reference,
        PrintFrame region,
        Point3d center,
        DetailBoundaryShape shape,
        double cornerRadius,
        Transaction transaction)
    {
        var points = CreateFilterPoints(region, center, shape, cornerRadius);
        var definition = new SpatialFilterDefinition(points, Vector3d.ZAxis, 0, 0, double.PositiveInfinity, true);
        var filter = new SpatialFilter { Definition = definition };
        reference.CreateExtensionDictionary();
        var extension = (DBDictionary)transaction.GetObject(reference.ExtensionDictionary, OpenMode.ForWrite);
        var filterDictionary = new DBDictionary();
        extension.SetAt("ACAD_FILTER", filterDictionary);
        transaction.AddNewlyCreatedDBObject(filterDictionary, true);
        filterDictionary.SetAt("SPATIAL", filter);
        transaction.AddNewlyCreatedDBObject(filter, true);
    }

    private static Point2dCollection CreateFilterPoints(
        PrintFrame region,
        Point3d center,
        DetailBoundaryShape shape,
        double cornerRadius)
    {
        if (shape == DetailBoundaryShape.RoundedRectangle)
        {
            var left = region.MinX - center.X;
            var right = region.MaxX - center.X;
            var bottom = region.MinY - center.Y;
            var top = region.MaxY - center.Y;
            var roundedRadius = Math.Min(cornerRadius, Math.Min(region.Width, region.Height) / 2);
            if (roundedRadius <= Tolerance)
            {
                return new Point2dCollection
                {
                    new(left, bottom),
                    new(right, bottom),
                    new(right, top),
                    new(left, top)
                };
            }

            var roundedPoints = new Point2dCollection();
            AddArcPoints(roundedPoints, right - roundedRadius, bottom + roundedRadius, roundedRadius, -Math.PI / 2, 0);
            AddArcPoints(roundedPoints, right - roundedRadius, top - roundedRadius, roundedRadius, 0, Math.PI / 2);
            AddArcPoints(roundedPoints, left + roundedRadius, top - roundedRadius, roundedRadius, Math.PI / 2, Math.PI);
            AddArcPoints(roundedPoints, left + roundedRadius, bottom + roundedRadius, roundedRadius, Math.PI, Math.PI * 1.5);
            return roundedPoints;
        }

        var radius = Math.Min(region.Width, region.Height) / 2;
        var points = new Point2dCollection();
        for (var index = 0; index < 32; index++)
        {
            var angle = Math.PI * 2 * index / 32;
            points.Add(new Point2d(Math.Cos(angle) * radius, Math.Sin(angle) * radius));
        }

        return points;
    }

    private static void AddArcPoints(
        Point2dCollection points,
        double centerX,
        double centerY,
        double radius,
        double startAngle,
        double endAngle)
    {
        const int segmentCount = 4;
        for (var index = 0; index <= segmentCount; index++)
        {
            var angle = startAngle + (endAngle - startAngle) * index / segmentCount;
            points.Add(new Point2d(
                centerX + Math.Cos(angle) * radius,
                centerY + Math.Sin(angle) * radius));
        }
    }

    private static Entity CreateBoundary(
        PrintFrame region,
        Point3d offset,
        DetailBoundaryShape shape,
        double cornerRadius)
    {
        var centerX = (region.MinX + region.MaxX) / 2 + offset.X;
        var centerY = (region.MinY + region.MaxY) / 2 + offset.Y;
        if (shape == DetailBoundaryShape.Circle)
        {
            return new Circle(
                new Point3d(centerX, centerY, 0),
                Vector3d.ZAxis,
                Math.Min(region.Width, region.Height) / 2);
        }

        var left = region.MinX + offset.X;
        var right = region.MaxX + offset.X;
        var bottom = region.MinY + offset.Y;
        var top = region.MaxY + offset.Y;
        var radius = Math.Min(cornerRadius, Math.Min(region.Width, region.Height) / 2);
        if (radius <= Tolerance)
        {
            var rectangle = new Polyline(4) { Closed = true };
            rectangle.AddVertexAt(0, new Point2d(left, bottom), 0, 0, 0);
            rectangle.AddVertexAt(1, new Point2d(right, bottom), 0, 0, 0);
            rectangle.AddVertexAt(2, new Point2d(right, top), 0, 0, 0);
            rectangle.AddVertexAt(3, new Point2d(left, top), 0, 0, 0);
            return rectangle;
        }

        const double bulge = 0.41421356237309503;
        var rounded = new Polyline(8) { Closed = true };
        rounded.AddVertexAt(0, new Point2d(left + radius, bottom), 0, 0, 0);
        rounded.AddVertexAt(1, new Point2d(right - radius, bottom), bulge, 0, 0);
        rounded.AddVertexAt(2, new Point2d(right, bottom + radius), 0, 0, 0);
        rounded.AddVertexAt(3, new Point2d(right, top - radius), bulge, 0, 0);
        rounded.AddVertexAt(4, new Point2d(right - radius, top), 0, 0, 0);
        rounded.AddVertexAt(5, new Point2d(left + radius, top), bulge, 0, 0);
        rounded.AddVertexAt(6, new Point2d(left, top - radius), 0, 0, 0);
        rounded.AddVertexAt(7, new Point2d(left, bottom + radius), bulge, 0, 0);
        return rounded;
    }

    private static void EnsureLayer(Database database, Transaction transaction, string layerName, short colorIndex)
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

    private static bool IsRectangle(Polyline polyline)
    {
        if (!polyline.Closed || polyline.NumberOfVertices != 4)
        {
            return false;
        }

        for (var index = 0; index < 4; index++)
        {
            if (polyline.GetSegmentType(index) != SegmentType.Line)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetFrame(Entity entity, out PrintFrame frame)
    {
        try
        {
            frame = ToFrame(entity.GeometricExtents);
            return frame.IsFinite && frame.Width > Tolerance && frame.Height > Tolerance;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            frame = default;
            return false;
        }
    }

    private static PrintFrame ToFrame(Extents3d extents) =>
        new(extents.MinPoint.X, extents.MinPoint.Y, extents.MaxPoint.X, extents.MaxPoint.Y);

    private static PrintFrame Union(PrintFrame left, PrintFrame right) =>
        new(
            Math.Min(left.MinX, right.MinX),
            Math.Min(left.MinY, right.MinY),
            Math.Max(left.MaxX, right.MaxX),
            Math.Max(left.MaxY, right.MaxY));

    private static PrintFrame Normalize(Point3d first, Point3d second) =>
        new(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Max(first.X, second.X),
            Math.Max(first.Y, second.Y));

    private readonly record struct FrameReference(ObjectId Id, PrintFrame Bounds);
}

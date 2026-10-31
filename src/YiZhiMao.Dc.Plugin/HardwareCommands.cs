using System.IO;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

internal static class HardwareRuntimeSettings
{
    public static HardwareLibraryItem? SelectedHangingRod { get; set; }

    public static HardwareLibraryItem? SelectedLightStrip { get; set; }

    public static HardwareOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.Hardware;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.Hardware = value);
    }

    public static void Save(HardwareOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.Hardware = value);
}

public static class HardwareCommands
{
    private const double HangingRodSideClearance = 4;
    private const double HangingRodFitTolerance = 0.01;
    private const int HangingRodFitPasses = 3;
    private const double HingeCupDiameter = 35;
    private const double HingeCupOffset = 23;
    private const string HingeLayerName = "DC_铰链";
    private const string DoorLayerName = "DC_门板";
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_HANGING_ROD", CommandFlags.Modal)]
    [ShortcutCommand("五金件", "插入挂衣杆", "DCYG")]
    public static void InsertHangingRod()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var item = HardwareRuntimeSettings.SelectedHangingRod;
        if (!TryGetLibraryItem(document.Editor, item, "挂衣杆", out var selectedItem))
        {
            return;
        }

        if (!TrySelectHangingRodLines(document.Database, document.Editor, out var selectedLine, out var topLine))
        {
            return;
        }

        try
        {
            var placement = HardwareLayoutCalculator.CalculateHangingRod(
                selectedLine.Left,
                selectedLine.Right,
                topLine.Y,
                HardwareRuntimeSettings.Current.HangingRodTopOffset,
                HangingRodSideClearance);
            var blockId = ImportDrawingAsBlock(document.Database, selectedItem.DwgPath);
            InsertStretchedHangingRod(document.Database, blockId, placement);
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 已插入挂衣杆：{selectedItem.Name}，成品长度 {placement.Width:0.###} mm，离顶 {HardwareRuntimeSettings.Current.HangingRodTopOffset:0.###} mm。");
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[错误] 插入挂衣杆失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_LIGHT_STRIP", CommandFlags.Modal)]
    [ShortcutCommand("五金件", "插入灯带", "DCDD")]
    public static void InsertLightStrip()
    {
        InsertSelected(HardwareRuntimeSettings.SelectedLightStrip, "灯带");
    }

    [CommandMethod("DC_CMD_HINGE_INSERT", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "插入铰链", "DCJL")]
    public static void InsertHinges()
    {
        RunHingeCommand();
    }

    [CommandMethod("DC_CMD_HINGE_LABEL", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "标记铰链种类", "DCHJL")]
    public static void LabelHinges()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        var selectionOptions = new PromptSelectionOptions
        {
            MessageForAdding = "\n左键框选或点选铰链圆，右键或空格结束选择: "
        };
        var selectionFilter = new SelectionFilter(
        [
            new TypedValue((int)DxfCode.Start, "CIRCLE"),
            new TypedValue((int)DxfCode.LayerName, HingeLayerName)
        ]);
        var selection = editor.GetSelection(selectionOptions, selectionFilter);
        if (selection.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 未选择铰链圆，操作已取消。");
            return;
        }

        if (!TryShowHingeTypeMenu(out var hingeType))
        {
            editor.WriteMessage("\n[提示] 铰链类型标记已取消。");
            return;
        }

        try
        {
            var count = WriteHingeTypeLabels(
                document.Database,
                selection.Value.GetObjectIds(),
                hingeType,
                HardwareRuntimeSettings.Current);
            editor.WriteMessage($"\n[一只猫dc] 已将 {count} 个铰链标记为“{hingeType}”。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 标记铰链类型失败：{exception.Message}");
        }
    }

    [CommandMethod("DCHJL", CommandFlags.Modal)]
    public static void LabelHingesShortcut()
    {
        LabelHinges();
    }

    private static void RunHingeCommand()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        if (!TrySelectHingeDoorEdges(document.Database, editor, out var door))
        {
            return;
        }

        var sideResult = editor.GetPoint("\n请在门板左侧或右侧点击，确定铰链侧: ");
        if (sideResult.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        var isLeft = Math.Abs(sideResult.Value.X - door.Left) <= Math.Abs(sideResult.Value.X - door.Right);
        var options = HardwareRuntimeSettings.Current;

        try
        {
            var hingeCount = options.GetHingeCount(door.Height);
            var offsets = HardwareLayoutCalculator.CalculateHingeOffsets(
                door.Height,
                hingeCount,
                options.HingeTopOffset,
                options.HingeBottomOffset,
                options.UseFixedHingeSpacing,
                options.FixedHingeSpacing);
            WriteHinges(
                document.Database,
                door,
                isLeft,
                offsets,
                options.GenerateHingeDimensions,
                options);

            editor.WriteMessage($"\n[一只猫dc] 已在{(isLeft ? "左" : "右")}侧生成 {hingeCount} 个铰链。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 铰链操作失败：{exception.Message}");
        }
    }

    private static void WriteHinges(
        Database database,
        AxisAlignedRectangle door,
        bool isLeft,
        IReadOnlyList<double> offsets,
        bool drawDimensions,
        HardwareOptions options)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(database, transaction, HingeLayerName);

        var sideX = isLeft ? door.Left : door.Right;
        var radius = HingeCupDiameter / 2;
        var symbolHalfWidth = radius + 2;
        var centerX = HardwareLayoutCalculator.CalculateHingeCenterX(
            door.Left,
            door.Right,
            isLeft,
            HingeCupOffset,
            symbolHalfWidth);
        foreach (var offset in offsets)
        {
            var center = new Point3d(centerX, door.Top - offset, 0);
            AppendEntity(space, transaction, new Circle(center, Vector3d.ZAxis, radius) { Layer = HingeLayerName });
            AppendEntity(
                space,
                transaction,
                new Line(
                    new Point3d(center.X - radius - 2, center.Y, 0),
                    new Point3d(center.X + radius + 2, center.Y, 0)) { Layer = HingeLayerName });
            AppendEntity(
                space,
                transaction,
                new Line(
                    new Point3d(center.X, center.Y - radius - 2, 0),
                    new Point3d(center.X, center.Y + radius + 2, 0)) { Layer = HingeLayerName });
        }

        var inside = isLeft ? 1d : -1d;
        if (drawDimensions)
        {
            var dimensions = HardwareLayoutCalculator.CalculateHingeDimensions(
                door.Height,
                offsets,
                options.HingeDimensionMode);
            foreach (var dimension in dimensions)
            {
                var firstPoint = new Point3d(sideX, door.Top - dimension.StartOffset, 0);
                var secondPoint = new Point3d(sideX, door.Top - dimension.EndOffset, 0);
                var lineOffset = options.HingeDimensionOffset +
                                 dimension.Level * options.HingeDimensionLineSpacing;
                var dimensionLinePoint = new Point3d(
                    sideX + inside * lineOffset,
                    (firstPoint.Y + secondPoint.Y) / 2,
                    0);
                var alignedDimension = new AlignedDimension(
                    firstPoint,
                    secondPoint,
                    dimensionLinePoint,
                    string.Empty,
                    database.Dimstyle)
                {
                    Layer = HingeLayerName,
                    Dimscale = 1,
                    Dimtxt = options.HingeDimensionTextHeight,
                    Dimasz = options.HingeDimensionTextHeight * 0.6,
                    Dimgap = options.HingeDimensionTextHeight * 0.25
                };
                AppendEntity(space, transaction, alignedDimension);
            }
        }

        transaction.Commit();
    }

    private static bool TryShowHingeTypeMenu(out string hingeType)
    {
        var menu = new HingeTypeMenuWindow();
        AcadApplication.ShowModalWindow(menu);
        hingeType = menu.SelectedHingeType ?? string.Empty;
        return hingeType.Length > 0;
    }

    private static int WriteHingeTypeLabels(
        Database database,
        IEnumerable<ObjectId> hingeIds,
        string hingeType,
        HardwareOptions options)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        EnsureLayer(database, transaction, HingeLayerName);
        var doorBounds = CollectHingeDoorBounds(space, transaction);
        var verticalEdges = CollectHingeVerticalEdges(space, transaction);
        var count = 0;
        foreach (var hingeId in hingeIds)
        {
            if (transaction.GetObject(hingeId, OpenMode.ForRead) is not Circle hinge)
            {
                continue;
            }

            var extendsRight = ResolveHingeLabelDirection(hinge.Center, doorBounds, verticalEdges);
            var placement = HardwareLayoutCalculator.CalculateHingeTypeLabelPlacement(
                hinge.Center.X,
                hinge.Radius,
                extendsRight,
                options.HingeTypeTextSpacing);
            AppendEntity(
                space,
                transaction,
                new MText
                {
                    Contents = hingeType,
                    Location = new Point3d(placement.X, hinge.Center.Y, hinge.Center.Z),
                    Attachment = placement.ExtendsRight
                        ? AttachmentPoint.MiddleLeft
                        : AttachmentPoint.MiddleRight,
                    TextHeight = options.HingeTypeTextHeight,
                    Layer = HingeLayerName
                });
            count++;
        }

        transaction.Commit();
        return count;
    }

    private static IReadOnlyList<DoorBoundsCandidate> CollectHingeDoorBounds(
        BlockTableRecord space,
        Transaction transaction)
    {
        var bounds = new List<DoorBoundsCandidate>();
        foreach (var objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is Entity entity &&
                !string.Equals(entity.Layer, HingeLayerName, StringComparison.OrdinalIgnoreCase) &&
                TryReadAxisAlignedRectangle(entity, out var rectangle))
            {
                bounds.Add(new DoorBoundsCandidate(
                    rectangle,
                    string.Equals(entity.Layer, DoorLayerName, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return bounds;
    }

    private static IReadOnlyList<VerticalLineSpan> CollectHingeVerticalEdges(
        BlockTableRecord space,
        Transaction transaction)
    {
        var edges = new List<VerticalLineSpan>();
        foreach (var objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is Entity entity &&
                !string.Equals(entity.Layer, HingeLayerName, StringComparison.OrdinalIgnoreCase) &&
                TryReadVerticalSpan(entity, out var edge))
            {
                edges.Add(edge);
            }
        }

        return edges;
    }

    private static bool ResolveHingeLabelDirection(
        Point3d center,
        IReadOnlyList<DoorBoundsCandidate> doorBounds,
        IReadOnlyList<VerticalLineSpan> verticalEdges)
    {
        var door = doorBounds
            .Where(candidate => candidate.Bounds.Contains(center))
            .OrderByDescending(candidate => candidate.Preferred)
            .ThenBy(candidate => candidate.Bounds.Area)
            .Select(candidate => (AxisAlignedRectangle?)candidate.Bounds)
            .FirstOrDefault();
        if (door is not null)
        {
            return Math.Abs(center.X - door.Value.Left) <= Math.Abs(center.X - door.Value.Right);
        }

        var nearestEdge = verticalEdges
            .Where(edge => center.Y >= edge.Bottom - Tolerance && center.Y <= edge.Top + Tolerance)
            .OrderBy(edge => Math.Abs(center.X - edge.X))
            .Select(edge => (VerticalLineSpan?)edge)
            .FirstOrDefault();
        return nearestEdge is null || nearestEdge.Value.X <= center.X;
    }

    private static bool TryReadAxisAlignedRectangle(Entity entity, out AxisAlignedRectangle rectangle)
    {
        rectangle = default;
        if (entity is not Polyline { Closed: true, NumberOfVertices: 4 } polyline)
        {
            return false;
        }

        for (var index = 0; index < polyline.NumberOfVertices; index++)
        {
            var next = (index + 1) % polyline.NumberOfVertices;
            var start = polyline.GetPoint3dAt(index);
            var end = polyline.GetPoint3dAt(next);
            if (Math.Abs(polyline.GetBulgeAt(index)) > Tolerance ||
                (Math.Abs(start.X - end.X) > Tolerance && Math.Abs(start.Y - end.Y) > Tolerance))
            {
                return false;
            }
        }

        var extents = polyline.GeometricExtents;
        rectangle = new AxisAlignedRectangle(
            extents.MinPoint.X,
            extents.MaxPoint.X,
            extents.MinPoint.Y,
            extents.MaxPoint.Y);
        return rectangle.Width > Tolerance && rectangle.Height > Tolerance;
    }

    private static bool TrySelectHangingRodLines(
        Database database,
        Editor editor,
        out HorizontalLineSpan selectedLine,
        out HorizontalLineSpan topLine)
    {
        selectedLine = default;
        topLine = default;
        var prompt = new PromptEntityOptions("\n请选择柜内左到右的水平基准线: ");
        prompt.SetRejectMessage("\n请选择水平直线或两点水平多段线。");
        prompt.AddAllowedClass(typeof(Line), exactMatch: true);
        prompt.AddAllowedClass(typeof(Polyline), exactMatch: true);
        var result = editor.GetEntity(prompt);
        if (result.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return false;
        }

        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var selectedEntity = (Entity)transaction.GetObject(result.ObjectId, OpenMode.ForRead);
        if (!TryReadHorizontalSpan(selectedEntity, out selectedLine))
        {
            editor.WriteMessage("\n[错误] 所选对象必须是有长度的水平直线或两点水平多段线。");
            return false;
        }

        var horizontalCandidates = new List<HorizontalLineSpan>();
        var verticalCandidates = new List<VerticalLineSpan>();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (var objectId in space)
        {
            if (objectId == result.ObjectId)
            {
                continue;
            }

            var entity = transaction.GetObject(objectId, OpenMode.ForRead) as Entity;
            if (entity is not null && TryReadHorizontalSpan(entity, out var candidate))
            {
                horizontalCandidates.Add(candidate);
            }

            if (entity is not null && TryReadVerticalSpan(entity, out var verticalCandidate))
            {
                verticalCandidates.Add(verticalCandidate);
            }
        }

        selectedLine = HardwareLayoutCalculator.FindHorizontalBayAt(
            selectedLine,
            result.PickedPoint.X,
            verticalCandidates);
        var match = HardwareLayoutCalculator.FindNearestCoveringLineAbove(selectedLine, horizontalCandidates);
        if (match is null)
        {
            editor.WriteMessage("\n[错误] 未找到完整覆盖所选横线的上方水平线。");
            return false;
        }

        topLine = match.Value;
        return true;
    }

    private static bool TryReadHorizontalSpan(Entity entity, out HorizontalLineSpan span)
    {
        span = default;
        const double tolerance = 0.000001;
        if (!TryReadStraightEndpoints(entity, out var start, out var end) ||
            Math.Abs(start.Y - end.Y) > tolerance ||
            Math.Abs(start.X - end.X) <= tolerance)
        {
            return false;
        }

        span = new HorizontalLineSpan(Math.Min(start.X, end.X), Math.Max(start.X, end.X), (start.Y + end.Y) / 2);
        return true;
    }

    private static bool TryReadVerticalSpan(Entity entity, out VerticalLineSpan span)
    {
        span = default;
        const double tolerance = 0.000001;
        if (!TryReadStraightEndpoints(entity, out var start, out var end) ||
            Math.Abs(start.X - end.X) > tolerance ||
            Math.Abs(start.Y - end.Y) <= tolerance)
        {
            return false;
        }

        span = new VerticalLineSpan((start.X + end.X) / 2, Math.Min(start.Y, end.Y), Math.Max(start.Y, end.Y));
        return true;
    }

    private static bool TryReadStraightEndpoints(Entity entity, out Point3d start, out Point3d end)
    {
        const double tolerance = 0.000001;
        switch (entity)
        {
            case Line line:
                start = line.StartPoint;
                end = line.EndPoint;
                return true;
            case Polyline polyline when !polyline.Closed &&
                                        polyline.NumberOfVertices == 2 &&
                                        Math.Abs(polyline.GetBulgeAt(0)) <= tolerance:
                start = polyline.GetPoint3dAt(0);
                end = polyline.GetPoint3dAt(1);
                return true;
            default:
                start = default;
                end = default;
                return false;
        }
    }

    private static void AppendEntity(BlockTableRecord space, Transaction transaction, Entity entity)
    {
        space.AppendEntity(entity);
        transaction.AddNewlyCreatedDBObject(entity, true);
    }

    private static void EnsureLayer(Database database, Transaction transaction, string layerName)
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
            Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
        };
        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
    }

    private static bool TrySelectHingeDoorEdges(
        Database database,
        Editor editor,
        out AxisAlignedRectangle door)
    {
        door = default;
        if (!TrySelectHorizontalEdge(database, editor, "\n请选择门板上边线: ", out var topEdge) ||
            !TrySelectHorizontalEdge(database, editor, "\n请选择门板下边线: ", out var bottomEdge))
        {
            return false;
        }

        const double tolerance = 0.000001;
        var left = Math.Max(topEdge.Left, bottomEdge.Left);
        var right = Math.Min(topEdge.Right, bottomEdge.Right);
        var bottom = Math.Min(topEdge.Y, bottomEdge.Y);
        var top = Math.Max(topEdge.Y, bottomEdge.Y);
        if (right - left <= tolerance)
        {
            editor.WriteMessage("\n[错误] 门板上边线与下边线没有共同的水平范围。");
            return false;
        }

        if (top - bottom <= tolerance)
        {
            editor.WriteMessage("\n[错误] 门板上边线与下边线不能位于同一高度。");
            return false;
        }

        door = new AxisAlignedRectangle(left, right, bottom, top);
        return true;
    }

    private static bool TrySelectHorizontalEdge(
        Database database,
        Editor editor,
        string message,
        out HorizontalLineSpan edge)
    {
        edge = default;
        var prompt = new PromptEntityOptions(message);
        prompt.SetRejectMessage("\n请选择水平直线或两点水平多段线。");
        prompt.AddAllowedClass(typeof(Line), exactMatch: true);
        prompt.AddAllowedClass(typeof(Polyline), exactMatch: true);
        var result = editor.GetEntity(prompt);
        if (result.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return false;
        }

        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var entity = (Entity)transaction.GetObject(result.ObjectId, OpenMode.ForRead);
        if (!TryReadHorizontalSpan(entity, out edge))
        {
            editor.WriteMessage("\n[错误] 所选对象必须是有长度的水平直线或两点水平多段线。");
            return false;
        }

        return true;
    }

    private static bool TryGetLibraryItem(
        Editor editor,
        HardwareLibraryItem? item,
        string displayName,
        out HardwareLibraryItem selectedItem)
    {
        selectedItem = item!;
        if (item is not null && File.Exists(item.DwgPath))
        {
            return true;
        }

        editor.WriteMessage($"\n[提示] 尚未选择可用的{displayName} DWG，请先在五金件面板选择素材。");
        return false;
    }

    private static void InsertSelected(HardwareLibraryItem? item, string displayName)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var editor = document.Editor;
        if (!TryGetLibraryItem(editor, item, displayName, out var selectedItem))
        {
            return;
        }

        var pointResult = editor.GetPoint($"\n请指定{displayName}插入点: ");
        if (pointResult.Status != PromptStatus.OK)
        {
            editor.WriteMessage("\n[提示] 操作已取消。");
            return;
        }

        try
        {
            var blockId = ImportDrawingAsBlock(document.Database, selectedItem.DwgPath);
            InsertBlockReference(document.Database, blockId, pointResult.Value);
            editor.WriteMessage($"\n[提示] 已插入{displayName}：{selectedItem.Name}。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 插入{displayName}失败：{exception.Message}");
        }
    }

    internal static ObjectId ImportDrawingAsBlock(Database targetDatabase, string filePath)
    {
        var blockName = CreateBlockName(filePath);
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

    private static void InsertStretchedHangingRod(
        Database database,
        ObjectId blockId,
        HangingRodPlacement placement)
    {
        ObjectId referenceId;
        using (var transaction = database.TransactionManager.StartTransaction())
        {
            var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
            var reference = new BlockReference(new Point3d(placement.Left, placement.Y, 0), blockId);
            space.AppendEntity(reference);
            transaction.AddNewlyCreatedDBObject(reference, true);
            referenceId = reference.ObjectId;

            if (reference.IsDynamicBlock)
            {
                TrySetHangingRodDynamicLength(reference, placement.Width);
            }

            transaction.Commit();
        }

        try
        {
            FitHangingRodToPlacement(database, referenceId, placement);
        }
        catch
        {
            TryEraseBlockReference(database, referenceId);
            throw;
        }
    }

    private static void FitHangingRodToPlacement(
        Database database,
        ObjectId referenceId,
        HangingRodPlacement placement)
    {
        for (var pass = 0; pass < HangingRodFitPasses; pass++)
        {
            using var transaction = database.TransactionManager.StartTransaction();
            var reference = (BlockReference)transaction.GetObject(referenceId, OpenMode.ForWrite);
            var extents = reference.GeometricExtents;
            var measuredWidth = extents.MaxPoint.X - extents.MinPoint.X;
            EnsureValidHangingRodWidth(measuredWidth);

            var widthDifference = placement.Width - measuredWidth;
            if (Math.Abs(widthDifference) <= HangingRodFitTolerance ||
                !TryAdjustHangingRodDynamicLength(reference, widthDifference))
            {
                transaction.Commit();
                break;
            }

            transaction.Commit();
        }

        using (var transaction = database.TransactionManager.StartTransaction())
        {
            var reference = (BlockReference)transaction.GetObject(referenceId, OpenMode.ForWrite);
            var extents = reference.GeometricExtents;
            var measuredWidth = extents.MaxPoint.X - extents.MinPoint.X;
            EnsureValidHangingRodWidth(measuredWidth);

            if (Math.Abs(placement.Width - measuredWidth) > HangingRodFitTolerance)
            {
                var currentScale = reference.ScaleFactors;
                reference.ScaleFactors = new Scale3d(
                    currentScale.X * placement.Width / measuredWidth,
                    currentScale.Y,
                    currentScale.Z);
            }

            transaction.Commit();
        }

        using (var transaction = database.TransactionManager.StartTransaction())
        {
            var reference = (BlockReference)transaction.GetObject(referenceId, OpenMode.ForWrite);
            var finalExtents = reference.GeometricExtents;
            var finalWidth = finalExtents.MaxPoint.X - finalExtents.MinPoint.X;
            EnsureValidHangingRodWidth(finalWidth);
            if (Math.Abs(finalWidth - placement.Width) > HangingRodFitTolerance)
            {
                throw new InvalidOperationException("挂衣杆最终宽度校正失败，请检查素材的动态参数。");
            }

            reference.TransformBy(Matrix3d.Displacement(
                new Vector3d(placement.Left - finalExtents.MinPoint.X, 0, 0)));
            transaction.Commit();
        }
    }

    private static bool TrySetHangingRodDynamicLength(BlockReference reference, double targetWidth)
    {
        foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
        {
            if (IsHangingRodLengthProperty(property) && property.Value is double)
            {
                property.Value = targetWidth;
                return true;
            }
        }

        return false;
    }

    private static bool TryAdjustHangingRodDynamicLength(BlockReference reference, double worldWidthDifference)
    {
        if (!reference.IsDynamicBlock)
        {
            return false;
        }

        var scaleX = Math.Abs(reference.ScaleFactors.X);
        if (scaleX <= 0.000001)
        {
            return false;
        }

        foreach (DynamicBlockReferenceProperty property in reference.DynamicBlockReferencePropertyCollection)
        {
            if (!IsHangingRodLengthProperty(property) || property.Value is not double currentValue)
            {
                continue;
            }

            var adjustedValue = currentValue + worldWidthDifference / scaleX;
            if (!Compatibility.IsFinite(adjustedValue) || adjustedValue <= 0)
            {
                return false;
            }

            property.Value = adjustedValue;
            return true;
        }

        return false;
    }

    private static bool IsHangingRodLengthProperty(DynamicBlockReferenceProperty property)
    {
        var propertyName = property.PropertyName.ToUpperInvariant();
        return !property.ReadOnly &&
               (Compatibility.Contains(propertyName, "长度", StringComparison.Ordinal) ||
                Compatibility.Contains(propertyName, "距离", StringComparison.Ordinal) ||
                Compatibility.Contains(propertyName, "LENGTH", StringComparison.Ordinal) ||
                Compatibility.Contains(propertyName, "DISTANCE", StringComparison.Ordinal));
    }

    private static void EnsureValidHangingRodWidth(double width)
    {
        if (!Compatibility.IsFinite(width) || width <= 0.000001)
        {
            throw new InvalidOperationException("挂衣杆素材宽度无效，无法自动拉伸。");
        }
    }

    private static void TryEraseBlockReference(Database database, ObjectId referenceId)
    {
        try
        {
            using var transaction = database.TransactionManager.StartTransaction();
            if (transaction.GetObject(referenceId, OpenMode.ForWrite, false) is BlockReference reference)
            {
                reference.Erase();
            }

            transaction.Commit();
        }
        catch
        {
            // Preserve the original fitting error; cleanup failure must not replace it.
        }
    }

    private static void InsertBlockReference(Database database, ObjectId blockId, Point3d insertionPoint)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        var reference = new BlockReference(insertionPoint, blockId);
        space.AppendEntity(reference);
        transaction.AddNewlyCreatedDBObject(reference, true);
        transaction.Commit();
    }

    private static string CreateBlockName(string filePath)
    {
        var sourceName = Path.GetFileNameWithoutExtension(filePath);
        var builder = new StringBuilder(sourceName.Length);
        foreach (var character in sourceName)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_');
        }

        var safeName = builder.Length == 0 ? "BLOCK" : builder.ToString();
        if (safeName.Length > 60)
        {
            safeName = safeName.Substring(0, 60);
        }

        return $"DC_HW_{safeName}_{File.GetLastWriteTimeUtc(filePath).Ticks:X}";
    }

    private readonly record struct AxisAlignedRectangle(double Left, double Right, double Bottom, double Top)
    {
        public double Width => Right - Left;

        public double Height => Top - Bottom;

        public double Area => Width * Height;

        public bool Contains(Point3d point)
        {
            return point.X >= Left - Tolerance && point.X <= Right + Tolerance &&
                   point.Y >= Bottom - Tolerance && point.Y <= Top + Tolerance;
        }
    }

    private readonly record struct DoorBoundsCandidate(AxisAlignedRectangle Bounds, bool Preferred);
}

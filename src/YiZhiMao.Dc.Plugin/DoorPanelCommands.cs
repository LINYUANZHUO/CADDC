using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using DbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

public static class DoorPanelCommands
{
    private const string DoorLayerName = "DC_门板";
    private const string OpeningLayerName = "DC_开门线";
    private const string HingeLayerName = "DC_铰链";
    private const string MaskLayerName = "DC_门板遮罩";
    private const double HingeCupDiameter = 35;
    private const double HingeCupOffset = 23;
    private const double ShelfClearance = 40;
    private const double HingeMinimumSpacing = 50;
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_ELEVATION_DOOR", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "生成立面门板", "DCMB")]
    public static void GenerateElevationDoor()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var options = DoorPanelRuntimeSettings.Current;
        var editor = document.Editor;
        var doorCount = Math.Max(1, options.DoorCount);
        var singleDoorHingeOnLeft = true;
        editor.WriteMessage("\n>>> 进入立面门板生成；依次点击柜体内空两个对角点，按 ESC 退出。");
        while (true)
        {
            if (!TryPromptTwoPointBounds(editor, out var innerBounds))
            {
                editor.WriteMessage("\n>>> 已退出立面门板生成。");
                return;
            }

            if (!TryPreviewLayout(
                    editor,
                    innerBounds,
                    options,
                    ref doorCount,
                    ref singleDoorHingeOnLeft,
                    out var layout))
            {
                editor.WriteMessage("\n>>> 已退出立面门板生成。");
                return;
            }

            try
            {
                var activeOptions = options with { DoorCount = doorCount };
                var shelves = activeOptions.AvoidShelves
                    ? CollectHorizontalSpans(
                        document.Database,
                        innerBounds.Left,
                        innerBounds.Right,
                        innerBounds.Bottom,
                        innerBounds.Top)
                    : [];
                WriteLayout(
                    document.Database,
                    layout,
                    innerBounds,
                    activeOptions,
                    HardwareRuntimeSettings.Current,
                    shelves);
                DoorPanelRuntimeSettings.Current = activeOptions;
                editor.WriteMessage(
                    $"\n[一只猫dc] 已生成 {layout.Panels.Count} 块门板；盖值 上 {options.TopExtension:0.###}、下 {options.BottomExtension:0.###}、左 {options.LeftExtension:0.###}、右 {options.RightExtension:0.###} mm。继续选择下一处内空的两个对角点，或按 ESC 退出。");
            }
            catch (Exception exception)
            {
                editor.WriteMessage($"\n[错误] 生成立面门板失败：{exception.Message}");
            }
        }
    }

    [CommandMethod("DCMB", CommandFlags.Modal)]
    public static void GenerateElevationDoorShortcut()
    {
        GenerateElevationDoor();
    }

    private static bool TryPromptTwoPointBounds(Editor editor, out DoorPanelBounds bounds)
    {
        bounds = default;
        var firstResult = editor.GetPoint("\n请选择柜体内空第一个角点: ");
        if (firstResult.Status != PromptStatus.OK)
        {
            return false;
        }

        var secondOptions = new PromptCornerOptions("\n请选择柜体内空对角点: ", firstResult.Value)
        {
            UseDashedLine = true
        };
        var secondResult = editor.GetCorner(secondOptions);
        if (secondResult.Status != PromptStatus.OK)
        {
            return false;
        }

        bounds = new DoorPanelBounds(
            Math.Min(firstResult.Value.X, secondResult.Value.X),
            Math.Max(firstResult.Value.X, secondResult.Value.X),
            Math.Min(firstResult.Value.Y, secondResult.Value.Y),
            Math.Max(firstResult.Value.Y, secondResult.Value.Y));
        if (bounds.Width > Tolerance && bounds.Height > Tolerance)
        {
            return true;
        }

        editor.WriteMessage("\n[提示] 两个角点必须形成有效的柜体内空范围。");
        return false;
    }

    private static bool TryPreviewLayout(
        Editor editor,
        DoorPanelBounds bounds,
        DoorPanelOptions options,
        ref int doorCount,
        ref bool singleDoorHingeOnLeft,
        out DoorPanelLayout layout)
    {
        layout = null!;
        var jig = new DoorPanelCountPreviewJig(
            bounds,
            options,
            doorCount,
            singleDoorHingeOnLeft);
        using var hotkeys = new DoorPanelHotkeyScope(jig);
        var result = editor.Drag(jig);
        doorCount = jig.DoorCount;
        singleDoorHingeOnLeft = jig.SingleDoorHingeOnLeft;
        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        if (jig.TryGetLayout(out layout))
        {
            return true;
        }

        editor.WriteMessage("\n[提示] 当前门板数量与门缝无法放入该内空，请减少门板数量后重试。");
        return false;
    }

    private static IReadOnlyList<DoorOpeningSegment> CollectDoorOpeningSegments(Database database)
    {
        var segments = new List<DoorOpeningSegment>();
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (var objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity ||
                IsGeneratedDoorLayer(entity.Layer))
            {
                continue;
            }

            segments.AddRange(ReadDoorOpeningSegments(entity));
        }

        return segments;
    }

    private static bool IsGeneratedDoorLayer(string layerName)
    {
        return string.Equals(layerName, DoorLayerName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(layerName, OpeningLayerName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(layerName, HingeLayerName, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<DoorOpeningSegment> ReadDoorOpeningSegments(Entity entity)
    {
        if (entity is Line line)
        {
            if (Math.Abs(line.StartPoint.X - line.EndPoint.X) <= Tolerance ||
                Math.Abs(line.StartPoint.Y - line.EndPoint.Y) <= Tolerance)
            {
                yield return new DoorOpeningSegment(
                    line.StartPoint.X,
                    line.StartPoint.Y,
                    line.EndPoint.X,
                    line.EndPoint.Y);
            }

            yield break;
        }

        if (entity is not DbPolyline polyline)
        {
            yield break;
        }

        var segmentCount = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            var next = (index + 1) % polyline.NumberOfVertices;
            if (Math.Abs(polyline.GetBulgeAt(index)) > Tolerance)
            {
                continue;
            }

            var start = polyline.GetPoint3dAt(index);
            var end = polyline.GetPoint3dAt(next);
            if (Math.Abs(start.X - end.X) <= Tolerance || Math.Abs(start.Y - end.Y) <= Tolerance)
            {
                yield return new DoorOpeningSegment(start.X, start.Y, end.X, end.Y);
            }
        }
    }

    private static void WriteLayout(
        Database database,
        DoorPanelLayout layout,
        DoorPanelBounds shelfScope,
        DoorPanelOptions doorOptions,
        HardwareOptions hingeOptions,
        IReadOnlyList<HorizontalLineSpan> shelves)
    {
        var dashedLinetype = TryEnsureDashedLinetype(database);
        using var transaction = database.TransactionManager.StartTransaction();
        EnsureLayer(database, transaction, DoorLayerName, doorOptions.LineColorIndex, null);
        EnsureLayer(database, transaction, OpeningLayerName, 8, dashedLinetype);
        EnsureLayer(database, transaction, HingeLayerName, 2, null);
        if (doorOptions.GenerateMask)
        {
            EnsureLayer(database, transaction, MaskLayerName, 7, null);
        }
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);

        foreach (var panel in layout.Panels)
        {
            if (doorOptions.GenerateMask)
            {
                var mask = CreatePanelMask(database, panel);
                mask.Layer = MaskLayerName;
                AppendEntity(space, transaction, mask);
            }

            var outline = CreatePanelOutline(panel);
            outline.Layer = DoorLayerName;
            AppendEntity(space, transaction, outline);

            if (doorOptions.GenerateOpeningLines)
            {
                foreach (var line in CreateOpeningLines(panel))
                {
                    line.Layer = OpeningLayerName;
                    if (dashedLinetype is not null)
                    {
                        line.Linetype = dashedLinetype;
                    }

                    AppendEntity(space, transaction, line);
                }
            }

            if (doorOptions.GenerateHinges)
            {
                WritePanelHinges(database, space, transaction, panel, shelfScope, hingeOptions, shelves);
            }
        }

        transaction.Commit();
    }

    private static void WritePanelHinges(
        Database database,
        BlockTableRecord space,
        Transaction transaction,
        DoorPanelLeaf panel,
        DoorPanelBounds shelfScope,
        HardwareOptions options,
        IReadOnlyList<HorizontalLineSpan> shelves)
    {
        var offsets = HardwareLayoutCalculator.CalculateInnerOpeningHingeOffsets(
            panel,
            shelfScope,
            options);
        var radius = HingeCupDiameter / 2;
        var centerX = HardwareLayoutCalculator.CalculateHingeCenterX(
            panel.Left,
            panel.Right,
            panel.HingeOnLeft,
            HingeCupOffset,
            radius);
        offsets = HardwareLayoutCalculator.MoveHingeOffsetsAwayFromShelves(
            panel,
            shelfScope,
            offsets,
            shelves,
            ShelfClearance,
            HingeMinimumSpacing,
            HingeCupDiameter / 2 + 2);

        foreach (var offset in offsets)
        {
            var center = new Point3d(centerX, panel.Top - offset, 0);
            AppendEntity(
                space,
                transaction,
                new Circle(center, Vector3d.ZAxis, radius) { Layer = HingeLayerName });
            AppendEntity(
                space,
                transaction,
                new Line(
                    new Point3d(center.X - radius - 2, center.Y, 0),
                    new Point3d(center.X + radius + 2, center.Y, 0))
                { Layer = HingeLayerName });
            AppendEntity(
                space,
                transaction,
                new Line(
                    new Point3d(center.X, center.Y - radius - 2, 0),
                    new Point3d(center.X, center.Y + radius + 2, 0))
                { Layer = HingeLayerName });
        }

        if (!options.GenerateHingeDimensions)
        {
            return;
        }

        var sideX = panel.HingeOnLeft ? panel.Left : panel.Right;
        var inside = panel.HingeOnLeft ? 1d : -1d;
        var dimensions = HardwareLayoutCalculator.CalculateHingeDimensions(
            panel.Height,
            offsets,
            options.HingeDimensionMode);
        foreach (var dimension in dimensions)
        {
            var firstPoint = new Point3d(sideX, panel.Top - dimension.StartOffset, 0);
            var secondPoint = new Point3d(sideX, panel.Top - dimension.EndOffset, 0);
            var lineOffset = options.HingeDimensionOffset +
                             dimension.Level * options.HingeDimensionLineSpacing;
            var dimensionLinePoint = new Point3d(
                sideX + inside * lineOffset,
                (firstPoint.Y + secondPoint.Y) / 2,
                0);
            AppendEntity(
                space,
                transaction,
                new AlignedDimension(
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
                });
        }
    }

    private static IReadOnlyList<HorizontalLineSpan> CollectHorizontalSpans(
        Database database,
        double innerLeft,
        double innerRight,
        double innerBottom,
        double innerTop)
    {
        var spans = new List<HorizontalLineSpan>();
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (var objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity ||
                IsGeneratedDoorLayer(entity.Layer))
            {
                continue;
            }

            foreach (var span in ReadHorizontalSpans(entity))
            {
                if (span.Y > innerBottom + Tolerance &&
                    span.Y < innerTop - Tolerance &&
                    span.Right >= innerLeft - Tolerance &&
                    span.Left <= innerRight + Tolerance)
                {
                    spans.Add(span);
                }
            }
        }

        return spans;
    }

    private static IEnumerable<HorizontalLineSpan> ReadHorizontalSpans(Entity entity)
    {
        if (TryReadShelfSpanFromBounds(entity, out var shelfSpan))
        {
            yield return shelfSpan;
            yield break;
        }

        if (entity is Line line && Math.Abs(line.StartPoint.Y - line.EndPoint.Y) <= Tolerance)
        {
            yield return new HorizontalLineSpan(
                Math.Min(line.StartPoint.X, line.EndPoint.X),
                Math.Max(line.StartPoint.X, line.EndPoint.X),
                (line.StartPoint.Y + line.EndPoint.Y) / 2);
            yield break;
        }

        if (entity is not DbPolyline polyline)
        {
            yield break;
        }

        var segmentCount = polyline.Closed ? polyline.NumberOfVertices : polyline.NumberOfVertices - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            var next = (index + 1) % polyline.NumberOfVertices;
            if (Math.Abs(polyline.GetBulgeAt(index)) > Tolerance)
            {
                continue;
            }

            var start = polyline.GetPoint3dAt(index);
            var end = polyline.GetPoint3dAt(next);
            if (Math.Abs(start.Y - end.Y) <= Tolerance)
            {
                yield return new HorizontalLineSpan(
                    Math.Min(start.X, end.X),
                    Math.Max(start.X, end.X),
                    (start.Y + end.Y) / 2);
            }
        }
    }

    private static bool TryReadShelfSpanFromBounds(Entity entity, out HorizontalLineSpan span)
    {
        try
        {
            var extents = entity.GeometricExtents;
            var candidate = HardwareLayoutCalculator.CreateShelfSpanFromBounds(
                extents.MinPoint.X,
                extents.MaxPoint.X,
                extents.MinPoint.Y,
                extents.MaxPoint.Y);
            if (candidate is not null)
            {
                span = candidate.Value;
                return true;
            }
        }
        catch
        {
        }

        span = default;
        return false;
    }

    private static DbPolyline CreatePanelOutline(DoorPanelLeaf panel)
    {
        var polyline = new DbPolyline(4);
        polyline.AddVertexAt(0, new Point2d(panel.Left, panel.Bottom), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(panel.Right, panel.Bottom), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(panel.Right, panel.Top), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(panel.Left, panel.Top), 0, 0, 0);
        polyline.Closed = true;
        return polyline;
    }

    private static Wipeout CreatePanelMask(Database database, DoorPanelLeaf panel)
    {
        var boundary = new Point2dCollection
        {
            new(panel.Left, panel.Bottom),
            new(panel.Right, panel.Bottom),
            new(panel.Right, panel.Top),
            new(panel.Left, panel.Top),
            new(panel.Left, panel.Bottom)
        };
        var wipeout = new Wipeout();
        wipeout.SetDatabaseDefaults(database);
        wipeout.SetFrom(boundary, Vector3d.ZAxis);
        return wipeout;
    }

    private static DbPolyline CreatePreviewOutline(DoorPanelBounds bounds)
    {
        var polyline = new DbPolyline(4);
        polyline.AddVertexAt(0, new Point2d(bounds.Left, bounds.Bottom), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(bounds.Right, bounds.Bottom), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(bounds.Right, bounds.Top), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(bounds.Left, bounds.Top), 0, 0, 0);
        polyline.Closed = true;
        return polyline;
    }

    private static IEnumerable<Line> CreatePreviewDividerLines(DoorPanelLayout layout)
    {
        for (var index = 0; index < layout.Panels.Count - 1; index++)
        {
            var currentRight = layout.Panels[index].Right;
            var nextLeft = layout.Panels[index + 1].Left;
            yield return new Line(
                new Point3d(currentRight, layout.Bounds.Bottom, 0),
                new Point3d(currentRight, layout.Bounds.Top, 0));
            if (Math.Abs(nextLeft - currentRight) > Tolerance)
            {
                yield return new Line(
                    new Point3d(nextLeft, layout.Bounds.Bottom, 0),
                    new Point3d(nextLeft, layout.Bounds.Top, 0));
            }
        }
    }

    private static IEnumerable<Line> CreateOpeningLines(DoorPanelLeaf panel)
    {
        var openingEdgeX = panel.HingeOnLeft ? panel.Right : panel.Left;
        var hingeX = panel.HingeOnLeft ? panel.Left : panel.Right;
        var center = new Point3d(hingeX, (panel.Top + panel.Bottom) / 2, 0);
        yield return new Line(new Point3d(openingEdgeX, panel.Top, 0), center);
        yield return new Line(new Point3d(openingEdgeX, panel.Bottom, 0), center);
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

    private static void EnsureLayer(
        Database database,
        Transaction transaction,
        string layerName,
        short colorIndex,
        string? linetypeName)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layers.Has(layerName))
        {
            var existing = (LayerTableRecord)transaction.GetObject(layers[layerName], OpenMode.ForWrite);
            existing.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            return;
        }

        layers.UpgradeOpen();
        var layer = new LayerTableRecord
        {
            Name = layerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
        };
        if (linetypeName is not null)
        {
            var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
            if (linetypes.Has(linetypeName))
            {
                layer.LinetypeObjectId = linetypes[linetypeName];
            }
        }

        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
    }

    private static void AppendEntity(BlockTableRecord space, Transaction transaction, Entity entity)
    {
        space.AppendEntity(entity);
        transaction.AddNewlyCreatedDBObject(entity, true);
    }

    private sealed class DoorPanelCountPreviewJig : DrawJig
    {
        private readonly DoorPanelBounds _bounds;
        private readonly DoorPanelOptions _options;
        private Point3d _cursorPoint;
        private int _revision;
        private int _sampledRevision = -1;

        public DoorPanelCountPreviewJig(
            DoorPanelBounds bounds,
            DoorPanelOptions options,
            int doorCount,
            bool singleDoorHingeOnLeft)
        {
            _bounds = bounds;
            _options = options;
            DoorCount = Math.Max(1, doorCount);
            SingleDoorHingeOnLeft = singleDoorHingeOnLeft;
            while (DoorCount > 1 && !CanCalculate(DoorCount))
            {
                DoorCount--;
            }

            _cursorPoint = new Point3d(
                (bounds.Left + bounds.Right) / 2,
                (bounds.Bottom + bounds.Top) / 2,
                0);
        }

        public int DoorCount { get; private set; }

        public bool SingleDoorHingeOnLeft { get; private set; }

        public bool AdjustDoorCount(int delta)
        {
            var candidate = Math.Max(1, DoorCount + delta);
            if (candidate == DoorCount || !CanCalculate(candidate))
            {
                return false;
            }

            DoorCount = candidate;
            _revision++;
            return true;
        }

        public bool SetSingleDoorHingeSide(bool hingeOnLeft)
        {
            if (DoorCount != 1 || SingleDoorHingeOnLeft == hingeOnLeft)
            {
                return false;
            }

            SingleDoorHingeOnLeft = hingeOnLeft;
            _revision++;
            return true;
        }

        public bool TryGetLayout(out DoorPanelLayout layout)
        {
            try
            {
                layout = DoorPanelLayoutCalculator.Calculate(
                    _bounds.Left,
                    _bounds.Right,
                    _bounds.Bottom,
                    _bounds.Top,
                    _options with { DoorCount = DoorCount },
                    SingleDoorHingeOnLeft);
                return true;
            }
            catch
            {
                layout = null!;
                return false;
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var prompt = new JigPromptPointOptions(
                $"\n请移动鼠标预览 [直接按键盘 +/- 或 W/S 增减数量，无需回车] 并点击左键确认（当前 {DoorCount} 扇，单门 L/R 换铰链侧）: ")
            {
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByOrthoMode |
                                    UserInputControls.GovernedByUCSDetect
            };
            var result = prompts.AcquirePoint(prompt);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(_cursorPoint) <= Tolerance && _sampledRevision == _revision)
            {
                return SamplerStatus.NoChange;
            }

            _cursorPoint = result.Value;
            _sampledRevision = _revision;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            using var outline = CreatePreviewOutline(layout.Bounds);
            outline.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
            draw.Geometry.Draw(outline);
            foreach (var line in CreatePreviewDividerLines(layout))
            {
                using (line)
                {
                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                    draw.Geometry.Draw(line);
                }
            }

            return true;
        }

        private bool CanCalculate(int doorCount)
        {
            try
            {
                DoorPanelLayoutCalculator.Calculate(
                    _bounds.Left,
                    _bounds.Right,
                    _bounds.Bottom,
                    _bounds.Top,
                    _options with { DoorCount = doorCount },
                    SingleDoorHingeOnLeft);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class DoorPanelHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyW = 0x57;
        private const int VirtualKeyS = 0x53;
        private const int VirtualKeyL = 0x4C;
        private const int VirtualKeyR = 0x52;
        private const int VirtualKeyAdd = 0x6B;
        private const int VirtualKeySubtract = 0x6D;
        private const int VirtualKeyOemPlus = 0xBB;
        private const int VirtualKeyOemMinus = 0xBD;

        private readonly DoorPanelCountPreviewJig _jig;
        private bool _disposed;

        public DoorPanelHotkeyScope(DoorPanelCountPreviewJig jig)
        {
            _jig = jig;
            AcadApplication.PreTranslateMessage += OnPreTranslateMessage;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            AcadApplication.PreTranslateMessage -= OnPreTranslateMessage;
            _disposed = true;
        }

        private void OnPreTranslateMessage(object? sender, PreTranslateMessageEventArgs eventArgs)
        {
            var message = eventArgs.Message;
            if (message.message is not WmKeyDown and not WmSystemKeyDown)
            {
                return;
            }

            var key = message.wParam.ToInt32();
            bool? changed = key switch
            {
                VirtualKeyW or VirtualKeyAdd or VirtualKeyOemPlus => _jig.AdjustDoorCount(1),
                VirtualKeyS or VirtualKeySubtract or VirtualKeyOemMinus => _jig.AdjustDoorCount(-1),
                VirtualKeyL => _jig.SetSingleDoorHingeSide(true),
                VirtualKeyR => _jig.SetSingleDoorHingeSide(false),
                _ => null
            };

            if (changed is null)
            {
                return;
            }

            eventArgs.Handled = true;
            if (changed.Value)
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
}

using System.Runtime.InteropServices;
using System.Windows.Interop;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using DbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using Exception = System.Exception;

namespace YiZhiMao.Dc.Plugin;

public static class DrawerFaceCommands
{
    private const string FaceLayerName = "DC_抽面";
    private const string OpeningLayerName = "DC_开门线";
    private const string MaskLayerName = "DC_抽面遮罩";
    private const string HandlelessProfileLayerName = "DC_免拉手侧视图";
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_DRAWER_FACE", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "生成抽面", "DCMC")]
    public static void GenerateDrawerFaces()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            GenerateDrawerFaces(document);
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 抽面生成失败：{exception.Message}");
        }
    }

    private static void GenerateDrawerFaces(Document document)
    {
        var editor = document.Editor;
        var options = DrawerFaceRuntimeSettings.Current;
        if (!BoardCommands.TryGetBoardSpaceBounds(document, out var bounds, out var automaticallyDetected))
        {
            return;
        }

        var first = new DrawerFacePoint(bounds.Left, bounds.Bottom);
        var second = new DrawerFacePoint(bounds.Right, bounds.Top);
        var jig = new DrawerFacePreviewJig(first, second, options);
        if (!jig.TryGetLayout(out var initialLayout))
        {
            editor.WriteMessage("\n[一只猫dc] 当前内空无法按所设行列数与缝隙生成抽面。");
            return;
        }

        DrawerFaceRuntimeSettings.PublishLayout(initialLayout);
        using var hotkeys = new DrawerFaceHotkeyScope(jig);
        var dragResult = editor.Drag(jig);
        if (dragResult.Status != PromptStatus.OK || !jig.TryGetLayout(out var layout))
        {
            editor.WriteMessage("\n[提示] 抽面生成已取消。");
            return;
        }

        WriteLayout(document.Database, layout, options);
        options = options with
        {
            Count = jig.RowCount,
            RowCount = jig.RowCount,
            ColumnCount = jig.ColumnCount
        };
        DrawerFaceRuntimeSettings.Save(options);
        DrawerFaceRuntimeSettings.PublishLayout(layout);
        var source = automaticallyDetected ? "智能识别内空" : "手动框选内空";
        editor.WriteMessage(
            $"\n[一只猫dc] 已按{source}生成 {layout.RowCount} 行 × {layout.ColumnCount} 列，共 {layout.Faces.Count} 块抽面；" +
            $"单块 {layout.FaceWidth:0.###} × {layout.FaceHeight:0.###} mm，左右缝 {layout.ActualHorizontalGap:0.###} mm，上下缝 {layout.ActualVerticalGap:0.###} mm。");
    }

    [CommandMethod("DCMC", CommandFlags.Modal)]
    public static void GenerateDrawerFacesShortcut()
    {
        GenerateDrawerFaces();
    }

    [CommandMethod("DC_CMD_DRAWER_HANDLELESS_PROFILE", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "生成免拉手侧视图", "DCMLS")]
    public static void GenerateHandlelessProfile()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            GenerateHandlelessProfile(document);
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 免拉手侧视图生成失败：{exception.Message}");
        }
    }

    [CommandMethod("DCMLS", CommandFlags.Modal)]
    public static void GenerateHandlelessProfileShortcut()
    {
        GenerateHandlelessProfile();
    }

    private static void GenerateHandlelessProfile(Document document)
    {
        var editor = document.Editor;
        var drawerOptions = DrawerFaceRuntimeSettings.Current;
        var boardThickness = BoardRuntimeSettings.Thickness;
        var pickOptions = new PromptPointOptions(
            "\n请在免拉手缝隙、门板边线或端角附近单击 [手动定位(M)] <自动识别>: ");
        pickOptions.Keywords.Add("Manual", "M", "手动定位(M)");
        var pick = editor.GetPoint(pickOptions);
        HandlelessProfileAnchor anchor;
        var automaticallyDetected = false;
        if (pick.Status == PromptStatus.Keyword && pick.StringResult is "Manual" or "M")
        {
            if (!TryGetManualHandlelessAnchor(editor, drawerOptions.HandlelessProfileMode, out anchor))
            {
                return;
            }
        }
        else
        {
            if (pick.Status != PromptStatus.OK)
            {
                return;
            }

            var segments = CadAxisAlignedSegmentReader.CollectCurrentSpace(document.Database);
            automaticallyDetected = HandlelessProfileCalculator.TryDetect(
                new BoardPlacementPoint(pick.Value.X, pick.Value.Y),
                segments,
                drawerOptions.HandlelessProfileMode,
                boardThickness,
                drawerOptions.HandlelessProfileSize,
                drawerOptions.HandlelessGap,
                out anchor);
            if (!automaticallyDetected)
            {
                editor.WriteMessage("\n[一只猫dc] 附近没有识别到有效免拉手边界，已转为手动定位。");
                if (!TryGetManualHandlelessAnchor(editor, drawerOptions.HandlelessProfileMode, out anchor))
                {
                    return;
                }
            }
        }

        var jig = new HandlelessProfilePreviewJig(
            anchor,
            boardThickness,
            drawerOptions.HandlelessProfileSize,
            drawerOptions.LineColorIndex);
        var drag = editor.Drag(jig);
        if (drag.Status != PromptStatus.OK || !jig.TryGetLayout(out var layout))
        {
            editor.WriteMessage("\n[提示] 免拉手侧视图生成已取消。");
            return;
        }

        WriteHandlelessProfile(document.Database, layout, drawerOptions.LineColorIndex);
        var typeText = layout.Type == HandlelessProfileMode.Middle ? "中拉手 C 型" : "上拉手 L 型";
        var frontText = layout.FrontSide == HandlelessProfileFrontSide.Left ? "左侧柜前" : "右侧柜前";
        var sourceText = automaticallyDetected ? "自动识别" : "手动定位";
        editor.WriteMessage(
            $"\n[一只猫dc] 已按{sourceText}生成{typeText}，{frontText}，门板搭接 {layout.DoorOverlap:0.###} mm。");
    }

    private static bool TryGetManualHandlelessAnchor(
        Editor editor,
        HandlelessProfileMode requestedMode,
        out HandlelessProfileAnchor anchor)
    {
        var type = requestedMode;
        if (type == HandlelessProfileMode.Automatic)
        {
            var typeOptions = new PromptKeywordOptions(
                "\n请选择免拉手类型 [中拉手C型(C)/上拉手L型(L)] <中拉手C型>: ")
            {
                AllowNone = true
            };
            typeOptions.Keywords.Add("Middle", "C", "中拉手C型(C)");
            typeOptions.Keywords.Add("Top", "L", "上拉手L型(L)");
            var typeResult = editor.GetKeywords(typeOptions);
            if (typeResult.Status == PromptStatus.Cancel)
            {
                anchor = null!;
                return false;
            }

            type = typeResult.Status is PromptStatus.OK or PromptStatus.Keyword &&
                   typeResult.StringResult is "Top" or "L"
                ? HandlelessProfileMode.Top
                : HandlelessProfileMode.Middle;
        }

        var upperMessage = type == HandlelessProfileMode.Middle
            ? "\n请点取上门板下边的内侧端点: "
            : "\n请点取顶部柜板下边、靠门板内侧的位置: ";
        var upper = editor.GetPoint(upperMessage);
        if (upper.Status != PromptStatus.OK)
        {
            anchor = null!;
            return false;
        }

        var lowerMessage = type == HandlelessProfileMode.Middle
            ? "\n请点取下门板上边的内侧端点: "
            : "\n请点取门板上边的内侧端点: ";
        var lower = editor.GetPoint(new PromptPointOptions(lowerMessage)
        {
            BasePoint = upper.Value,
            UseBasePoint = true,
            UseDashedLine = true
        });
        if (lower.Status != PromptStatus.OK)
        {
            anchor = null!;
            return false;
        }

        var upperY = Math.Max(upper.Value.Y, lower.Value.Y);
        var lowerY = Math.Min(upper.Value.Y, lower.Value.Y);
        var innerX = type == HandlelessProfileMode.Top
            ? lower.Value.X
            : (upper.Value.X + lower.Value.X) / 2;
        anchor = new HandlelessProfileAnchor(type, innerX, innerX, upperY, lowerY);
        return true;
    }

    private static void WriteHandlelessProfile(
        Database database,
        HandlelessProfileLayout layout,
        short colorIndex)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        EnsureLayer(database, transaction, HandlelessProfileLayerName, colorIndex, null);
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        foreach (var board in layout.Boards)
        {
            using var outline = CreateHandlelessProfileOutline(board);
            outline.Layer = HandlelessProfileLayerName;
            space.AppendEntity(outline);
            transaction.AddNewlyCreatedDBObject(outline, true);
        }

        transaction.Commit();
    }

    private static DbPolyline CreateHandlelessProfileOutline(HandlelessProfileRectangle board)
    {
        var polyline = new DbPolyline(4) { Closed = true };
        polyline.AddVertexAt(0, new Point2d(board.X, board.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(board.Right, board.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(board.Right, board.Top), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(board.X, board.Top), 0, 0, 0);
        return polyline;
    }

    private static void WriteLayout(
        Database database,
        DrawerFaceLayout layout,
        DrawerFaceOptions options)
    {
        var dashedLinetype = options.GenerateOpeningLines ? TryEnsureDashedLinetype(database) : null;

        using var transaction = database.TransactionManager.StartTransaction();
        EnsureLayer(database, transaction, FaceLayerName, options.LineColorIndex, null);
        if (options.GenerateOpeningLines)
        {
            EnsureLayer(database, transaction, OpeningLayerName, 8, dashedLinetype);
        }

        if (options.GenerateMask)
        {
            EnsureLayer(database, transaction, MaskLayerName, 7, null);
        }

        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        foreach (var face in layout.Faces)
        {
            if (options.GenerateMask)
            {
                using var wipeout = CreateMask(database, face);
                wipeout.Layer = MaskLayerName;
                space.AppendEntity(wipeout);
                transaction.AddNewlyCreatedDBObject(wipeout, true);
            }

            using var outline = CreateOutline(face);
            outline.Layer = FaceLayerName;
            space.AppendEntity(outline);
            transaction.AddNewlyCreatedDBObject(outline, true);

            if (!options.GenerateOpeningLines)
            {
                continue;
            }

            foreach (var segment in DrawerFaceLayoutCalculator.CalculateOpeningArrow(face))
            {
                var line = new Line(
                    new Point3d(segment.Start.X, segment.Start.Y, 0),
                    new Point3d(segment.End.X, segment.End.Y, 0))
                {
                    Layer = OpeningLayerName,
                    Linetype = dashedLinetype ?? "ByLayer"
                };

                space.AppendEntity(line);
                transaction.AddNewlyCreatedDBObject(line, true);
            }
        }

        transaction.Commit();
    }

    private static DbPolyline CreateOutline(DrawerFaceRectangle face)
    {
        var polyline = new DbPolyline(4) { Closed = true };
        polyline.AddVertexAt(0, new Point2d(face.X, face.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(face.Right, face.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(face.Right, face.Top), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(face.X, face.Top), 0, 0, 0);
        return polyline;
    }

    private static Wipeout CreateMask(Database database, DrawerFaceRectangle face)
    {
        var boundary = new Point2dCollection
        {
            new(face.X, face.Y),
            new(face.Right, face.Y),
            new(face.Right, face.Top),
            new(face.X, face.Top),
            new(face.X, face.Y)
        };
        var wipeout = new Wipeout();
        wipeout.SetDatabaseDefaults(database);
        wipeout.SetFrom(boundary, Vector3d.ZAxis);
        return wipeout;
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

    private sealed class HandlelessProfilePreviewJig : DrawJig
    {
        private readonly HandlelessProfileAnchor anchor;
        private readonly double boardThickness;
        private readonly double profileSize;
        private readonly short colorIndex;
        private readonly Point3d center;
        private Point3d cursorPoint;

        public HandlelessProfilePreviewJig(
            HandlelessProfileAnchor anchor,
            double boardThickness,
            double profileSize,
            short colorIndex)
        {
            this.anchor = anchor;
            this.boardThickness = boardThickness;
            this.profileSize = profileSize;
            this.colorIndex = colorIndex;
            center = new Point3d(
                (anchor.DoorEdgeMinX + anchor.DoorEdgeMaxX) / 2,
                (anchor.UpperBoundaryY + anchor.LowerBoundaryY) / 2,
                0);
            cursorPoint = new Point3d(center.X + profileSize, center.Y, 0);
        }

        public bool TryGetLayout(out HandlelessProfileLayout layout)
        {
            try
            {
                var frontSide = cursorPoint.X < center.X
                    ? HandlelessProfileFrontSide.Left
                    : HandlelessProfileFrontSide.Right;
                layout = HandlelessProfileCalculator.Calculate(
                    anchor,
                    frontSide,
                    boardThickness,
                    profileSize);
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
            var options = new JigPromptPointOptions(
                "\n将鼠标移到左侧或右侧柜前方向，预览免拉手侧视图，单击确认: ")
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

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            foreach (var board in layout.Boards)
            {
                using var outline = CreateHandlelessProfileOutline(board);
                outline.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                draw.Geometry.Draw(outline);
            }

            return true;
        }
    }

    private sealed class DrawerFacePreviewJig : DrawJig
    {
        private readonly DrawerFacePoint first;
        private readonly DrawerFacePoint second;
        private readonly DrawerFaceOptions options;
        private Point3d cursorPoint;
        private int revision;
        private int sampledRevision = -1;

        public DrawerFacePreviewJig(DrawerFacePoint first, DrawerFacePoint second, DrawerFaceOptions options)
        {
            this.first = first;
            this.second = second;
            this.options = options;
            RowCount = Math.Max(1, options.RowCount);
            ColumnCount = Math.Max(1, options.ColumnCount);
            cursorPoint = new Point3d((first.X + second.X) / 2, (first.Y + second.Y) / 2, 0);
            while (RowCount > 1 && !CanCalculate(RowCount, ColumnCount))
            {
                RowCount--;
            }

            while (ColumnCount > 1 && !CanCalculate(RowCount, ColumnCount))
            {
                ColumnCount--;
            }
        }

        public int RowCount { get; private set; }

        public int ColumnCount { get; private set; }

        public bool AdjustRows(int delta)
        {
            var candidate = Math.Max(1, RowCount + delta);
            if (candidate == RowCount || !CanCalculate(candidate, ColumnCount))
            {
                return false;
            }

            RowCount = candidate;
            revision++;
            PublishLayout();
            return true;
        }

        public bool AdjustColumns(int delta)
        {
            var candidate = Math.Max(1, ColumnCount + delta);
            if (candidate == ColumnCount || !CanCalculate(RowCount, candidate))
            {
                return false;
            }

            ColumnCount = candidate;
            revision++;
            PublishLayout();
            return true;
        }

        public bool TryGetLayout(out DrawerFaceLayout layout)
        {
            try
            {
                layout = DrawerFaceLayoutCalculator.Calculate(
                    first,
                    second,
                    options with
                    {
                        Count = RowCount,
                        RowCount = RowCount,
                        ColumnCount = ColumnCount
                    });
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
                $"\n当前 {RowCount} 行 × {ColumnCount} 列；W/S、上/下或 +/- 调整行数，A/D 或左/右调整列数，左键确认: ")
            {
                Cursor = CursorType.Crosshair,
                UserInputControls = UserInputControls.Accept3dCoordinates
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

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            foreach (var face in layout.Faces)
            {
                using var outline = CreateOutline(face);
                outline.Color = Color.FromColorIndex(ColorMethod.ByAci, options.LineColorIndex);
                draw.Geometry.Draw(outline);
                if (!options.GenerateOpeningLines)
                {
                    continue;
                }

                foreach (var segment in DrawerFaceLayoutCalculator.CalculateOpeningArrow(face))
                {
                    using var line = new Line(
                        new Point3d(segment.Start.X, segment.Start.Y, 0),
                        new Point3d(segment.End.X, segment.End.Y, 0));
                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, 8);
                    draw.Geometry.Draw(line);
                }
            }

            return true;
        }

        private void PublishLayout()
        {
            if (TryGetLayout(out var layout))
            {
                DrawerFaceRuntimeSettings.PublishLayout(layout);
            }
        }

        private bool CanCalculate(int rowCount, int columnCount)
        {
            try
            {
                DrawerFaceLayoutCalculator.Calculate(
                    first,
                    second,
                    options with
                    {
                        Count = rowCount,
                        RowCount = rowCount,
                        ColumnCount = columnCount
                    });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class DrawerFaceHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyLeft = 0x25;
        private const int VirtualKeyUp = 0x26;
        private const int VirtualKeyRight = 0x27;
        private const int VirtualKeyDown = 0x28;
        private const int VirtualKeyAdd = 0x6B;
        private const int VirtualKeySubtract = 0x6D;
        private const int VirtualKeyOemPlus = 0xBB;
        private const int VirtualKeyOemMinus = 0xBD;
        private const int VirtualKeyA = 0x41;
        private const int VirtualKeyD = 0x44;
        private const int VirtualKeyS = 0x53;
        private const int VirtualKeyW = 0x57;

        private readonly DrawerFacePreviewJig jig;
        private bool disposed;

        public DrawerFaceHotkeyScope(DrawerFacePreviewJig jig)
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

            var key = message.wParam.ToInt32();
            var rowDelta = key switch
            {
                VirtualKeyAdd or VirtualKeyOemPlus => 1,
                VirtualKeySubtract or VirtualKeyOemMinus => -1,
                VirtualKeyW or VirtualKeyUp => 1,
                VirtualKeyS or VirtualKeyDown => -1,
                _ => 0
            };
            var columnDelta = key switch
            {
                VirtualKeyD or VirtualKeyRight => 1,
                VirtualKeyA or VirtualKeyLeft => -1,
                _ => 0
            };
            if (rowDelta == 0 && columnDelta == 0)
            {
                return;
            }

            eventArgs.Handled = true;
            var changed = rowDelta != 0
                ? jig.AdjustRows(rowDelta)
                : jig.AdjustColumns(columnDelta);
            if (changed)
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

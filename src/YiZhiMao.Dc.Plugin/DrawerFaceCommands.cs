using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.IO;
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
    private const string HandleLayerName = "DC_抽面拉手";
    private const string MaskLayerName = "DC_抽面遮罩";
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

        var editor = document.Editor;
        var options = DrawerFaceRuntimeSettings.Current;
        var selectedHandle = DrawerFaceRuntimeSettings.SelectedHandle;
        if (options.GenerateHandle && (selectedHandle is null || !File.Exists(selectedHandle.DwgPath)))
        {
            editor.WriteMessage("\n[提示] 已启用生成拉手，请先在抽面设置中选择有效的拉手 DWG。");
            return;
        }

        var firstResult = editor.GetPoint("\n请选择柜体内空第一角点: ");
        if (firstResult.Status != PromptStatus.OK)
        {
            return;
        }

        var secondOptions = new PromptPointOptions("\n请选择柜体内空对角点: ")
        {
            BasePoint = firstResult.Value,
            UseBasePoint = true,
            UseDashedLine = true
        };
        var secondResult = editor.GetPoint(secondOptions);
        if (secondResult.Status != PromptStatus.OK)
        {
            return;
        }

        var first = new DrawerFacePoint(firstResult.Value.X, firstResult.Value.Y);
        var second = new DrawerFacePoint(secondResult.Value.X, secondResult.Value.Y);
        var jig = new DrawerFacePreviewJig(first, second, options);
        if (!jig.TryGetLayout(out var initialLayout))
        {
            editor.WriteMessage("\n[提示] 当前柜体内空无法按所设缝隙与数量生成抽面。");
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

        try
        {
            WriteLayout(document.Database, layout, options, selectedHandle);
            options = options with { Count = jig.Count };
            DrawerFaceRuntimeSettings.Current = options;
            DrawerFaceRuntimeSettings.PublishLayout(layout);
            editor.WriteMessage(
                $"\n[一只猫dc] 已生成 {layout.Faces.Count} 块抽面；单块 {layout.FaceWidth:0.###} × {layout.FaceHeight:0.###} mm，实际缝隙 {layout.ActualGap:0.###} mm。");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\n[错误] 生成抽面失败：{exception.Message}");
        }
    }

    [CommandMethod("DCMC", CommandFlags.Modal)]
    public static void GenerateDrawerFacesShortcut()
    {
        GenerateDrawerFaces();
    }

    private static void WriteLayout(
        Database database,
        DrawerFaceLayout layout,
        DrawerFaceOptions options,
        HardwareLibraryItem? selectedHandle)
    {
        var handleBlockId = options.GenerateHandle && selectedHandle is not null
            ? HardwareCommands.ImportDrawingAsBlock(database, selectedHandle.DwgPath)
            : ObjectId.Null;
        var dashedLinetype = TryEnsureDashedLinetype(database);

        using var transaction = database.TransactionManager.StartTransaction();
        EnsureLayer(database, transaction, FaceLayerName, options.LineColorIndex, null);
        EnsureLayer(database, transaction, OpeningLayerName, 8, dashedLinetype);
        if (options.GenerateMask)
        {
            EnsureLayer(database, transaction, MaskLayerName, 7, null);
        }

        if (!handleBlockId.IsNull)
        {
            EnsureLayer(database, transaction, HandleLayerName, 2, null);
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

            foreach (var segment in DrawerFaceLayoutCalculator.CalculateOpeningArrow(face))
            {
                var line = new Line(
                    new Point3d(segment.Start.X, segment.Start.Y, 0),
                    new Point3d(segment.End.X, segment.End.Y, 0))
                {
                    Layer = OpeningLayerName
                };
                if (dashedLinetype is not null)
                {
                    line.Linetype = dashedLinetype;
                }

                space.AppendEntity(line);
                transaction.AddNewlyCreatedDBObject(line, true);
            }
        }

        if (!handleBlockId.IsNull)
        {
            foreach (var center in layout.HandleCenters)
            {
                using var reference = new BlockReference(new Point3d(center.X, center.Y, 0), handleBlockId)
                {
                    Layer = HandleLayerName
                };
                space.AppendEntity(reference);
                transaction.AddNewlyCreatedDBObject(reference, true);
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
            Count = Math.Max(1, options.Count);
            cursorPoint = new Point3d((first.X + second.X) / 2, (first.Y + second.Y) / 2, 0);
            while (Count > 1 && !CanCalculate(Count))
            {
                Count--;
            }
        }

        public int Count { get; private set; }

        public DrawerFaceOrientation Orientation => options.Orientation;

        public bool AdjustCount(int delta)
        {
            var candidate = Math.Max(1, Count + delta);
            if (candidate == Count || !CanCalculate(candidate))
            {
                return false;
            }

            Count = candidate;
            revision++;
            if (TryGetLayout(out var layout))
            {
                DrawerFaceRuntimeSettings.PublishLayout(layout);
            }

            return true;
        }

        public bool TryGetLayout(out DrawerFaceLayout layout)
        {
            try
            {
                layout = DrawerFaceLayoutCalculator.Calculate(first, second, options with { Count = Count });
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
            var direction = Orientation == DrawerFaceOrientation.Vertical ? "竖向" : "横向";
            var prompt = new JigPromptPointOptions(
                $"\n{direction} {Count} 块；左/右或 +/- 调整数量，竖向也可用上/下，左键确认: ")
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
            }

            return true;
        }

        private bool CanCalculate(int count)
        {
            try
            {
                DrawerFaceLayoutCalculator.Calculate(first, second, options with { Count = count });
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
            var delta = key switch
            {
                VirtualKeyAdd or VirtualKeyOemPlus => 1,
                VirtualKeySubtract or VirtualKeyOemMinus => -1,
                VirtualKeyRight => 1,
                VirtualKeyLeft => -1,
                VirtualKeyUp when jig.Orientation == DrawerFaceOrientation.Vertical => 1,
                VirtualKeyDown when jig.Orientation == DrawerFaceOrientation.Vertical => -1,
                _ => 0
            };
            if (delta == 0)
            {
                return;
            }

            eventArgs.Handled = true;
            if (jig.AdjustCount(delta))
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

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

public static class PlanDoorPanelCommands
{
    private const string DoorLayerName = "DC_门板";
    private const double Tolerance = 0.000001;

    [CommandMethod("DC_CMD_PLAN_DOOR", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "生成俯视门板", "DCMBF")]
    public static void GeneratePlanDoor()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var options = PlanDoorPanelRuntimeSettings.Current;
        var doorCount = Math.Max(1, options.DoorCount);
        var editor = document.Editor;
        editor.WriteMessage("\n>>> 进入俯视门板生成；依次指定柜体前沿起点和终点，按 ESC 退出。");
        while (true)
        {
            if (!TryPromptBaseline(editor, out var start, out var end))
            {
                editor.WriteMessage("\n>>> 已退出俯视门板生成。");
                return;
            }

            if (!TryPreviewLayout(editor, start, end, options, ref doorCount, out var layout))
            {
                editor.WriteMessage("\n>>> 已退出俯视门板生成。");
                return;
            }

            try
            {
                WriteLayout(document.Database, layout, options);
                options = options with { DoorCount = doorCount };
                PlanDoorPanelRuntimeSettings.Current = options;
                PlanDoorPanelRuntimeSettings.PublishActualGap(layout.ActualGap);
                editor.WriteMessage(
                    $"\n[一只猫dc] 已生成 {layout.Panels.Count} 块俯视门板；离柜体前沿 {options.FrontClearance:0.###} mm，厚度 {options.Thickness:0.###} mm，实际门缝 {layout.ActualGap:0.###} mm。继续指定下一组柜体前沿，或按 ESC 退出。");
            }
            catch (Exception exception)
            {
                editor.WriteMessage($"\n[错误] 生成俯视门板失败：{exception.Message}");
            }
        }
    }

    [CommandMethod("DCMBF", CommandFlags.Modal)]
    public static void GeneratePlanDoorShortcut()
    {
        GeneratePlanDoor();
    }

    private static bool TryPromptBaseline(Editor editor, out PlanDoorPanelPoint start, out PlanDoorPanelPoint end)
    {
        start = default;
        end = default;
        var firstResult = editor.GetPoint("\n请选择柜体前沿起点: ");
        if (firstResult.Status != PromptStatus.OK)
        {
            return false;
        }

        var secondOptions = new PromptPointOptions("\n请选择柜体前沿终点（自动取水平或垂直方向）: ")
        {
            BasePoint = firstResult.Value,
            UseBasePoint = true,
            UseDashedLine = true
        };
        var secondResult = editor.GetPoint(secondOptions);
        if (secondResult.Status != PromptStatus.OK)
        {
            return false;
        }

        start = new PlanDoorPanelPoint(firstResult.Value.X, firstResult.Value.Y);
        var deltaX = secondResult.Value.X - firstResult.Value.X;
        var deltaY = secondResult.Value.Y - firstResult.Value.Y;
        end = Math.Abs(deltaX) >= Math.Abs(deltaY)
            ? new PlanDoorPanelPoint(secondResult.Value.X, firstResult.Value.Y)
            : new PlanDoorPanelPoint(firstResult.Value.X, secondResult.Value.Y);
        if (Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2)) > Tolerance)
        {
            return true;
        }

        editor.WriteMessage("\n[提示] 起点和终点必须形成有效的水平或垂直柜体前沿。");
        return false;
    }

    private static bool TryPreviewLayout(
        Editor editor,
        PlanDoorPanelPoint start,
        PlanDoorPanelPoint end,
        PlanDoorPanelOptions options,
        ref int doorCount,
        out PlanDoorPanelLayout layout)
    {
        layout = null!;
        var jig = new PlanDoorPanelPreviewJig(start, end, options, doorCount);
        using var hotkeys = new PlanDoorPanelHotkeyScope(jig);
        var result = editor.Drag(jig);
        doorCount = jig.DoorCount;
        if (result.Status != PromptStatus.OK)
        {
            return false;
        }

        if (jig.TryGetLayout(out layout))
        {
            return true;
        }

        editor.WriteMessage("\n[提示] 当前门板数量和门缝无法放入所选柜体前沿，请减少门板数量后重试。");
        return false;
    }

    private static void WriteLayout(
        Database database,
        PlanDoorPanelLayout layout,
        PlanDoorPanelOptions options)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        EnsureLayer(database, transaction, DoorLayerName, options.LineColorIndex);
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        foreach (var panel in layout.Panels)
        {
            using var outline = CreateOutline(panel);
            outline.Layer = DoorLayerName;
            space.AppendEntity(outline);
            transaction.AddNewlyCreatedDBObject(outline, true);
        }

        transaction.Commit();
    }

    private static DbPolyline CreateOutline(PlanDoorPanelLeaf panel)
    {
        var polyline = new DbPolyline(4)
        {
            Closed = true
        };
        polyline.AddVertexAt(0, new Point2d(panel.InnerStart.X, panel.InnerStart.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(panel.InnerEnd.X, panel.InnerEnd.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(panel.OuterEnd.X, panel.OuterEnd.Y), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(panel.OuterStart.X, panel.OuterStart.Y), 0, 0, 0);
        return polyline;
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
        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
    }

    private sealed class PlanDoorPanelPreviewJig : DrawJig
    {
        private readonly PlanDoorPanelPoint start;
        private readonly PlanDoorPanelPoint end;
        private readonly PlanDoorPanelOptions options;
        private Point3d cursorPoint;
        private int revision;
        private int sampledRevision = -1;
        private bool flipDirection;

        public PlanDoorPanelPreviewJig(
            PlanDoorPanelPoint start,
            PlanDoorPanelPoint end,
            PlanDoorPanelOptions options,
            int doorCount)
        {
            this.start = start;
            this.end = end;
            this.options = options;
            DoorCount = Math.Max(1, doorCount);
            cursorPoint = CreateDefaultCursor(start, end, options.Thickness);
            while (DoorCount > 1 && !CanCalculate(DoorCount))
            {
                DoorCount--;
            }
        }

        public int DoorCount { get; private set; }

        public bool AdjustDoorCount(int delta)
        {
            var candidate = Math.Max(1, DoorCount + delta);
            if (candidate == DoorCount || !CanCalculate(candidate))
            {
                return false;
            }

            DoorCount = candidate;
            revision++;
            PublishActualGap();
            return true;
        }

        public bool FlipDirection()
        {
            flipDirection = !flipDirection;
            revision++;
            return true;
        }

        public bool TryGetLayout(out PlanDoorPanelLayout layout)
        {
            try
            {
                layout = PlanDoorPanelLayoutCalculator.Calculate(
                    start,
                    end,
                    new PlanDoorPanelPoint(cursorPoint.X, cursorPoint.Y),
                    options with { DoorCount = DoorCount },
                    flipDirection);
                PlanDoorPanelRuntimeSettings.PublishActualGap(layout.ActualGap);
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
            var summary = TryGetLayout(out var layout)
                ? $"当前 {DoorCount} 扇，实际门缝 {layout.ActualGap:0.###} mm，厚度 {options.Thickness:0.###} mm"
                : $"当前 {DoorCount} 扇";
            var prompt = new JigPromptPointOptions(
                $"\n移动鼠标指定生成侧，R翻转，W/+增加、S/-减少，左键确认（{summary}）: ")
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

            foreach (var panel in layout.Panels)
            {
                using var outline = CreateOutline(panel);
                outline.Color = Color.FromColorIndex(ColorMethod.ByAci, options.LineColorIndex);
                draw.Geometry.Draw(outline);
            }

            return true;
        }

        private bool CanCalculate(int doorCount)
        {
            try
            {
                PlanDoorPanelLayoutCalculator.Calculate(
                    start,
                    end,
                    new PlanDoorPanelPoint(cursorPoint.X, cursorPoint.Y),
                    options with { DoorCount = doorCount },
                    flipDirection);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PublishActualGap()
        {
            if (TryGetLayout(out var layout))
            {
                PlanDoorPanelRuntimeSettings.PublishActualGap(layout.ActualGap);
            }
        }

        private static Point3d CreateDefaultCursor(
            PlanDoorPanelPoint start,
            PlanDoorPanelPoint end,
            double thickness)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var normalX = deltaY / length;
            var normalY = -deltaX / length;
            return new Point3d(
                (start.X + end.X) / 2 + normalX * thickness * 2,
                (start.Y + end.Y) / 2 + normalY * thickness * 2,
                0);
        }
    }

    private sealed class PlanDoorPanelHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyW = 0x57;
        private const int VirtualKeyS = 0x53;
        private const int VirtualKeyR = 0x52;
        private const int VirtualKeyAdd = 0x6B;
        private const int VirtualKeySubtract = 0x6D;
        private const int VirtualKeyOemPlus = 0xBB;
        private const int VirtualKeyOemMinus = 0xBD;

        private readonly PlanDoorPanelPreviewJig jig;
        private bool disposed;

        public PlanDoorPanelHotkeyScope(PlanDoorPanelPreviewJig jig)
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
            bool? changed = key switch
            {
                VirtualKeyW or VirtualKeyAdd or VirtualKeyOemPlus => jig.AdjustDoorCount(1),
                VirtualKeyS or VirtualKeySubtract or VirtualKeyOemMinus => jig.AdjustDoorCount(-1),
                VirtualKeyR => jig.FlipDirection(),
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

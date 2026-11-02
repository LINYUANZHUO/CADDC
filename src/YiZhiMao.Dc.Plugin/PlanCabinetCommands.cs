using System.Runtime.InteropServices;
using System.Text.Json;
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

public static class PlanCabinetCommands
{
    private const string AppName = "YIZHIMAO_DC_PLAN_CABINET";
    private const string SnapshotRecordName = "YIZHIMAO_DC_PLAN_CABINET_OPTIONS";
    private const string StructureLayerName = "0-柜体结构";
    private const string HiddenLayerName = "0-柜体虚线";
    private const string DoorLayerName = "0-门板";
    private const int SnapshotChunkLength = 200;
    private const double Tolerance = 0.000001;
    private static readonly JsonSerializerOptions JsonOptions = new();

    [CommandMethod("DC_CMD_PLAN_CABINET_GENERATE", CommandFlags.Modal)]
    [ShortcutCommand("柜体", "生成柜体俯视图", "DCFST")]
    public static void GeneratePlanCabinet()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var options = PlanCabinetRuntimeSettings.Current;
            if (!options.TryValidate(out var error))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 俯视图参数无效：{error}");
                return;
            }

            var jig = new PlanCabinetPlacementJig(options);
            using var hotkeys = new DoorCountHotkeyScope(jig);
            if (document.Editor.Drag(jig).Status != PromptStatus.OK || !jig.TryGetLayout(out var layout))
            {
                return;
            }

            options = options with { DoorCount = jig.DoorCount };
            PlanCabinetRuntimeSettings.Save(options);
            WritePlanCabinet(
                document.Database,
                options,
                layout,
                jig.Position,
                Guid.NewGuid().ToString("N"));
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 已生成柜体俯视图：长 {options.Length:0.###}，深 {options.Depth:0.###}，{options.DoorCount} 扇门。前方位于图形下侧。");
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 柜体俯视图生成失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_PLAN_CABINET_REPLACE_DOORS", CommandFlags.Modal)]
    [ShortcutCommand("柜体", "俯视图门板替换", "DCMBT2")]
    public static void ReplacePlanCabinetDoors()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var selection = document.Editor.GetEntity(
                new PromptEntityOptions("\n请选择由 DCFST 生成的柜体俯视图任一矩形: "));
            if (selection.Status != PromptStatus.OK ||
                !TryReadGroup(document.Database, selection.ObjectId, out var group))
            {
                document.Editor.WriteMessage("\n[一只猫dc] 选择对象不是可替换门板的 DCFST 柜体俯视图。");
                return;
            }

            var current = PlanCabinetRuntimeSettings.Current;
            var options = MergeDoorOptions(group.Snapshot.Options, current);
            if (!options.TryValidate(out var error))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 当前门板参数无效：{error}");
                return;
            }

            if (options.GenerateDoors)
            {
                var jig = new DoorReplacementJig(options, group.Origin);
                using var hotkeys = new DoorCountHotkeyScope(jig);
                if (document.Editor.Drag(jig).Status != PromptStatus.OK)
                {
                    return;
                }

                options = options with { DoorCount = jig.DoorCount };
            }

            ReplaceDoors(document.Database, group, options);
            PlanCabinetRuntimeSettings.Save(current with { DoorCount = options.DoorCount });
            document.Editor.WriteMessage(
                options.GenerateDoors
                    ? $"\n[一只猫dc] 已仅替换俯视图门板，柜体结构未修改；当前 {options.DoorCount} 扇。"
                    : "\n[一只猫dc] 已移除俯视图门板，柜体结构未修改。");
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 俯视图门板替换失败：{exception.Message}");
        }
    }

    private static PlanCabinetOptions MergeDoorOptions(
        PlanCabinetOptions original,
        PlanCabinetOptions current) =>
        original with
        {
            GenerateDoors = current.GenerateDoors,
            DoorThickness = current.DoorThickness,
            DoorGap = current.DoorGap,
            DoorLeftExtension = current.DoorLeftExtension,
            DoorRightExtension = current.DoorRightExtension,
            DoorCalculationMode = current.DoorCalculationMode,
            DoorFrontClearance = current.DoorFrontClearance,
            DoorCount = current.DoorCount
        };

    private static void WritePlanCabinet(
        Database database,
        PlanCabinetOptions options,
        PlanCabinetLayout layout,
        Point3d origin,
        string groupId)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        PrepareDrawing(database, transaction);
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        ObjectId anchorId = ObjectId.Null;
        foreach (var shape in layout.Shapes)
        {
            var id = AppendShape(database, transaction, space, shape, origin, groupId);
            if (anchorId.IsNull && shape.Kind != PlanCabinetPartKind.Door)
            {
                anchorId = id;
            }
        }

        if (!anchorId.IsNull)
        {
            var anchor = transaction.GetObject(anchorId, OpenMode.ForWrite);
            WriteSnapshot(anchor, transaction, new PlanCabinetSnapshot(options, origin.X, origin.Y));
        }

        transaction.Commit();
    }

    private static void ReplaceDoors(Database database, PlanCabinetGroup group, PlanCabinetOptions options)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        PrepareDrawing(database, transaction);
        foreach (var entity in group.Entities.Where(item => item.Kind == PlanCabinetPartKind.Door))
        {
            if (!entity.Id.IsNull && transaction.GetObject(entity.Id, OpenMode.ForWrite, false) is Entity drawingEntity)
            {
                drawingEntity.Erase();
            }
        }

        if (options.GenerateDoors)
        {
            var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
            foreach (var door in PlanCabinetLayoutCalculator.CalculateDoors(options))
            {
                AppendShape(database, transaction, space, door, group.Origin, group.Id);
            }
        }

        var anchor = transaction.GetObject(group.AnchorId, OpenMode.ForWrite);
        WriteSnapshot(anchor, transaction, new PlanCabinetSnapshot(options, group.Origin.X, group.Origin.Y));
        transaction.Commit();
    }

    private static ObjectId AppendShape(
        Database database,
        Transaction transaction,
        BlockTableRecord space,
        PlanCabinetShape shape,
        Point3d origin,
        string groupId)
    {
        var layer = shape.Kind == PlanCabinetPartKind.Door
            ? DoorLayerName
            : shape.Hidden ? HiddenLayerName : StructureLayerName;
        var polyline = CreateRectangle(shape.Bounds, origin, layer);
        space.AppendEntity(polyline);
        transaction.AddNewlyCreatedDBObject(polyline, true);
        AttachData(polyline, groupId, shape.Kind, shape.Index);
        return polyline.ObjectId;
    }

    private static DbPolyline CreateRectangle(CabinetRectangle bounds, Point3d origin, string layer)
    {
        var polyline = new DbPolyline(4)
        {
            Closed = true,
            Layer = layer,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        polyline.AddVertexAt(0, new Point2d(bounds.Left + origin.X, bounds.Bottom + origin.Y), 0, 0, 0);
        polyline.AddVertexAt(1, new Point2d(bounds.Right + origin.X, bounds.Bottom + origin.Y), 0, 0, 0);
        polyline.AddVertexAt(2, new Point2d(bounds.Right + origin.X, bounds.Top + origin.Y), 0, 0, 0);
        polyline.AddVertexAt(3, new Point2d(bounds.Left + origin.X, bounds.Top + origin.Y), 0, 0, 0);
        return polyline;
    }

    private static void PrepareDrawing(Database database, Transaction transaction)
    {
        var dashed = TryEnsureDashedLinetype(database);
        EnsureLayer(database, transaction, StructureLayerName, 1, null);
        EnsureLayer(database, transaction, DoorLayerName, 3, null);
        EnsureLayer(database, transaction, HiddenLayerName, 8, dashed);
        EnsureRegApp(database, transaction, AppName);
    }

    private static bool TryReadGroup(Database database, ObjectId selectedId, out PlanCabinetGroup group)
    {
        group = default!;
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        if (transaction.GetObject(selectedId, OpenMode.ForRead, false) is not Entity selected ||
            !TryReadData(selected, out var groupId, out _, out _))
        {
            return false;
        }

        var entities = new List<PlanCabinetEntity>();
        PlanCabinetSnapshot? snapshot = null;
        var anchorId = ObjectId.Null;
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, false) is not Entity entity ||
                !TryReadData(entity, out var candidateId, out var kind, out var index) ||
                !string.Equals(candidateId, groupId, StringComparison.Ordinal))
            {
                continue;
            }

            entities.Add(new PlanCabinetEntity(id, kind, index));
            if (snapshot is null && TryReadSnapshot(entity, transaction, out var candidateSnapshot))
            {
                snapshot = candidateSnapshot;
                anchorId = id;
            }
        }

        if (snapshot is null || anchorId.IsNull)
        {
            return false;
        }

        group = new PlanCabinetGroup(
            groupId,
            snapshot,
            new Point3d(snapshot.OriginX, snapshot.OriginY, 0),
            anchorId,
            entities);
        return true;
    }

    private static void AttachData(Entity entity, string groupId, PlanCabinetPartKind kind, int index)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, groupId),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, kind.ToString()),
            new TypedValue((int)DxfCode.ExtendedDataInteger32, index));
        entity.XData = data;
    }

    private static bool TryReadData(
        Entity entity,
        out string groupId,
        out PlanCabinetPartKind kind,
        out int index)
    {
        groupId = string.Empty;
        kind = default;
        index = 0;
        using var data = entity.GetXDataForApplication(AppName);
        if (data is null)
        {
            return false;
        }

        var values = data.AsArray();
        var strings = values
            .Where(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Select(value => value.Value as string)
            .ToArray();
        groupId = strings.ElementAtOrDefault(0) ?? string.Empty;
        var kindName = strings.ElementAtOrDefault(1);
        var integer = values.FirstOrDefault(value => value.TypeCode == (int)DxfCode.ExtendedDataInteger32);
        index = integer.Value is int value ? value : 0;
        return groupId.Length > 0 && Enum.TryParse(kindName, out kind);
    }

    private static void WriteSnapshot(
        DBObject entity,
        Transaction transaction,
        PlanCabinetSnapshot snapshot)
    {
        if (entity.ExtensionDictionary.IsNull)
        {
            entity.CreateExtensionDictionary();
        }

        var dictionary = (DBDictionary)transaction.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var values = Enumerable.Range(0, (json.Length + SnapshotChunkLength - 1) / SnapshotChunkLength)
            .Select(index => new TypedValue(
                (int)DxfCode.Text,
                json.Substring(
                    index * SnapshotChunkLength,
                    Math.Min(SnapshotChunkLength, json.Length - index * SnapshotChunkLength))))
            .ToArray();
        if (dictionary.Contains(SnapshotRecordName))
        {
            var existing = (Xrecord)transaction.GetObject(dictionary.GetAt(SnapshotRecordName), OpenMode.ForWrite);
            existing.Data = new ResultBuffer(values);
            return;
        }

        var record = new Xrecord { Data = new ResultBuffer(values) };
        dictionary.SetAt(SnapshotRecordName, record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static bool TryReadSnapshot(
        DBObject entity,
        Transaction transaction,
        out PlanCabinetSnapshot snapshot)
    {
        snapshot = default!;
        if (entity.ExtensionDictionary.IsNull)
        {
            return false;
        }

        var dictionary = (DBDictionary)transaction.GetObject(entity.ExtensionDictionary, OpenMode.ForRead);
        if (!dictionary.Contains(SnapshotRecordName) ||
            transaction.GetObject(dictionary.GetAt(SnapshotRecordName), OpenMode.ForRead) is not Xrecord record ||
            record.Data is null)
        {
            return false;
        }

        var json = string.Concat(record.Data.AsArray()
            .Where(value => value.TypeCode == (int)DxfCode.Text)
            .Select(value => value.Value as string));
        try
        {
            snapshot = JsonSerializer.Deserialize<PlanCabinetSnapshot>(json, JsonOptions)!;
            return snapshot?.Options is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void EnsureRegApp(Database database, Transaction transaction, string name)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(name))
        {
            return;
        }

        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = name };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static void EnsureLayer(
        Database database,
        Transaction transaction,
        string name,
        short colorIndex,
        string? linetype)
    {
        var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layers.Has(name))
        {
            var existing = (LayerTableRecord)transaction.GetObject(layers[name], OpenMode.ForWrite);
            existing.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            if (linetype is not null)
            {
                var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
                if (linetypes.Has(linetype))
                {
                    existing.LinetypeObjectId = linetypes[linetype];
                }
            }

            return;
        }

        layers.UpgradeOpen();
        var layer = new LayerTableRecord
        {
            Name = name,
            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
        };
        if (linetype is not null)
        {
            var linetypes = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
            if (linetypes.Has(linetype))
            {
                layer.LinetypeObjectId = linetypes[linetype];
            }
        }

        layers.Add(layer);
        transaction.AddNewlyCreatedDBObject(layer, true);
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

    private interface IDoorCountJig
    {
        bool AdjustDoorCount(int delta);
    }

    private sealed class PlanCabinetPlacementJig(PlanCabinetOptions options) : DrawJig, IDoorCountJig
    {
        private int revision;
        private int sampledRevision = -1;

        public Point3d Position { get; private set; }

        public int DoorCount { get; private set; } = Math.Max(1, options.DoorCount);

        public bool AdjustDoorCount(int delta)
        {
            var candidate = Math.Max(1, DoorCount + delta);
            if (candidate == DoorCount || !(options with { DoorCount = candidate }).TryValidate(out _))
            {
                return false;
            }

            DoorCount = candidate;
            revision++;
            return true;
        }

        public bool TryGetLayout(out PlanCabinetLayout layout)
        {
            try
            {
                layout = PlanCabinetLayoutCalculator.Calculate(options with { DoorCount = DoorCount });
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
            var result = prompts.AcquirePoint(new JigPromptPointOptions(
                $"\n移动鼠标放置俯视图左前角；W/+增加、S/-减少门数，当前 {DoorCount} 扇，单击确认: ")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            });
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(Position) <= Tolerance && sampledRevision == revision)
            {
                return SamplerStatus.NoChange;
            }

            Position = result.Value;
            sampledRevision = revision;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (!TryGetLayout(out var layout))
            {
                return true;
            }

            foreach (var shape in layout.Shapes)
            {
                using var rectangle = CreateRectangle(shape.Bounds, Position, "0");
                rectangle.Color = Color.FromColorIndex(
                    ColorMethod.ByAci,
                    shape.Kind == PlanCabinetPartKind.Door ? (short)3 : (short)1);
                draw.Geometry.Draw(rectangle);
            }

            return true;
        }
    }

    private sealed class DoorReplacementJig(PlanCabinetOptions options, Point3d origin) : DrawJig, IDoorCountJig
    {
        private Point3d cursor;
        private int revision;
        private int sampledRevision = -1;

        public int DoorCount { get; private set; } = Math.Max(1, options.DoorCount);

        public bool AdjustDoorCount(int delta)
        {
            var candidate = Math.Max(1, DoorCount + delta);
            if (candidate == DoorCount || !(options with { DoorCount = candidate }).TryValidate(out _))
            {
                return false;
            }

            DoorCount = candidate;
            revision++;
            return true;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var result = prompts.AcquirePoint(new JigPromptPointOptions(
                $"\n预览替换门板；W/+增加、S/-减少门数，当前 {DoorCount} 扇，单击确认: ")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.GovernedByUCSDetect
            });
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(cursor) <= Tolerance && sampledRevision == revision)
            {
                return SamplerStatus.NoChange;
            }

            cursor = result.Value;
            sampledRevision = revision;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            foreach (var door in PlanCabinetLayoutCalculator.CalculateDoors(options with { DoorCount = DoorCount }))
            {
                using var rectangle = CreateRectangle(door.Bounds, origin, "0");
                rectangle.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                draw.Geometry.Draw(rectangle);
            }

            return true;
        }
    }

    private sealed class DoorCountHotkeyScope : IDisposable
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSystemKeyDown = 0x0104;
        private const int VirtualKeyW = 0x57;
        private const int VirtualKeyS = 0x53;
        private const int VirtualKeyAdd = 0x6B;
        private const int VirtualKeySubtract = 0x6D;
        private const int VirtualKeyOemPlus = 0xBB;
        private const int VirtualKeyOemMinus = 0xBD;
        private readonly IDoorCountJig jig;
        private bool disposed;

        public DoorCountHotkeyScope(IDoorCountJig jig)
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

    private sealed record PlanCabinetSnapshot(PlanCabinetOptions Options, double OriginX, double OriginY);

    private sealed record PlanCabinetEntity(ObjectId Id, PlanCabinetPartKind Kind, int Index);

    private sealed record PlanCabinetGroup(
        string Id,
        PlanCabinetSnapshot Snapshot,
        Point3d Origin,
        ObjectId AnchorId,
        IReadOnlyList<PlanCabinetEntity> Entities);
}

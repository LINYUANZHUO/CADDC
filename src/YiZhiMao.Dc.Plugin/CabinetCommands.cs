using System.Globalization;
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

public static class CabinetCommands
{
    private const string CabinetAppName = "YIZHIMAO_DC_CABINET";
    private const string SnapshotRecordName = "YIZHIMAO_DC_CABINET_OPTIONS";
    private const string StructureLayerName = "0-柜体结构";
    private const string AuxiliaryLayerName = "0-柜体辅助图";
    private const string HiddenLayerName = "0-柜体虚线";
    private const string TextLayerName = "0-柜体文字";
    private const int SnapshotChunkLength = 200;
    private const double Tolerance = 0.000001;
    private static readonly JsonSerializerOptions JsonOptions = new();

    [CommandMethod("DC_CMD_CABINET_STANDARD", CommandFlags.Modal)]
    [ShortcutCommand("柜体", "一键生成单元柜", "DCDG")]
    public static void GenerateStandardCabinet()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var options = CabinetRuntimeSettings.Current;
            if (!options.TryValidate(out var error))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 柜体参数无效：{error}");
                return;
            }

            var layout = CabinetLayoutCalculator.Calculate(options);
            var jig = new CabinetPlacementJig(layout);
            if (document.Editor.Drag(jig).Status != PromptStatus.OK)
            {
                return;
            }

            WriteCabinet(document.Database, options, layout, jig.Position, Guid.NewGuid().ToString("N"));
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 已生成标准单元柜：宽 {options.Width:0.###}，高 {options.Height:0.###}，深 {options.Depth:0.###}。" );
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 标准单元柜生成失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_CABINET_UNIVERSAL", CommandFlags.Modal)]
    [ShortcutCommand("柜体", "生成万能柜体", "DCWNG")]
    public static void GenerateUniversalCabinet()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var first = document.Editor.GetPoint("\n请选择万能柜体正视图第一个角点: ");
            if (first.Status != PromptStatus.OK)
            {
                return;
            }

            var second = document.Editor.GetCorner(
                new PromptCornerOptions("\n请选择正视图对角点（确定宽和高）: ", first.Value)
                {
                    UseDashedLine = true
                });
            if (second.Status != PromptStatus.OK)
            {
                return;
            }

            var left = Math.Min(first.Value.X, second.Value.X);
            var bottom = Math.Min(first.Value.Y, second.Value.Y);
            var options = CabinetRuntimeSettings.Current with
            {
                Width = Math.Abs(second.Value.X - first.Value.X),
                Height = Math.Abs(second.Value.Y - first.Value.Y)
            };
            if (!options.TryValidate(out var error))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 万能柜体范围无效：{error}");
                return;
            }

            var layout = CabinetLayoutCalculator.Calculate(options);
            WriteCabinet(
                document.Database,
                options,
                layout,
                new Point3d(left, bottom, 0),
                Guid.NewGuid().ToString("N"));
            document.Editor.WriteMessage(
                $"\n[一只猫dc] 已按两点生成万能柜体：宽 {options.Width:0.###}，高 {options.Height:0.###}，深 {options.Depth:0.###}。" );
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 万能柜体生成失败：{exception.Message}");
        }
    }

    [CommandMethod("DC_CMD_CABINET_REFRESH", CommandFlags.Modal)]
    [ShortcutCommand("柜体", "全局刷新柜体/板件", "DCXX")]
    public static void RefreshCabinets()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var cabinets = ReadCabinets(document.Database);
            var refreshed = 0;
            foreach (var cabinet in cabinets)
            {
                if (!cabinet.Snapshot.Options.TryValidate(out _))
                {
                    continue;
                }

                var layout = CabinetLayoutCalculator.Calculate(cabinet.Snapshot.Options);
                using var transaction = document.Database.TransactionManager.StartTransaction();
                foreach (var objectId in cabinet.Entities)
                {
                    if (!objectId.IsNull && transaction.GetObject(objectId, OpenMode.ForWrite, false) is Entity entity)
                    {
                        entity.Erase();
                    }
                }

                AppendCabinet(
                    document.Database,
                    transaction,
                    cabinet.Snapshot.Options,
                    layout,
                    new Point3d(cabinet.Snapshot.OriginX, cabinet.Snapshot.OriginY, 0),
                    cabinet.Id);
                transaction.Commit();
                refreshed++;
            }

            document.Editor.WriteMessage(
                refreshed == 0
                    ? "\n[一只猫dc] 当前空间没有可刷新的插件柜体。"
                    : $"\n[一只猫dc] 已按各柜体保存的参数刷新 {refreshed} 组柜体/板件。" );
        }
        catch (Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 柜体全局刷新失败：{exception.Message}");
        }
    }

    private static void WriteCabinet(
        Database database,
        CabinetOptions options,
        CabinetLayout layout,
        Point3d origin,
        string cabinetId)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        AppendCabinet(database, transaction, options, layout, origin, cabinetId);
        transaction.Commit();
    }

    private static void AppendCabinet(
        Database database,
        Transaction transaction,
        CabinetOptions options,
        CabinetLayout layout,
        Point3d origin,
        string cabinetId)
    {
        var dashedLinetype = TryEnsureDashedLinetype(database);
        EnsureLayer(database, transaction, StructureLayerName, 1, null);
        EnsureLayer(database, transaction, AuxiliaryLayerName, 1, null);
        EnsureLayer(database, transaction, HiddenLayerName, 1, dashedLinetype);
        EnsureLayer(database, transaction, TextLayerName, 1, null);
        EnsureRegApp(database, transaction, CabinetAppName);
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
        var parts = layout.Parts.ToDictionary(part => part.Kind);

        foreach (var shape in layout.Shapes)
        {
            var isCanonical = shape.View == layout.PrimaryView;
            var layer = shape.Hidden
                ? HiddenLayerName
                : isCanonical ? StructureLayerName : AuxiliaryLayerName;
            var polyline = CreateRectangle(shape.Bounds, origin, layer, shape.Hidden ? dashedLinetype : null);
            space.AppendEntity(polyline);
            transaction.AddNewlyCreatedDBObject(polyline, true);
            AttachCabinetData(polyline, cabinetId, shape.Part.ToString(), shape.View.ToString());
            if (isCanonical)
            {
                var part = parts[shape.Part];
                BoardCommands.SetBoardProperty(
                    database,
                    transaction,
                    polyline,
                    part.Category,
                    part.Name,
                    part.CutWidth,
                    part.CutHeight,
                    part.CutThickness);
            }
        }

        var labelBounds = layout.LabelBounds.Offset(origin.X, origin.Y);
        var labelHeight = Math.Max(25, Math.Min(50, Math.Min(options.Width, options.Height) * 0.035));
        var labelPoint = new Point3d(
            (labelBounds.Left + labelBounds.Right) / 2,
            (labelBounds.Bottom + labelBounds.Top) / 2,
            0);
        var label = new DBText
        {
            Position = labelPoint,
            HorizontalMode = TextHorizontalMode.TextCenter,
            VerticalMode = TextVerticalMode.TextVerticalMid,
            AlignmentPoint = labelPoint,
            Height = labelHeight,
            TextString = $"宽{Format(options.Width)}高{Format(options.Height)}深{Format(options.Depth)}板厚{Format(options.Thickness)}",
            Layer = TextLayerName,
            Color = Color.FromColorIndex(ColorMethod.ByLayer, 256)
        };
        space.AppendEntity(label);
        transaction.AddNewlyCreatedDBObject(label, true);
        label.AdjustAlignment(database);
        AttachCabinetData(label, cabinetId, "Anchor", layout.PrimaryView.ToString());
        WriteSnapshot(
            label,
            transaction,
            new CabinetSnapshot(options, origin.X, origin.Y));
    }

    private static DbPolyline CreateRectangle(
        CabinetRectangle bounds,
        Point3d origin,
        string layer,
        string? linetype)
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
        if (linetype is not null)
        {
            polyline.Linetype = linetype;
        }

        return polyline;
    }

    private static IReadOnlyList<CabinetGroup> ReadCabinets(Database database)
    {
        var grouped = new Dictionary<string, List<ObjectId>>(StringComparer.Ordinal);
        var snapshots = new Dictionary<string, CabinetSnapshot>(StringComparer.Ordinal);
        using var transaction = database.TransactionManager.StartOpenCloseTransaction();
        var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
        foreach (ObjectId objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity ||
                !TryReadCabinetId(entity, out var cabinetId))
            {
                continue;
            }

            if (!grouped.TryGetValue(cabinetId, out var entities))
            {
                entities = [];
                grouped[cabinetId] = entities;
            }

            entities.Add(objectId);
            if (!snapshots.ContainsKey(cabinetId) && TryReadSnapshot(entity, transaction, out var snapshot))
            {
                snapshots[cabinetId] = snapshot;
            }
        }

        return grouped
            .Where(pair => snapshots.ContainsKey(pair.Key))
            .Select(pair => new CabinetGroup(pair.Key, snapshots[pair.Key], pair.Value))
            .ToArray();
    }

    private static void AttachCabinetData(Entity entity, string cabinetId, string role, string view)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, CabinetAppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, cabinetId),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, role),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, view));
        entity.XData = data;
    }

    private static bool TryReadCabinetId(Entity entity, out string cabinetId)
    {
        cabinetId = string.Empty;
        using var data = entity.GetXDataForApplication(CabinetAppName);
        if (data is null)
        {
            return false;
        }

        cabinetId = data.AsArray()
            .FirstOrDefault(value => value.TypeCode == (int)DxfCode.ExtendedDataAsciiString)
            .Value as string ?? string.Empty;
        return !string.IsNullOrWhiteSpace(cabinetId);
    }

    private static void WriteSnapshot(DBObject entity, Transaction transaction, CabinetSnapshot snapshot)
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
        var record = new Xrecord { Data = new ResultBuffer(values) };
        dictionary.SetAt(SnapshotRecordName, record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    private static bool TryReadSnapshot(
        DBObject entity,
        Transaction transaction,
        out CabinetSnapshot snapshot)
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
            snapshot = JsonSerializer.Deserialize<CabinetSnapshot>(json, JsonOptions)!;
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

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record CabinetSnapshot(CabinetOptions Options, double OriginX, double OriginY);

    private sealed record CabinetGroup(
        string Id,
        CabinetSnapshot Snapshot,
        IReadOnlyList<ObjectId> Entities);

    private sealed class CabinetPlacementJig(CabinetLayout layout) : DrawJig
    {
        public Point3d Position { get; private set; }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var result = prompts.AcquirePoint(new JigPromptPointOptions(
                "\n移动鼠标放置柜体正视图左下基点，单击确认: ")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates |
                                    UserInputControls.NoZeroResponseAccepted |
                                    UserInputControls.NoNegativeResponseAccepted
            });
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.DistanceTo(Position) <= Tolerance)
            {
                return SamplerStatus.NoChange;
            }

            Position = result.Value;
            return SamplerStatus.OK;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            foreach (var shape in layout.Shapes)
            {
                using var preview = CreateRectangle(shape.Bounds, Position, "0", null);
                preview.Color = Color.FromColorIndex(ColorMethod.ByAci, shape.Hidden ? (short)8 : (short)2);
                if (!draw.Geometry.Draw(preview))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

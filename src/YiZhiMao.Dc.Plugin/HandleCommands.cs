using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using DbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace YiZhiMao.Dc.Plugin;

public static class HandleCommands
{
    private static ObjectId sourceId = ObjectId.Null;
    private static string sourceDrawing = string.Empty;

    [CommandMethod("DC_CMD_HANDLE_PICK", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "拾取拉手", "DCSDH")]
    public static void PickHandle()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var options = new PromptEntityOptions("\n请选择一个拉手块: ");
        options.SetRejectMessage("\n请选择块参照。");
        options.AddAllowedClass(typeof(BlockReference), exactMatch: false);
        var result = document.Editor.GetEntity(options);
        if (result.Status != PromptStatus.OK)
        {
            return;
        }

        sourceId = result.ObjectId;
        sourceDrawing = document.Name;
        document.Editor.WriteMessage("\n[一只猫dc] 已拾取当前拉手。输入 DCLAS 应用到门板/抽面。" );
    }

    [CommandMethod("DCSDH", CommandFlags.Modal)]
    public static void PickHandleShortcut() => PickHandle();

    [CommandMethod("DC_CMD_HANDLE_APPLY", CommandFlags.Modal)]
    [ShortcutCommand("门板抽面", "应用或替换拉手", "DCLAS")]
    public static void ApplyHandle()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        if (sourceId.IsNull || sourceId.IsErased ||
            !string.Equals(sourceDrawing, document.Name, StringComparison.OrdinalIgnoreCase))
        {
            document.Editor.WriteMessage("\n[提示] 请先在当前图纸中执行 DCSDH 拾取拉手块。" );
            return;
        }

        var selection = document.Editor.GetSelection(
            new PromptSelectionOptions { MessageForAdding = "\n请选择闭合门板/抽面或需要替换的拉手块: " },
            new SelectionFilter([
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE,INSERT")
            ]));
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        if (transaction.GetObject(sourceId, OpenMode.ForRead, false) is not BlockReference source)
        {
            document.Editor.WriteMessage("\n[提示] 已拾取的拉手已失效，请重新拾取。" );
            return;
        }

        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForWrite);
        var created = 0;
        foreach (var id in selection.Value.GetObjectIds())
        {
            if (id == sourceId)
            {
                continue;
            }

            var entity = transaction.GetObject(id, OpenMode.ForRead, false);
            Point3d insertionPoint;
            double rotation;
            if (entity is DbPolyline polyline && TryGetClosedRectangleCenter(polyline, out insertionPoint))
            {
                rotation = source.Rotation;
            }
            else if (entity is BlockReference existing)
            {
                insertionPoint = existing.Position;
                rotation = existing.Rotation;
                existing.UpgradeOpen();
                existing.Erase();
            }
            else
            {
                continue;
            }

            var reference = new BlockReference(insertionPoint, source.BlockTableRecord)
            {
                Layer = source.Layer,
                Rotation = rotation,
                ScaleFactors = source.ScaleFactors
            };
            space.AppendEntity(reference);
            transaction.AddNewlyCreatedDBObject(reference, true);
            CopyDynamicProperties(source, reference);
            created++;
        }

        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已应用或替换 {created} 个拉手。" );
    }

    [CommandMethod("DCLAS", CommandFlags.Modal)]
    public static void ApplyHandleShortcut() => ApplyHandle();

    private static bool TryGetClosedRectangleCenter(DbPolyline polyline, out Point3d center)
    {
        center = default;
        if (!polyline.Closed || polyline.NumberOfVertices != 4)
        {
            return false;
        }

        try
        {
            var extents = polyline.GeometricExtents;
            center = new Point3d(
                (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                (extents.MinPoint.Z + extents.MaxPoint.Z) / 2);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDynamicProperties(BlockReference source, BlockReference target)
    {
        if (!source.IsDynamicBlock || !target.IsDynamicBlock)
        {
            return;
        }

        var values = source.DynamicBlockReferencePropertyCollection
            .Cast<DynamicBlockReferenceProperty>()
            .Where(property => !property.ReadOnly)
            .ToDictionary(property => property.PropertyName, property => property.Value, StringComparer.OrdinalIgnoreCase);
        foreach (DynamicBlockReferenceProperty property in target.DynamicBlockReferencePropertyCollection)
        {
            if (!property.ReadOnly && values.TryGetValue(property.PropertyName, out var value))
            {
                try
                {
                    property.Value = value;
                }
                catch
                {
                }
            }
        }
    }
}

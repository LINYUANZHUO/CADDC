using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

internal static class DrawingLayerRuntimeSettings
{
    public static DrawingLayerToolOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.DrawingLayer;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.DrawingLayer = value);
    }

    public static void Save(DrawingLayerToolOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.DrawingLayer = value);

    public static double LinetypeScale => Current.LinetypeScale;
    public static double WallHatchScale => Current.WallHatchScale;
    public static string DoorSelectionLayer => Current.DoorSelectionLayer;
    public static string CabinetSelectionLayer => Current.CabinetSelectionLayer;
    public static string CustomSelectionLayerOne => Current.CustomSelectionLayerOne;
    public static string CustomSelectionLayerTwo => Current.CustomSelectionLayerTwo;
}

public static class DrawingLayerCommands
{
    private static Database? lastHiddenDatabase;
    private static IReadOnlyList<ObjectId> lastHiddenObjectIds = [];

    private static readonly (string Name, short Color)[] StandardLayers =
    [
        ("0-柜体结构", 1), ("0-柜体辅助图", 1), ("0-柜体虚线", 8), ("0-正视图背板", 3), ("0-分区线", 8),
        ("0-护墙板图纸", 1), ("0-门板图层", 3), ("0-门板遮罩", 7), ("0-柜门拉伸线", 4),
        ("0-智能拉伸线图层", 4), ("0-五金", 2), ("0-门板开虚线", 8), ("0-标注-柜体", 7),
        ("0-标注-门板", 7), ("0-拉手", 2), ("0-暗拉手", 2), ("0-衣杆", 2),
        ("0-灯带", 2), ("0-铰链", 2), ("0-墙体", 1), ("0-墙体填充", 8),
        ("0-文字", 7), ("0-柜体文字", 1), ("0-柜门中档", 3), ("0-抽屉盒", 3),
        ("0-拆单图表", 7), ("0-图框", 7), ("0-矩形打印", 7), ("0-板件标识图层", 7)
    ];

    [CommandMethod("DC_CMD_LAYER_MANAGER", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "快速图层管理器", "DCTC")]
    public static void ShowLayerManager()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        AcadApplication.ShowModalWindow(new LayerManagerWindow(document.Database));
    }

    [CommandMethod("DC_CMD_LAYER_STANDARDIZE", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "一键生成雷神标准图层", "DCTCJZ")]
    public static void CreateStandardLayers()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layers = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        var created = 0;
        var definitions = StandardLayers
            .Concat(DrawingLayerRuntimeSettings.Current.CategoryLayers.Values
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => (Name: name, Color: (short)7)))
            .GroupBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());
        foreach (var definition in definitions)
        {
            if (layers.Has(definition.Name))
            {
                continue;
            }

            layers.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = definition.Name,
                Color = Color.FromColorIndex(ColorMethod.ByAci, definition.Color)
            };
            layers.Add(layer);
            transaction.AddNewlyCreatedDBObject(layer, true);
            created++;
        }

        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已补齐 {created} 个标准图层；同名图层未覆盖。" );
    }

    [CommandMethod("DC_CMD_LAYER_NORMALIZE", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "一键智能归层", "DCTCGC")]
    public static void NormalizeLayers()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        CreateStandardLayers();
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForRead);
        var changed = 0;
        foreach (ObjectId id in space)
        {
            if (transaction.GetObject(id, OpenMode.ForRead, false) is not Entity entity ||
                !TryResolveTargetLayer(entity, out var target) ||
                string.Equals(entity.Layer, target, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entity.UpgradeOpen();
            entity.Layer = target;
            changed++;
        }

        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已安全归层 {changed} 个明确类型对象；未猜测普通柜体几何。" );
    }

    [CommandMethod("DC_CMD_DRAWING_PURGE", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "图纸终极清理", "DCPU")]
    public static void PurgeDrawing()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var options = new PromptKeywordOptions("\n深度清理会删除所有未使用的图层和块，是否继续? [是(Y)/否(N)] <否>: ");
        options.Keywords.Add("Y");
        options.Keywords.Add("N");
        options.Keywords.Default = "N";
        var result = document.Editor.GetKeywords(options);
        if (result.Status != PromptStatus.OK || !string.Equals(result.StringResult, "Y", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        document.SendStringToExecute("_.-PURGE _All * _No ", true, false, true);
    }

    [CommandMethod("DC_CMD_LAYER_HIDE_HARDWARE", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏五金图层", "DCTCWJ")]
    public static void HideHardware() => HideMatching(["五金", "铰链", "拉手", "衣杆", "灯带"]);

    [CommandMethod("DC_CMD_LAYER_HIDE_DOOR", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏门板图层", "DCTCMB")]
    public static void HideDoor() => HideMatching(["门板", "抽面", "开门线"]);

    [CommandMethod("DC_CMD_LAYER_HIDE_CABINET", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏柜体图层", "DCTCHG")]
    public static void HideCabinet() => HideMatching(["柜体", "背板", "分区线"]);

    [CommandMethod("DC_CMD_LAYER_HIDE_ANNOTATION", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏标注图层", "DCTCBZ")]
    public static void HideAnnotation() => HideMatching(["标注", "文字"]);

    [CommandMethod("DC_CMD_LAYER_SHOW_ALL", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "显示所有图层", "DCTCXS")]
    public static void ShowAllLayers()
    {
        SetLayerVisibility(_ => true, show: true);
    }

    [CommandMethod("DC_CMD_LAYER_MAKE_CURRENT", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "选取图形图层置为当前", "DCT1")]
    public static void MakeSelectedLayerCurrent()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var selected = document.Editor.GetEntity("\n请选择要置为当前图层的图形: ");
        if (selected.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        if (transaction.GetObject(selected.ObjectId, OpenMode.ForRead, false) is not Entity entity)
        {
            return;
        }

        var layer = (LayerTableRecord)transaction.GetObject(entity.LayerId, OpenMode.ForWrite);
        if (layer.IsDependent)
        {
            document.Editor.WriteMessage("\n[一只猫dc] 外部参照依赖图层不能置为当前图层。" );
            return;
        }

        layer.IsOff = false;
        layer.IsFrozen = false;
        layer.IsLocked = false;
        document.Database.Clayer = entity.LayerId;
        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 当前图层已切换为 {layer.Name}。" );
    }

    [CommandMethod("DC_CMD_ENTITY_HIDE", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("图层图纸", "隐藏选中图形", "DCH1")]
    public static void HideSelectedEntities() => SetSelectedEntityVisibility(visible: false, remember: true);

    [CommandMethod("DC_CMD_ENTITY_UNHIDE_LAST", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "取消最近隐藏", "DCH2")]
    public static void UnhideLastEntities()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null || !ReferenceEquals(lastHiddenDatabase, document.Database) || lastHiddenObjectIds.Count == 0)
        {
            document?.Editor.WriteMessage("\n[一只猫dc] 当前图纸没有可恢复的最近隐藏对象。" );
            return;
        }

        var restored = SetEntityVisibility(document, lastHiddenObjectIds, visible: true);
        lastHiddenObjectIds = [];
        document.Editor.WriteMessage($"\n[一只猫dc] 已恢复最近隐藏的 {restored} 个图形。" );
    }

    [CommandMethod("DC_CMD_ENTITY_SHOW_ALL", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "显示全部隐藏图形", "DCH3")]
    public static void ShowAllEntities()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var blocks = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
        var shown = 0;
        foreach (ObjectId blockId in blocks)
        {
            var block = (BlockTableRecord)transaction.GetObject(blockId, OpenMode.ForRead);
            if (block.IsFromExternalReference || block.IsFromOverlayReference)
            {
                continue;
            }

            foreach (ObjectId objectId in block)
            {
                if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity || entity.Visible)
                {
                    continue;
                }

                var writable = (Entity)transaction.GetObject(objectId, OpenMode.ForWrite, false, true);
                writable.Visible = true;
                shown++;
            }
        }

        transaction.Commit();
        lastHiddenObjectIds = [];
        document.Editor.Regen();
        document.Editor.WriteMessage($"\n[一只猫dc] 已显示 {shown} 个隐藏图形。" );
    }

    [CommandMethod("DC_CMD_ENTITY_LOCK_LAYERS", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("图层图纸", "锁定选中图形所在图层", "DCS1")]
    public static void LockSelectedEntityLayers() => SetSelectedLayersLocked(locked: true);

    [CommandMethod("DC_CMD_ENTITY_UNLOCK_LAYERS", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("图层图纸", "解锁选中图形所在图层", "DCS2")]
    public static void UnlockSelectedEntityLayers() => SetSelectedLayersLocked(locked: false);

    [CommandMethod("DC_CMD_ENTITY_UNLOCK_ALL_LAYERS", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "解锁全部图层", "DCS3")]
    public static void UnlockAllLayers()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layers = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        var changed = 0;
        foreach (ObjectId objectId in layers)
        {
            var layer = (LayerTableRecord)transaction.GetObject(objectId, OpenMode.ForWrite);
            if (!layer.IsLocked)
            {
                continue;
            }

            layer.IsLocked = false;
            changed++;
        }

        transaction.Commit();
        document.Editor.WriteMessage($"\n[一只猫dc] 已解锁 {changed} 个图层。" );
    }

    [CommandMethod("DC_CMD_SELECT_DOOR_LAYER", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "按门板配置智能选择", "DCX1")]
    public static void SelectDoorLayer() => SelectByConfiguredLayer(DrawingLayerRuntimeSettings.DoorSelectionLayer, "门板");

    [CommandMethod("DC_CMD_SELECT_CABINET_LAYER", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "按柜体配置智能选择", "DCX2")]
    public static void SelectCabinetLayer() => SelectByConfiguredLayer(DrawingLayerRuntimeSettings.CabinetSelectionLayer, "柜体");

    [CommandMethod("DC_CMD_SELECT_CUSTOM_LAYER_ONE", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "按自定义图层一选择", "DCX3")]
    public static void SelectCustomLayerOne() => SelectByConfiguredLayer(DrawingLayerRuntimeSettings.CustomSelectionLayerOne, "自定义一");

    [CommandMethod("DC_CMD_SELECT_CUSTOM_LAYER_TWO", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "按自定义图层二选择", "DCX4")]
    public static void SelectCustomLayerTwo() => SelectByConfiguredLayer(DrawingLayerRuntimeSettings.CustomSelectionLayerTwo, "自定义二");

    [CommandMethod("DC_CMD_SELECT_ALL_ANNOTATIONS", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "选择所有标注", "DCX5")]
    public static void SelectAllAnnotations()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var space = (BlockTableRecord)transaction.GetObject(document.Database.CurrentSpaceId, OpenMode.ForRead);
        var selected = new List<ObjectId>();
        foreach (ObjectId objectId in space)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is Entity entity &&
                (entity is Dimension or Leader or MLeader or DBText or MText ||
                 Compatibility.Contains(entity.Layer, "标注", StringComparison.OrdinalIgnoreCase)))
            {
                selected.Add(objectId);
            }
        }

        document.Editor.SetImpliedSelection(selected.ToArray());
        document.Editor.WriteMessage($"\n[一只猫dc] 已选择 {selected.Count} 个标注或标注文字对象。" );
    }

    [CommandMethod("DC_CMD_SELECT_SIMILAR_IN_RANGE", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "范围内选择类似对象", "DCXZ")]
    public static void SelectSimilarInRange()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var range = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n请框选需要匹配类似对象的范围，回车确认: "
        });
        if (range.Status != PromptStatus.OK)
        {
            return;
        }

        var referenceResult = document.Editor.GetEntity("\n请在框选范围内选择一个参照对象: ");
        if (referenceResult.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        if (transaction.GetObject(referenceResult.ObjectId, OpenMode.ForRead, false) is not Entity reference)
        {
            return;
        }

        var selected = new List<ObjectId>();
        foreach (var objectId in range.Value.GetObjectIds())
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead, false) is Entity candidate &&
                candidate.GetRXClass() == reference.GetRXClass() &&
                string.Equals(candidate.Layer, reference.Layer, StringComparison.OrdinalIgnoreCase) &&
                candidate.ColorIndex == reference.ColorIndex &&
                string.Equals(candidate.Linetype, reference.Linetype, StringComparison.OrdinalIgnoreCase))
            {
                selected.Add(objectId);
            }
        }

        document.Editor.SetImpliedSelection(selected.ToArray());
        document.Editor.WriteMessage($"\n[一只猫dc] 已在框选范围内保留 {selected.Count} 个同类型、同图层、同颜色和同线型对象。" );
    }

    [CommandMethod("DC_CMD_DRAWING_DISPLAY_REPAIR", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "一键修复图纸显示", "DCTXF")]
    public static void RepairDisplay()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        document.Database.Ltscale = DrawingLayerRuntimeSettings.LinetypeScale;
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var blocks = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
        var hatches = 0;
        foreach (ObjectId blockId in blocks)
        {
            var block = (BlockTableRecord)transaction.GetObject(blockId, OpenMode.ForRead);
            if (block.IsFromExternalReference || block.IsFromOverlayReference)
            {
                continue;
            }

            foreach (ObjectId id in block)
            {
                if (transaction.GetObject(id, OpenMode.ForRead, false) is not Hatch hatch ||
                    !IsWallHatchLayer(hatch.Layer) ||
                    string.Equals(hatch.PatternName, "SOLID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                hatch.UpgradeOpen();
                hatch.PatternScale = DrawingLayerRuntimeSettings.WallHatchScale;
                hatch.EvaluateHatch(true);
                hatches++;
            }
        }

        transaction.Commit();
        document.Editor.Regen();
        document.Editor.WriteMessage($"\n[一只猫dc] 已设置全局虚线比例，并修复 {hatches} 个墙体填充。" );
    }

    private static void SetSelectedEntityVisibility(bool visible, bool remember)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var selection = document.Editor.SelectImplied();
        if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
        {
            selection = document.Editor.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = visible ? "\n请选择需要显示的图形: " : "\n请选择需要隐藏的图形: "
            });
        }

        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        var objectIds = selection.Value.GetObjectIds();
        var changed = SetEntityVisibility(document, objectIds, visible);
        if (remember && !visible)
        {
            lastHiddenDatabase = document.Database;
            lastHiddenObjectIds = objectIds;
        }

        document.Editor.SetImpliedSelection([]);
        document.Editor.WriteMessage($"\n[一只猫dc] 已{(visible ? "显示" : "隐藏")} {changed} 个图形。" );
    }

    private static int SetEntityVisibility(Document document, IReadOnlyList<ObjectId> objectIds, bool visible)
    {
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var changed = 0;
        foreach (var objectId in objectIds)
        {
            if (objectId.IsNull || objectId.IsErased || !objectId.IsValid ||
                transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity ||
                entity.Visible == visible)
            {
                continue;
            }

            var writable = (Entity)transaction.GetObject(objectId, OpenMode.ForWrite, false, true);
            writable.Visible = visible;
            changed++;
        }

        transaction.Commit();
        document.Editor.Regen();
        return changed;
    }

    private static void SetSelectedLayersLocked(bool locked)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        var selection = document.Editor.SelectImplied();
        if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
        {
            selection = document.Editor.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = locked ? "\n请选择需要锁定的图形: " : "\n请选择需要解锁的图形: "
            });
        }

        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layerIds = selection.Value.GetObjectIds()
            .Select(id => transaction.GetObject(id, OpenMode.ForRead, false))
            .OfType<Entity>()
            .Select(entity => entity.LayerId)
            .Distinct()
            .ToArray();
        var layers = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        if (locked && layers.Has("0") && layerIds.Contains(document.Database.Clayer))
        {
            document.Database.Clayer = layers["0"];
        }

        var changed = 0;
        foreach (var layerId in layerIds)
        {
            var layer = (LayerTableRecord)transaction.GetObject(layerId, OpenMode.ForWrite);
            if (layer.IsDependent || (locked && string.Equals(layer.Name, "0", StringComparison.OrdinalIgnoreCase)) ||
                layer.IsLocked == locked)
            {
                continue;
            }

            layer.IsLocked = locked;
            changed++;
        }

        transaction.Commit();
        document.Editor.SetImpliedSelection([]);
        document.Editor.WriteMessage($"\n[一只猫dc] 已{(locked ? "锁定" : "解锁")} {changed} 个对象所在图层。" );
    }

    private static void SelectByConfiguredLayer(string layerName, string label)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        using (var transaction = document.Database.TransactionManager.StartTransaction())
        {
            var layers = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
            if (!layers.Has(layerName))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 当前图纸不存在{label}配置图层：{layerName}。" );
                return;
            }
        }

        var filter = new SelectionFilter([new TypedValue((int)DxfCode.LayerName, layerName)]);
        var selection = document.Editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = $"\n请点选或框选{label}对象，回车完成: "
        }, filter);
        if (selection.Status != PromptStatus.OK)
        {
            return;
        }

        document.Editor.SetImpliedSelection(selection.Value.GetObjectIds());
        document.Editor.WriteMessage($"\n[一只猫dc] 已保留 {selection.Value.Count} 个 {layerName} 图层对象，可直接移动、复制或删除。" );
    }

    private static bool IsWallHatchLayer(string layerName)
    {
        return Compatibility.Contains(layerName, "墙体填充", StringComparison.OrdinalIgnoreCase) ||
               Compatibility.Contains(layerName, "护墙板", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(layerName, "DC_墙板", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveTargetLayer(Entity entity, out string target)
    {
        target = ResolveBoardCategoryLayer(entity);
        if (!string.IsNullOrEmpty(target))
        {
            return true;
        }

        target = ResolveKnownPluginLayer(entity.Layer);
        if (!string.IsNullOrEmpty(target))
        {
            return true;
        }

        target = entity switch
        {
            Dimension => GetConfiguredLayer(DrawingLayerCategories.CabinetAnnotation, "0-标注-柜体"),
            DBText or MText => "0-文字",
            Hatch => "0-墙体填充",
            _ => string.Empty
        };
        return !string.IsNullOrEmpty(target);
    }

    private static string ResolveBoardCategoryLayer(Entity entity)
    {
        using var data = entity.GetXDataForApplication(BoardCommands.BoardPropertyAppName);
        if (data is null)
        {
            return string.Empty;
        }

        var mappings = DrawingLayerRuntimeSettings.Current.CategoryLayers;
        foreach (var value in data.AsArray().Skip(1))
        {
            if (value.TypeCode != (int)DxfCode.ExtendedDataAsciiString || value.Value is not string category)
            {
                continue;
            }

            if (mappings.TryGetValue(category, out var layerName))
            {
                return layerName;
            }

            if (category.StartsWith(DrawingLayerCategories.ActivityShelf, StringComparison.Ordinal) &&
                mappings.TryGetValue(DrawingLayerCategories.ActivityShelf, out layerName))
            {
                return layerName;
            }
        }

        return string.Empty;
    }

    private static string ResolveKnownPluginLayer(string source) => source switch
    {
        "DC_门板" => GetConfiguredLayer(DrawingLayerCategories.DoorPanel, "0-门板图层"),
        "DC_抽面" => GetConfiguredLayer(DrawingLayerCategories.DoorPanel, "0-门板图层"),
        "DC_开门线" => "0-门板开虚线",
        "DC_铰链" => "0-铰链",
        "DC_抽面拉手" => "0-拉手",
        "DC_门板遮罩" or "DC_抽面遮罩" => "0-门板遮罩",
        "0-柜体结构" => GetConfiguredLayer(DrawingLayerCategories.CabinetStructure, "0-柜体结构"),
        "0-门板" or "0-门板图层" => GetConfiguredLayer(DrawingLayerCategories.DoorPanel, "0-门板图层"),
        "0-标注-柜体" => GetConfiguredLayer(DrawingLayerCategories.CabinetAnnotation, "0-标注-柜体"),
        "0-标注-门板" => GetConfiguredLayer(DrawingLayerCategories.DoorAnnotation, "0-标注-门板"),
        _ => string.Empty
    };

    private static string GetConfiguredLayer(string category, string fallback) =>
        DrawingLayerRuntimeSettings.Current.CategoryLayers.TryGetValue(category, out var layerName) &&
        !string.IsNullOrWhiteSpace(layerName)
            ? layerName
            : fallback;

    private static void HideMatching(IReadOnlyList<string> keywords)
    {
        SetLayerVisibility(name => keywords.Any(keyword => Compatibility.Contains(name, keyword, StringComparison.OrdinalIgnoreCase)), show: false);
    }

    private static void SetLayerVisibility(Func<string, bool> predicate, bool show)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        using var transaction = document.Database.TransactionManager.StartTransaction();
        var layers = (LayerTable)transaction.GetObject(document.Database.LayerTableId, OpenMode.ForRead);
        if (layers.Has("0"))
        {
            document.Database.Clayer = layers["0"];
        }

        var changed = 0;
        foreach (ObjectId id in layers)
        {
            var layer = (LayerTableRecord)transaction.GetObject(id, OpenMode.ForWrite);
            if (!show && !predicate(layer.Name))
            {
                continue;
            }

            var desiredOff = !show;
            if (layer.IsOff != desiredOff)
            {
                layer.IsOff = desiredOff;
                changed++;
            }
        }

        transaction.Commit();
        document.Editor.Regen();
        document.Editor.WriteMessage($"\n[一只猫dc] 已更新 {changed} 个图层的显示状态。" );
    }
}

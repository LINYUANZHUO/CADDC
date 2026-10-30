using Autodesk.AutoCAD.Colors;
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
}

public static class DrawingLayerCommands
{
    private static readonly (string Name, short Color)[] StandardLayers =
    [
        ("0-柜体结构", 1), ("0-柜体虚线", 8), ("0-正视图背板", 3), ("0-分区线", 8),
        ("0-护墙板图纸", 1), ("0-门板图层", 3), ("0-门板遮罩", 7), ("0-柜门拉伸线", 4),
        ("0-智能拉伸线图层", 4), ("0-五金", 2), ("0-门板开虚线", 8), ("0-标注-柜体", 7),
        ("0-标注-门板", 7), ("0-拉手", 2), ("0-暗拉手", 2), ("0-衣杆", 2),
        ("0-灯带", 2), ("0-铰链", 2), ("0-墙体", 1), ("0-墙体填充", 8),
        ("0-文字", 7), ("0-柜体文字", 7), ("0-柜门中档", 3), ("0-抽屉盒", 3),
        ("0-拆单图表", 7), ("0-图框", 7), ("0-矩形打印", 7), ("0-板件标识图层", 7)
    ];

    [CommandMethod("DC_CMD_LAYER_MANAGER", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "快速图层管理器", "TC")]
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
    [ShortcutCommand("图层图纸", "一键生成标准图层", "TCJZ")]
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
        foreach (var definition in StandardLayers)
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
    [ShortcutCommand("图层图纸", "一键智能归层", "TCGC")]
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
    [ShortcutCommand("图层图纸", "图纸垃圾清理", "PU")]
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
    [ShortcutCommand("图层图纸", "隐藏五金图层", "TCWJ")]
    public static void HideHardware() => HideMatching(["五金", "铰链", "拉手", "衣杆", "灯带"]);

    [CommandMethod("DC_CMD_LAYER_HIDE_DOOR", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏门板图层", "TCMB")]
    public static void HideDoor() => HideMatching(["门板", "抽面", "开门线"]);

    [CommandMethod("DC_CMD_LAYER_HIDE_CABINET", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏柜体图层", "TCHG")]
    public static void HideCabinet() => HideMatching(["柜体", "背板", "分区线"]);

    [CommandMethod("DC_CMD_LAYER_HIDE_ANNOTATION", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "隐藏标注图层", "TCBZ")]
    public static void HideAnnotation() => HideMatching(["标注", "文字"]);

    [CommandMethod("DC_CMD_LAYER_SHOW_ALL", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "显示所有图层", "TCXS")]
    public static void ShowAllLayers()
    {
        SetLayerVisibility(_ => true, show: true);
    }

    [CommandMethod("DC_CMD_DRAWING_DISPLAY_REPAIR", CommandFlags.Modal)]
    [ShortcutCommand("图层图纸", "一键修复图纸显示", "TXF")]
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

    private static bool IsWallHatchLayer(string layerName)
    {
        return layerName.Contains("墙体填充", StringComparison.OrdinalIgnoreCase) ||
               layerName.Contains("护墙板", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(layerName, "DC_墙板", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveTargetLayer(Entity entity, out string target)
    {
        target = entity switch
        {
            Dimension => "0-标注-柜体",
            DBText or MText => "0-文字",
            Hatch => "0-墙体填充",
            _ => ResolveKnownPluginLayer(entity.Layer)
        };
        return !string.IsNullOrEmpty(target);
    }

    private static string ResolveKnownPluginLayer(string source) => source switch
    {
        "DC_门板" => "0-门板图层",
        "DC_抽面" => "0-门板图层",
        "DC_开门线" => "0-门板开虚线",
        "DC_铰链" => "0-铰链",
        "DC_抽面拉手" => "0-拉手",
        "DC_门板遮罩" or "DC_抽面遮罩" => "0-门板遮罩",
        _ => string.Empty
    };

    private static void HideMatching(IReadOnlyList<string> keywords)
    {
        SetLayerVisibility(name => keywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)), show: false);
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

using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class SmartAnnotationCommands
{
    [CommandMethod("DC_CMD_DIM_CABINET", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "柜体标注", "DCBC")]
    public static void DimensionCabinet() => Execute("LS_CMD_DIM_CABINET_ALL");

    [CommandMethod("DC_CMD_DIM_PANEL", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "门板标注", "DCBM")]
    public static void DimensionPanel() => Execute("LS_CMD_DIM_PANEL_ALL");

    [CommandMethod("DC_CMD_DIM_LEFT_BOTTOM", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "左下标注", "DCBZ")]
    public static void DimensionLeftBottom() => Execute("LS_CMD_DIM_LEFT_BOTTOM");

    [CommandMethod("DC_CMD_DIM_RIGHT_TOP", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "右上标注", "DCBY")]
    public static void DimensionRightTop() => Execute("LS_CMD_DIM_RIGHT_TOP");

    [CommandMethod("DC_CMD_DIM_INNER", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "内空标注", "DCBN")]
    public static void DimensionInner() => Execute("LS_CMD_DIM_INNER");

    [CommandMethod("DC_CMD_DIM_SINGLE", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "单边标注", "DCBD")]
    public static void DimensionSingle() => Execute("LS_CMD_DIM_SMART_SINGLE");

    [CommandMethod("DC_CMD_DIM_CONTINUE", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "连续标注", "DCBL")]
    public static void DimensionContinue() => Execute("LS_CMD_DIM_CONTINUE");

    [CommandMethod("DC_CMD_DIM_SMART_OBJECT", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "智能对象", "DCBDX")]
    public static void DimensionSmartObject() => Execute("LS_CMD_DIM_SMART_OBJECT");

    [CommandMethod("DC_CMD_DIM_BATCH_DYNAMIC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "批量动态标注", "DCBP")]
    public static void DimensionBatchDynamic() => Execute("LS_CMD_BPDIM");

    [CommandMethod("DC_CMD_DIM_SELECT_DYNAMIC", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "选择标注动态", "DCBX")]
    public static void DimensionSelectDynamic() => Execute("LS_CMD_DIM_SMART_DRAG");

    [CommandMethod("DC_CMD_DIM_ALIGN_BATCH", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "批量对齐", "DCBAA")]
    public static void AlignBatch() => Execute("LS_CMD_DIM_ALIGNED_BATCH");

    [CommandMethod("DC_CMD_DIM_ALIGN_CONTINUE", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "连续对齐", "DCBAC")]
    public static void AlignContinue() => Execute("LS_CMD_DIM_ALIGNED_CONT");

    [CommandMethod("DC_CMD_DIM_OUTER", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "外围线条标注", "DCBWW")]
    public static void DimensionOuter() => Execute("LS_CMD_DIM_OUTER");

    [CommandMethod("DC_CMD_DIM_SCALE", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "标注缩放", "DCBS")]
    public static void ScaleDimensions() => Execute("LS_CMD_DIM_SCALE");

    [CommandMethod("DC_CMD_DIM_SPLIT", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "标注分割", "DCBF")]
    public static void SplitDimensions() => Execute("LS_CMD_DIM_SPLIT");

    [CommandMethod("DC_CMD_DIM_ALIGN", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "标注对齐", "DCBDQ")]
    public static void AlignDimensions() => Execute("LS_CMD_DIM_ALIGN");

    [CommandMethod("DC_CMD_DIM_JOIN", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "合并标注", "DCBJ")]
    public static void JoinDimensions() => Execute("LS_CMD_DIM_SMART_JOIN");

    [CommandMethod("DC_CMD_DIM_DELETE_ALL", CommandFlags.Modal | CommandFlags.UsePickSet)]
    [ShortcutCommand("智能标注", "清除所有标注", "DCBQ")]
    public static void DeleteDimensions() => Execute("LS_CMD_DIM_DELETE_ONLY", synchronizeStyle: false);

    private static void Execute(string externalCommand, bool synchronizeStyle = true)
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            var options = SmartAnnotationRuntimeSettings.Current;
            if (!LeishenDimensionConfigBridge.TryApply(options, out var configMessage))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 智能标注参数同步失败：{configMessage}");
                return;
            }

            if (synchronizeStyle &&
                !DimensionStyleSyncService.TrySynchronize(document, options, out var styleMessage))
            {
                document.Editor.WriteMessage($"\n[一只猫dc] 标注样式同步失败：{styleMessage}");
                return;
            }

            document.SendStringToExecute(
                externalCommand + " ",
                activate: true,
                wrapUpInactiveDoc: false,
                echoCommand: false);
        }
        catch (System.Exception exception)
        {
            document.Editor.WriteMessage($"\n[一只猫dc] 智能标注启动失败：{exception.Message}");
        }
    }
}

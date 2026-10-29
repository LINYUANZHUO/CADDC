using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Plugin.Shortcuts;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class StatusCommands
{
    [CommandMethod("DC_CMD_STATUS", CommandFlags.Modal)]
    [ShortcutCommand("系统", "检查插件运行状态", "JC")]
    public static void ShowStatus()
    {
        var document = AcadApplication.DocumentManager.MdiActiveDocument;
        document?.Editor.WriteMessage("\n一只猫dc：插件运行正常，当前版本 0.1.0。");
    }
}

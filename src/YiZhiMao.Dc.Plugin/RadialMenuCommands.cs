using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Core;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace YiZhiMao.Dc.Plugin;

public static class RadialMenuCommands
{
    [CommandMethod("DC_CMD_RADIAL_MENU", CommandFlags.Modal)]
    [ShortcutCommand("圆盘", "打开圆盘工具", "DCYP")]
    public static void ShowRadialMenu()
    {
        if (!ModulePreferencesRuntime.EnsureEnabled(ToolboxFeatureModule.RadialMenu))
        {
            return;
        }

        var options = RadialMenuRuntime.Options;
        if (!options.Enabled)
        {
            AcadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\n[一只猫dc] 圆盘工具已关闭，请在工具箱的圆盘设置中启用。");
            return;
        }

        RadialMenuWindowManager.Show(options);
    }
}

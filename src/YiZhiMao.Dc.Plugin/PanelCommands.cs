using Autodesk.AutoCAD.Runtime;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;

namespace YiZhiMao.Dc.Plugin;

public static class PanelCommands
{
    [CommandMethod("DC_CMD_PANEL", CommandFlags.Modal)]
    [ShortcutCommand("系统", "打开快捷键面板", "DCS")]
    public static void ShowPanelByCoreCommand()
    {
        ToolboxPalette.Show();
    }
}

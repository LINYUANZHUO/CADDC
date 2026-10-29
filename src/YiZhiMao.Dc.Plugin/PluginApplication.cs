using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(YiZhiMao.Dc.Plugin.PluginApplication))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.StatusCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.PanelCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.ImageBatchCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.BatchPrintCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.HardwareCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.DoorPanelCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.PlanDoorPanelCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.DrawerFaceCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.HandleCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.DrawingLayerCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.WainscotCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.BoardCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.AdvancedBoardCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.StatisticsCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.FrameCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.RadialMenuCommands))]

namespace YiZhiMao.Dc.Plugin;

public sealed class PluginApplication : IExtensionApplication
{
    public void Initialize()
    {
        AcadApplication.DocumentManager.DocumentCreated += OnDocumentCreated;
        RadialMenuMouseActivation.Initialize();

        try
        {
            ShortcutManager.Initialize();
            AcadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\n一只猫dc 已加载。输入 DCS 打开工具箱，输入 DC_CMD_STATUS 检查插件状态。");
        }
        catch (System.Exception exception)
        {
            AcadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\n一只猫dc 已加载，但快捷键初始化失败：{exception.Message}");
        }
    }

    public void Terminate()
    {
        AcadApplication.DocumentManager.DocumentCreated -= OnDocumentCreated;
        RadialMenuMouseActivation.Dispose();
        ToolboxPalette.Dispose();
        RadialMenuWindowManager.Dispose();
    }

    private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
    {
        ShortcutManager.ApplyTo(e.Document);
    }
}

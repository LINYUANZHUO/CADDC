using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using System.IO;
using YiZhiMao.Dc.Plugin.Shortcuts;
using YiZhiMao.Dc.Plugin.Views;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(YiZhiMao.Dc.Plugin.PluginApplication))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.StatusCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.PanelCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.ImageBatchCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.BatchPrintCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.SuperLibraryCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.HardwareCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.DoorPanelCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.PlanDoorPanelCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.DrawerFaceCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.HandleCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.DrawingLayerCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.WainscotCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.SmartAnnotationCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.BoardCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.AdvancedBoardCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.CabinetCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.PlanCabinetCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.StatisticsCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.FrameCommands))]
[assembly: CommandClass(typeof(YiZhiMao.Dc.Plugin.RadialMenuCommands))]

namespace YiZhiMao.Dc.Plugin;

public sealed class PluginApplication : IExtensionApplication
{
    private static bool autoExpandPending;

    public void Initialize()
    {
        PluginDependencyResolver.Initialize();
        AcadApplication.DocumentManager.DocumentCreated += OnDocumentCreated;
        if (IsCoreConsole())
        {
            return;
        }

        TryAutoExpandImmediately();

        if (!CadQuickToggleRuntime.TryApplyConfiguredSaveFileFormat(out var saveFormatError))
        {
            AcadApplication.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                $"\n一只猫dc 默认保存版本应用失败：{saveFormatError}");
        }

        SmartInputMethodRuntime.Initialize();
        MissingShxReplacementRuntime.Initialize();
        ShxFontSyncRuntime.Initialize();
        ModuleCommandGuard.Initialize();
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
        AcadApplication.Idle -= OnInitialIdle;
        autoExpandPending = false;
        AcadApplication.DocumentManager.DocumentCreated -= OnDocumentCreated;
        SmartInputMethodRuntime.Dispose();
        MissingShxReplacementRuntime.Dispose();
        ShxFontSyncRuntime.Dispose();
        ModuleCommandGuard.Dispose();
        RadialMenuMouseActivation.Dispose();
        ToolboxPalette.Dispose();
        SuperLibraryWindowManager.Dispose();
        RadialMenuWindowManager.Dispose();
        PluginDependencyResolver.Dispose();
    }

    private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
    {
        if (IsCoreConsole())
        {
            return;
        }

        ShortcutManager.ApplyTo(e.Document);
    }

    private static void ScheduleAutoExpand()
    {
        if (!ToolboxSettingsRuntime.Settings.Preferences.AutoExpandSidebar || autoExpandPending)
        {
            return;
        }

        autoExpandPending = true;
        AcadApplication.Idle += OnInitialIdle;
    }

    private static void TryAutoExpandImmediately()
    {
        if (!ToolboxSettingsRuntime.Settings.Preferences.AutoExpandSidebar)
        {
            return;
        }

        try
        {
            ToolboxPalette.Show();
        }
        catch (System.Exception)
        {
            ToolboxPalette.Dispose();
            ScheduleAutoExpand();
        }
    }

    private static void OnInitialIdle(object? sender, EventArgs e)
    {
        if (AcadApplication.DocumentManager.MdiActiveDocument is null)
        {
            return;
        }

        AcadApplication.Idle -= OnInitialIdle;
        autoExpandPending = false;
        if (ToolboxSettingsRuntime.Settings.Preferences.AutoExpandSidebar)
        {
            ToolboxPalette.Show();
        }
    }

    private static bool IsCoreConsole() =>
        string.Equals(
            Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName),
            "accoreconsole",
            StringComparison.OrdinalIgnoreCase);
}

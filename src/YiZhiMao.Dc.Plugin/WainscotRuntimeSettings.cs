using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class WainscotRuntimeSettings
{
    public static GrilleOptions Grille
    {
        get => ToolboxSettingsRuntime.Settings.Grille;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.Grille = value);
    }

    public static void SaveGrille(GrilleOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.Grille = value);

    public static PerforatedPanelOptions PerforatedPanel
    {
        get => ToolboxSettingsRuntime.Settings.PerforatedPanel;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.PerforatedPanel = value);
    }

    public static void SavePerforatedPanel(PerforatedPanelOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.PerforatedPanel = value);

    public static BackgroundWallOptions BackgroundWall
    {
        get => ToolboxSettingsRuntime.Settings.BackgroundWall;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.BackgroundWall = value);
    }

    public static void SaveBackgroundWall(BackgroundWallOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.BackgroundWall = value);
}

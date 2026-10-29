using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class WainscotRuntimeSettings
{
    public static GrilleOptions Grille { get; set; } = GrilleOptions.Default;

    public static PerforatedPanelOptions PerforatedPanel { get; set; } = PerforatedPanelOptions.Default;

    public static BackgroundWallOptions BackgroundWall { get; set; } = BackgroundWallOptions.Default;
}

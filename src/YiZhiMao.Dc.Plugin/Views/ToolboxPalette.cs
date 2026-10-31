using Autodesk.AutoCAD.Windows;
using DrawingSize = System.Drawing.Size;

namespace YiZhiMao.Dc.Plugin.Views;

internal enum ToolboxModule
{
    Preferences,
    Shortcuts,
    Images,
    BatchPrint,
    DoorPanels,
    Hardware,
    WallPanels,
    Boards,
    Statistics,
    DrawingLayers,
    OneClickFrame,
    RadialMenu,
    LispMarket
}

internal static class ToolboxPalette
{
    private static readonly Guid PaletteId = new("06A42E44-C73B-4D97-8E75-3B49958DF6A6");
    private static PaletteSet? paletteSet;
    private static ShortcutPanelView? panelView;

    public static void Show(ToolboxModule? module = null)
    {
        if (paletteSet is null)
        {
            paletteSet = new PaletteSet("一只猫dc", PaletteId)
            {
                DockEnabled = DockSides.Left | DockSides.Right,
            MinimumSize = new DrawingSize(32, 360),
                Size = new DrawingSize(720, 640),
                Style = PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.ShowPropertiesMenu
            };
            panelView = new ShortcutPanelView();
            paletteSet.AddVisual("工具箱", panelView, true);
        }

        if (module.HasValue)
        {
            panelView?.SelectModule(module.Value);
        }
        paletteSet.Visible = true;
    }

    public static void Dispose()
    {
        paletteSet?.Dispose();
        paletteSet = null;
        panelView = null;
    }
}

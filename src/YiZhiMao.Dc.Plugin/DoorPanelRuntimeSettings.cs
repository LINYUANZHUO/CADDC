using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class DoorPanelRuntimeSettings
{
    public static DoorPanelOptions Current { get; set; } = DoorPanelOptions.Default;
}

internal static class PlanDoorPanelRuntimeSettings
{
    private static double? lastActualGap;

    public static PlanDoorPanelOptions Current { get; set; } = PlanDoorPanelOptions.Default;

    public static double? LastActualGap => lastActualGap;

    public static event Action<double>? ActualGapChanged;

    public static void PublishActualGap(double actualGap)
    {
        if (lastActualGap.HasValue && Math.Abs(lastActualGap.Value - actualGap) < 0.000001)
        {
            return;
        }

        lastActualGap = actualGap;
        ActualGapChanged?.Invoke(actualGap);
    }
}

internal static class DrawerFaceRuntimeSettings
{
    private static DrawerFaceLayout? lastLayout;

    public static DrawerFaceOptions Current { get; set; } = DrawerFaceOptions.Default;

    public static HardwareLibraryItem? SelectedHandle { get; set; }

    public static DrawerFaceLayout? LastLayout => lastLayout;

    public static event Action<DrawerFaceLayout>? LayoutChanged;

    public static void PublishLayout(DrawerFaceLayout layout)
    {
        lastLayout = layout;
        LayoutChanged?.Invoke(layout);
    }
}

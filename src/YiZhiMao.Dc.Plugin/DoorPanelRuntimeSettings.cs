using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class DoorPanelRuntimeSettings
{
    public static DoorPanelOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.DoorPanel;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.DoorPanel = value);
    }

    public static void Save(DoorPanelOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.DoorPanel = value);
}

internal static class PlanDoorPanelRuntimeSettings
{
    private static double? lastActualGap;

    public static PlanDoorPanelOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.PlanDoorPanel;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.PlanDoorPanel = value);
    }

    public static void Save(PlanDoorPanelOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.PlanDoorPanel = value);

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

    public static DrawerFaceOptions Current
    {
        get => ToolboxSettingsRuntime.Settings.DrawerFace;
        set => ToolboxSettingsRuntime.SetInMemory(settings => settings.DrawerFace = value);
    }

    public static void Save(DrawerFaceOptions value) =>
        ToolboxSettingsRuntime.Update(settings => settings.DrawerFace = value);

    public static DrawerFaceLayout? LastLayout => lastLayout;

    public static event Action<DrawerFaceLayout>? LayoutChanged;

    public static void PublishLayout(DrawerFaceLayout layout)
    {
        lastLayout = layout;
        LayoutChanged?.Invoke(layout);
    }
}

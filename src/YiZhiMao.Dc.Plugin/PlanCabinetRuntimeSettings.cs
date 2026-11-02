using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class PlanCabinetRuntimeSettings
{
    public static PlanCabinetOptions Current => ToolboxSettingsRuntime.Settings.PlanCabinet;

    public static void Save(PlanCabinetOptions options) =>
        ToolboxSettingsRuntime.Update(settings => settings.PlanCabinet = options);
}

using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class CabinetRuntimeSettings
{
    public static CabinetOptions Current => ToolboxSettingsRuntime.Settings.Cabinet;

    public static void Save(CabinetOptions options) =>
        ToolboxSettingsRuntime.Update(settings => settings.Cabinet = options);
}

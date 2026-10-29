using System.IO;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin.Printing;

internal static class PrintRuntime
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YiZhiMaoDc",
        "PrintSettings.json");

    private static readonly PrintSettingsStore Store = new(SettingsPath);

    public static PrintSettings Settings { get; private set; } = Store.Load();

    public static void Save(PrintSettings settings)
    {
        Store.Save(settings);
        Settings = settings;
    }
}

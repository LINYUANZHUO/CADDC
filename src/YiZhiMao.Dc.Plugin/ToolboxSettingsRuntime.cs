using System.IO;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class ToolboxSettingsRuntime
{
    private static readonly object SyncRoot = new();
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YiZhiMaoDc",
        "ToolboxSettings.json");
    private static readonly ToolboxSettingsStore Store = new(SettingsPath);
    private static ToolboxSettings settings = Store.Load();

    public static ToolboxSettings Settings => settings;

    public static void SetInMemory(Action<ToolboxSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (SyncRoot)
        {
            var next = settings.Clone();
            update(next);
            next.Validate();
            settings = next;
        }
    }

    public static void Update(Action<ToolboxSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        lock (SyncRoot)
        {
            var next = settings.Clone();
            update(next);
            Store.Save(next);
            settings = next;
        }
    }
}

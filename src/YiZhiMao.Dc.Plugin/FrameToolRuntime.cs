using System.IO;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class FrameToolRuntime
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YiZhiMaoDc",
        "FrameToolOptions.json");

    private static readonly FrameToolOptionsStore Store = new(SettingsPath);

    public static FrameToolOptions Options { get; private set; } = Store.Load();

    public static void Save(FrameToolOptions options)
    {
        Store.Save(options);
        Options = options.Clone();
    }
}

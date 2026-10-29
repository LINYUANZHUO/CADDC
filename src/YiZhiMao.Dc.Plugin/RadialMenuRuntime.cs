using System.IO;
using YiZhiMao.Dc.Core;

namespace YiZhiMao.Dc.Plugin;

internal static class RadialMenuRuntime
{
    private static readonly string OptionsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YiZhiMaoDc",
        "RadialMenuOptions.json");

    private static RadialMenuOptions options = RadialMenuOptionsStore.Load(OptionsPath);

    public static RadialMenuOptions Options => options.Clone();

    public static void Save(RadialMenuOptions value)
    {
        value.Validate();
        RadialMenuOptionsStore.Save(OptionsPath, value);
        options = value.Clone();
    }
}

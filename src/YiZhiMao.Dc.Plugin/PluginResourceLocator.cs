using System.IO;

namespace YiZhiMao.Dc.Plugin;

internal static class PluginResourceLocator
{
    public static string GetPluginRoot()
    {
        var assemblyDirectory = Path.GetDirectoryName(typeof(PluginResourceLocator).Assembly.Location)
                                ?? AppContext.BaseDirectory;
        if (Directory.Exists(Path.Combine(assemblyDirectory, "Modules")))
        {
            return assemblyDirectory;
        }

        var parentDirectory = Directory.GetParent(assemblyDirectory)?.FullName;
        return parentDirectory is not null && Directory.Exists(Path.Combine(parentDirectory, "Modules"))
            ? parentDirectory
            : assemblyDirectory;
    }
}
